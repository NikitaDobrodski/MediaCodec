using MediaCodec.Video;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MediaCodec;

/// <summary>
/// Синхронный плеер MJPEG + WAV с истинной A/V синхронизацией
///
/// Синхронизация:
///   Кадр выбирается по Stopwatch — оба потока (видео/аудио) стартуют одновременно
///   Если аудио заканчивается раньше видео — плеер останавливается
///   Если видео заканчивается раньше — аудио принудительно останавливается
/// Аудио: Windows MCI (winmm.dll, P/Invoke) — без внешних зависимостей
///
/// Важно: MCI waveaudio требует, чтобы stop/close вызывались с того же потока
///        (или STA-потока), с которого вызывались open/play. Поэтому все MCI-команды
///        выполняются через SynchronizationContext UI-потока
/// </summary>
public sealed class MjpegPlayer : IDisposable
{
    #region Поля

    private string? _mjpegPath;
    private (long Offset, int Length)[]? _index;
    private int _frameCount;
    private int _fps;
    private int _totalMs;

    private System.Threading.Timer? _timer;
    private readonly Stopwatch _stopwatch = new();
    private MciAudioPlayer? _audio;

    // UI-поток SynchronizationContext — нужен для остановки MCI из фонового потока
    private SynchronizationContext? _uiContext;

    private int _lastFrameIdx = -1;
    private volatile bool _playing;

    // Флаг: MCI хотя бы раз вернул "playing" — защита от false-positive на старте
    private volatile bool _audioConfirmedPlaying;
    // Флаг: Stop() уже выполнен (предотвращает дублирование PlaybackStopped)
    private int _stopGuard;

    #endregion

    #region События

    /// <summary>Новый кадр готов к отображению: (bitmap, номер 1-based, всего кадров)</summary>
    public event Action<Bitmap, int, int>? FrameReady;

    /// <summary>Тик времени: (текущее мс, общее мс)</summary>
    public event Action<int, int>? TimeUpdated;

    /// <summary>Воспроизведение завершено или остановлено</summary>
    public event Action? PlaybackStopped;

    #endregion

    #region Публичный API

    /// <summary>
    /// Запускает воспроизведение. Должен вызываться с UI-потока
    /// </summary>
    /// <param name="mjpegPath">Путь к .mjpeg файлу</param>
    /// <param name="wavPath">Путь к PCM WAV (null — только видео)</param>
    /// <param name="fps">Целевой FPS (по умолчанию 30)</param>
    public void Play(string mjpegPath, string? wavPath, int fps = 30)
    {
        Stop();

        // Захватываем контекст UI-потока — MCI stop/close
        _uiContext = SynchronizationContext.Current;

        _mjpegPath = mjpegPath;
        _fps = fps;
        _index = MjpegDecoder.BuildFrameIndex(mjpegPath);
        _frameCount = _index.Length;
        _totalMs = (int)(_frameCount / (double)_fps * 1000);
        _lastFrameIdx = -1;
        _audioConfirmedPlaying = false;
        _stopGuard = 0;
        _playing = true;

        // Stopwatch стартует одновременно с MCI — drift < 1 мс
        _stopwatch.Restart();

        if (wavPath is not null && File.Exists(wavPath))
        {
            _audio = new MciAudioPlayer();
            _audio.Open(wavPath);
            _audio.Play();
        }

        // Тикаем вдвое чаще FPS — не пропускаем кадры при лёгких задержках декодирования
        int intervalMs = Math.Max(1, 1000 / (_fps * 2));
        _timer = new System.Threading.Timer(OnTick, null, 0, intervalMs);
    }

