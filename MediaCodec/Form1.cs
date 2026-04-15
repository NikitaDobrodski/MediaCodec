using MediaCodec.Audio;
using MediaCodec.Video;

namespace MediaCodec;

public partial class Form1 : Form
{
    #region Fields

    private string? _wavPath;
    private string? _framesFolder;
    private CancellationTokenSource? _cts;

    #endregion

    #region Constructor

    public Form1()
    {
        InitializeComponent();
        WireEvents();
        Log("MediaCodec ready.", LogColor.Muted);
    }

    #endregion

    #region Event wiring

    private void WireEvents()
    {
        btnLoadWav.Click += BtnLoadWav_Click;
        btnLoadFrames.Click += BtnLoadFrames_Click;
        btnGenFrames.Click += BtnGenFrames_Click;
        btnEncode.Click += BtnEncode_Click;
        btnDecode.Click += BtnDecode_Click;
        btnPlay.Click += BtnPlay_Click;
        btnStop.Click += BtnStop_Click;
    }

    #endregion

    #region Form lifecycle

    /// <summary>
    /// Вызывается при первом отображении формы
    /// </summary>
    /// <param name="e">Аргументы события</param>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Order matters: MinSizes first, then SplitterDistance.
        splitMain.Panel1MinSize = 300;
        splitMain.Panel2MinSize = 220;
        splitMain.SplitterDistance = (int)(ClientSize.Width * 0.65);
    }

    #endregion

    #region Load

    /// <summary>
    /// Загружает WAV-файл
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void BtnLoadWav_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select WAV file",
            Filter = "WAV files (*.wav)|*.wav"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _wavPath = dlg.FileName;
        Log($"WAV loaded: {Path.GetFileName(_wavPath)}", LogColor.Blue);
        SetStatus($"WAV: {Path.GetFileName(_wavPath)}");
    }

    /// <summary>
    /// Загружает PNG-файлы
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void BtnLoadFrames_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select folder with PNG frames",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _framesFolder = dlg.SelectedPath;
        var count = Directory.GetFiles(_framesFolder, "*.png").Length;
        Log($"Frames: {Path.GetFileName(_framesFolder)}  ({count} PNG files)", LogColor.Blue);
        SetStatus($"Frames: {count} files");
    }

    /// <summary>
    /// Генерирует PNG-файлы
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private async void BtnGenFrames_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select folder to save generated frames",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _framesFolder = dlg.SelectedPath;
        var folder = _framesFolder;

        SetBusy(true);
        try
        {
            Log("Generating 100 test frames (320×240)...", LogColor.Muted);
            await Task.Run(() => TestFrameGenerator.Generate(folder, count: 100, width: 320, height: 240));

            int saved = Directory.GetFiles(folder, "*.png").Length;
            Log($"Generated {saved} frames → {Path.GetFileName(folder)}", LogColor.Green);
            SetStatus($"Frames: {saved} files");
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}", LogColor.Red);
        }
        finally
        {
            SetBusy(false);
        }
    }

    #endregion

    #region Encode

    /// <summary>
    /// Запускает кодирование
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private async void BtnEncode_Click(object? sender, EventArgs e)
    {
        if (_wavPath is null && _framesFolder is null)
        {
            Log("Nothing to encode. Load WAV and/or Frames first.", LogColor.Amber);
            return;
        }

        SetBusy(true);
        _cts = new CancellationTokenSource();

        try
        {
            //Audio: WAV → ADPCM
            if (_wavPath is not null)
            {
                Log("Starting IMA ADPCM encode...", LogColor.Green);
                await Task.Run(() =>
                {
                    var wav = WavReader.Load(_wavPath);
                    var encoder = new ImaAdpcmEncoder();
                    var adpcm = encoder.Encode(wav.Samples, wav.Channels);

                    var outPath = Path.ChangeExtension(_wavPath, ".adpcm");
                    File.WriteAllBytes(outPath, adpcm);
                    Log($"ADPCM saved: {Path.GetFileName(outPath)}", LogColor.Green);
                }, _cts.Token);
            }

            //Video: PNG frames → MJPEG
            if (_framesFolder is not null)
            {
                Log("Starting MJPEG encode...", LogColor.Green);

                var outPath = Path.Combine(_framesFolder, "output.mjpeg");
                var encoder = new MjpegEncoder();

                encoder.ProgressChanged += (cur, total) =>
                {
                    SetProgress((int)(cur / (double)total * 100));
                    SetStatus($"Encoding frame {cur}/{total}");
                };

                await Task.Run(() => encoder.Encode(_framesFolder, outPath), _cts.Token);
                Log($"MJPEG saved: {outPath}", LogColor.Green);
            }

            SetStatus("Encode complete.");
            Log("Encode complete.", LogColor.Green);
            btnPlay.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            Log("Encode cancelled.", LogColor.Muted);
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}", LogColor.Red);
        }
        finally
        {
            SetBusy(false);
            SetProgress(0);
        }
    }

    #endregion

    #region Decode

    /// <summary>
    /// Декодирование
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private async void BtnDecode_Click(object? sender, EventArgs e)
    {
        SetBusy(true);
        _cts = new CancellationTokenSource();

        try
        {
            //Audio: ADPCM → PCM
            if (_wavPath is not null)
            {
                Log("Decoding ADPCM → PCM...", LogColor.Purple);
                await Task.Run(() =>
                {
                    var adpcmPath = Path.ChangeExtension(_wavPath, ".adpcm");
                    if (!File.Exists(adpcmPath))
                    {
                        Log("ADPCM file not found. Encode first.", LogColor.Amber);
                        return;
                    }

                    var adpcm = File.ReadAllBytes(adpcmPath);
                    var wav = WavReader.Load(_wavPath);
                    var decoder = new ImaAdpcmDecoder();
                    var pcm = decoder.Decode(adpcm, wav.Channels);

                    var outPath = Path.ChangeExtension(_wavPath, ".decoded.wav");
                    WavReader.Save(outPath, pcm, wav.SampleRate, wav.Channels);
                    Log($"PCM saved: {Path.GetFileName(outPath)}", LogColor.Purple);
                }, _cts.Token);
            }

            //Video: MJPEG → frames preview
            if (_framesFolder is not null)
            {
                Log("Decoding MJPEG frames...", LogColor.Purple);

                var mjpegPath = Path.Combine(_framesFolder, "output.mjpeg");
                if (!File.Exists(mjpegPath))
                {
                    Log("MJPEG file not found. Encode first.", LogColor.Amber);
                    SetBusy(false);
                    return;
                }

                var decoder = new MjpegDecoder();
                int i = 0;

                await Task.Run(() =>
                {
                    foreach (var frame in decoder.DecodeFrames(mjpegPath))
                    {
                        i++;
                        pictureBoxFrame.InvokeIfRequired(() =>
                        {
                            pictureBoxFrame.Image?.Dispose();
                            pictureBoxFrame.Image = (Bitmap)frame.Clone();
                        });
                        SetStatus($"Decoded frame {i}");
                    }
                }, _cts.Token);

                Log($"Decoded {i} frames.", LogColor.Purple);
            }

            SetStatus("Decode complete.");
        }
        catch (OperationCanceledException)
        {
            Log("Decode cancelled.", LogColor.Muted);
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}", LogColor.Red);
        }
        finally
        {
            SetBusy(false);
            SetProgress(0);
        }
    }

    #endregion

    #region Playback

    /// <summary>
    /// Воспроизведение
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void BtnPlay_Click(object? sender, EventArgs e)
    {
        // TODO: реализовать воспроизведение декодированных файлов WAV и MJPEG (с синхронизацией аудио и видео)
        Log("Playback not yet implemented.", LogColor.Amber);
        btnPlay.Enabled = false;
        btnStop.Enabled = true;
    }

    /// <summary>
    /// Остановка
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        btnPlay.Enabled = true;
        btnStop.Enabled = false;
        Log("Stopped.", LogColor.Muted);
    }

    #endregion

    #region UI helpers

    /// <summary>
    /// Цвета логов
    /// </summary>
    private static class LogColor
    {
        public static readonly Color Blue = Color.FromArgb(79, 142, 247);
        public static readonly Color Green = Color.FromArgb(62, 207, 122);
        public static readonly Color Amber = Color.FromArgb(245, 166, 35);
        public static readonly Color Purple = Color.FromArgb(167, 139, 250);
        public static readonly Color Red = Color.FromArgb(240, 80, 80);
        public static readonly Color Muted = Color.FromArgb(107, 107, 128);
        public static readonly Color Text = Color.FromArgb(160, 160, 200);
    }

    /// <summary>
    /// Вывод в лог
    /// </summary>
    /// <param name="message">Сообщение</param>
    /// <param name="color">Цвет</param>
    private void Log(string message, Color? color = null)
    {
        var c = color ?? LogColor.Text;
        rtbLog.InvokeIfRequired(() =>
        {
            rtbLog.SelectionColor = LogColor.Muted;
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
            rtbLog.SelectionColor = c;
            rtbLog.AppendText(message + "\n");
            rtbLog.ScrollToCaret();
        });
    }

    /// <summary>
    /// Установка статуса
    /// </summary>
    /// <param name="text">Текст</param>
    private void SetStatus(string text) =>
        lblStatus.InvokeIfRequired(() => lblStatus.Text = text);

    private void SetProgress(int value) =>
        progressBar.InvokeIfRequired(() => progressBar.Value = Math.Clamp(value, 0, 100));

    private void SetBusy(bool busy)
    {
        this.InvokeIfRequired(() =>
        {
            btnLoadWav.Enabled = !busy;
            btnLoadFrames.Enabled = !busy;
            btnGenFrames.Enabled = !busy;
            btnEncode.Enabled = !busy;
            btnDecode.Enabled = !busy;
            btnStop.Enabled = busy;
        });
    }

    #endregion

    #region Designer helpers

    /// <summary>
    /// Настройка кнопки
    /// </summary>
    /// <param name="btn">Кнопка</param>
    /// <param name="text">Текст</param>
    /// <param name="x">Координата X</param>
    /// <param name="width">Ширина</param>
    /// <param name="accent">Цвет акцента</param>
    private static void StyleBtn(Button btn, string text, int x, int width, Color accent)
    {
        btn.Text = text;
        btn.Location = new Point(x, 9);
        btn.Size = new Size(width, 34);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = Color.FromArgb(45, 45, 65);
        btn.FlatAppearance.BorderSize = 1;
        btn.BackColor = Color.FromArgb(28, 28, 42);
        btn.ForeColor = accent;
        btn.Font = new Font("Segoe UI", 8.5f);
        btn.Cursor = Cursors.Hand;
    }

    #endregion
}