    /// <summary>Останавливает воспроизведение и освобождает ресурсы. Идемпотентен</summary>
    public void Stop()
    {
        _playing = false;

        // Таймер останавливаем сразу — Interlocked.Exchange предотвращает двойной Dispose
        var timer = Interlocked.Exchange(ref _timer, null);
        timer?.Dispose();

        // Аудио забираем из поля атомарно
        var audio = Interlocked.Exchange(ref _audio, null);
        if (audio != null)
        {
            // MCI требует stop/close с того же потока, с которого был вызван open/play (UI-поток)
            // Если у нас есть UI-контекст и мы НЕ на UI-потоке — маршалируем синхронно
            // Send() блокирует вызывающий поток до завершения делегата — это нужно, чтобы аудио гарантированно остановилось до возврата из Stop().
            var ctx = _uiContext;
            if (ctx != null && SynchronizationContext.Current != ctx)
                ctx.Send(_ => { audio.Stop(); audio.Dispose(); }, null);
            else
            { audio.Stop(); audio.Dispose(); }
        }

        _stopwatch.Stop();

        // PlaybackStopped стреляем только если таймер реально был активен
        // Это предотвращает ложное событие при вызове Stop() в начале Play() (для очистки)
        if (timer is not null && Interlocked.Exchange(ref _stopGuard, 1) == 0)
            PlaybackStopped?.Invoke();
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    #endregion

    #region Обратный вызов таймера

    private void OnTick(object? state)
    {
        if (!_playing || _index is null) return;

        // Stopwatch надёжно отсчитывает время от момента запуска
        long elapsedMs = _stopwatch.ElapsedMilliseconds;
        int frameIdx = (int)(elapsedMs * _fps / 1000.0);

        // Конец видео ИЛИ конец аудио — останавливаем всё синхронно
        // audioEnded проверяем только ПОСЛЕ того, как MCI хотя бы раз вернул "playing" — иначе на первом тике (delay=0) IsPlaying() ещё false → ложное завершение
        if (_audio is not null && !_audioConfirmedPlaying && _audio.IsPlaying())
            _audioConfirmedPlaying = true;

        bool videoEnded = frameIdx >= _frameCount;
        bool audioEnded = _audio is not null && _audioConfirmedPlaying && !_audio.IsPlaying();

        if (videoEnded || audioEnded)
        {
            _playing = false;  // блокируем повторный вход до вызова Stop()
            // Таймер нельзя диспозить из его собственного колбека — откладываем в ThreadPool
            // Stop() внутри использует _uiContext.Send() для корректной остановки MCI
            ThreadPool.QueueUserWorkItem(_ => Stop());
            return;
        }

        // Тот же кадр — ничего не делаем
        if (frameIdx == _lastFrameIdx) return;
        _lastFrameIdx = frameIdx;

        try
        {
            var (offset, length) = _index[frameIdx];
            var bmp = MjpegDecoder.DecodeFrameAtOffset(_mjpegPath!, offset, length);

            FrameReady?.Invoke(bmp, frameIdx + 1, _frameCount);
            TimeUpdated?.Invoke((int)elapsedMs, _totalMs);
        }
        catch
        {
            // Не роняем плеер из-за одного повреждённого кадра
        }
    }

    #endregion
}

// MciAudioPlayer

/// <summary>
/// Тонкая обёртка над Windows MCI (winmm.dll)
/// Умеет открывать WAV, воспроизводить и запрашивать текущую позицию
/// Все публичные методы должны вызываться с UI-потока (или с того, с которого был вызван Open)
/// </summary>
internal sealed class MciAudioPlayer : IDisposable
{
    #region P/Invoke

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(
        string command,
        StringBuilder? returnValue,
        int returnLength,
        IntPtr hwnd);

    #endregion

    #region Поля

    private const string Alias = "mciaudio_mediacodec";
    private bool _opened;

    #endregion

    #region Публичный API

    /// <summary>Открывает WAV-файл для воспроизведения</summary>
    public void Open(string wavPath)
    {
        // Закрываем предыдущий экземпляр на случай, если не был освобождён
        mciSendString($"close {Alias}", null, 0, IntPtr.Zero);

        mciSendString($"open \"{wavPath}\" type waveaudio alias {Alias}", null, 0, IntPtr.Zero);
        // Явно задаём миллисекунды — MCI по умолчанию может использовать другой формат
        mciSendString($"set {Alias} time format milliseconds", null, 0, IntPtr.Zero);
        _opened = true;
    }

    /// <summary>Запускает воспроизведение</summary>
    public void Play()
    {
        if (!_opened) return;
        mciSendString($"play {Alias}", null, 0, IntPtr.Zero);
    }

    /// <summary>Возвращает текущую позицию воспроизведения в миллисекундах</summary>
    public int PositionMs()
    {
        if (!_opened) return 0;
        var sb = new StringBuilder(64);
        mciSendString($"status {Alias} position", sb, 64, IntPtr.Zero);
        return int.TryParse(sb.ToString().Trim(), out int ms) ? ms : 0;
    }

    /// <summary>
    /// Возвращает true, если аудио сейчас воспроизводится
    /// Используется для определения естественного конца трека
    /// </summary>
    public bool IsPlaying()
    {
        if (!_opened) return false;
        var sb = new StringBuilder(64);
        mciSendString($"status {Alias} mode", sb, 64, IntPtr.Zero);
        return sb.ToString().Trim().Equals("playing", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Останавливает воспроизведение</summary>
    public void Stop()
    {
        if (!_opened) return;
        mciSendString($"stop {Alias}", null, 0, IntPtr.Zero);
    }

    /// <summary>Останавливает и освобождает MCI-устройство</summary>
    public void Dispose()
    {
        if (_opened)
        {
            mciSendString($"stop {Alias}", null, 0, IntPtr.Zero);  // stop перед close
            mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
            _opened = false;
        }
    }

    #endregion
}