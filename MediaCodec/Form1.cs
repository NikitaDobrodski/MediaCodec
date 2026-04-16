using MediaCodec.Audio;
using MediaCodec.Video;

namespace MediaCodec;

public partial class Form1 : Form
{
    #region Fields

    private string? _wavPath;
    private string? _framesFolder;
    private CancellationTokenSource? _cts;
    private MjpegPlayer? _player;

    // Пути к файлам результата (выбираются пользователем при кодировании/декодировании)
    private string? _adpcmOutPath;
    private string? _mjpegOutPath;
    private string? _decodedWavOutPath;

    #endregion

    #region Constructor

    /// <summary>
    /// Конструктор
    /// </summary>
    public Form1()
    {
        InitializeComponent();
        WireEvents();
        Log("MediaCodec ready.", LogColor.Muted);
    }

    #endregion

    #region Event wiring

    /// <summary>
    /// Подключение событий
    /// </summary>
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
    /// Первое появление формы
    /// </summary>
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
    /// Выбор WAV-файла
    /// </summary>
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
    /// Выбор папки с кадрами
    /// </summary>
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
    /// Генерация тестовых кадров
    /// </summary>
    private async void BtnGenFrames_Click(object? sender, EventArgs e)
    {
        // Спрашиваем количество кадров
        int count = AskFrameCount(defaultValue: 250);
        if (count <= 0) return;

        using var dlg = new FolderBrowserDialog
        {
            Description = "Выберите папку для сохранения кадров",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _framesFolder = dlg.SelectedPath;
        var folder = _framesFolder;

        SetBusy(true);
        try
        {
            Log($"Генерация {count} тестовых кадров (320×240)...", LogColor.Muted);
            await Task.Run(() => TestFrameGenerator.Generate(folder, count: count, width: 320, height: 240));

            int saved = Directory.GetFiles(folder, "*.png").Length;
            Log($"Сгенерировано {saved} кадров → {Path.GetFileName(folder)}", LogColor.Green);
            SetStatus($"Кадры: {saved} файлов");
        }
        catch (Exception ex)
        {
            Log($"Ошибка: {ex.Message}", LogColor.Red);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Показывает простой диалог ввода числа (без внешних зависимостей)
    /// Возвращает введённое значение или 0 при отмене
    /// </summary>
    private static int AskFrameCount(int defaultValue)
    {
        using var form = new Form
        {
            Text = "Количество кадров",
            Size = new Size(300, 130),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(22, 22, 36),
            ForeColor = Color.FromArgb(200, 200, 220)
        };

        var label = new Label
        {
            Text = "Введите количество кадров:",
            Location = new Point(12, 12),
            AutoSize = true,
            ForeColor = Color.FromArgb(200, 200, 220)
        };

        var numericUpDown = new NumericUpDown
        {
            Location = new Point(12, 36),
            Width = 260,
            Minimum = 1,
            Maximum = 10000,
            Value = defaultValue,
            BackColor = Color.FromArgb(30, 30, 46),
            ForeColor = Color.FromArgb(200, 200, 220)
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(116, 64),
            Width = 80,
            BackColor = Color.FromArgb(62, 207, 122),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat
        };
        btnOk.FlatAppearance.BorderSize = 0;

        form.AcceptButton = btnOk;
        form.Controls.AddRange(new Control[] { label, numericUpDown, btnOk });

        return form.ShowDialog() == DialogResult.OK ? (int)numericUpDown.Value : 0;
    }

    #endregion

    #region Encode

    /// <summary>
    /// Кодирование
    /// </summary>
    /// <param name="sender">Объект, вызвавший событие</param>
    /// <param name="e">Аргументы события</param>
    private async void BtnEncode_Click(object? sender, EventArgs e)
    {
        // Останавливаем плеер — иначе MCI держит хэндл на decoded.wav
        _player?.Stop();
        _player = null;

        if (_wavPath is null && _framesFolder is null)
        {
            Log("Nothing to encode. Load WAV and/or Frames first.", LogColor.Amber);
            return;
        }

        // Выбор папки для сохранения (один диалог для всего)
        using var folderDlg = new FolderBrowserDialog
        {
            Description = "Выберите папку для сохранения файлов",
            UseDescriptionForTitle = true,
            InitialDirectory = _framesFolder ?? Path.GetDirectoryName(_wavPath)
        };
        if (folderDlg.ShowDialog() != DialogResult.OK)
        {
            Log("Кодирование отменено.", LogColor.Muted);
            return;
        }

        var outDir = folderDlg.SelectedPath;

        string? adpcmPath = _wavPath is not null
            ? Path.Combine(outDir, Path.GetFileNameWithoutExtension(_wavPath) + ".adpcm")
            : null;

        string? mjpegPath = _framesFolder is not null
            ? Path.Combine(outDir, "output.mjpeg")
            : null;

        SetBusy(true);
        _cts = new CancellationTokenSource();

        try
        {
            // Audio: WAV → ADPCM
            if (_wavPath is not null && adpcmPath is not null)
            {
                Log("Starting IMA ADPCM encode...", LogColor.Green);
                await Task.Run(() =>
                {
                    var wav = WavReader.Load(_wavPath);
                    var encoder = new ImaAdpcmEncoder();
                    var adpcm = encoder.Encode(wav.Samples, wav.Channels);

                    File.WriteAllBytes(adpcmPath, adpcm);
                    Log($"ADPCM saved: {Path.GetFileName(adpcmPath)}", LogColor.Green);
                }, _cts.Token);

                _adpcmOutPath = adpcmPath;
            }

            // Video: PNG frames → MJPEG
            if (_framesFolder is not null && mjpegPath is not null)
            {
                Log("Starting MJPEG encode...", LogColor.Green);

                var encoder = new MjpegEncoder();
                encoder.ProgressChanged += (cur, total) =>
                {
                    SetProgress((int)(cur / (double)total * 100));
                    SetStatus($"Encoding frame {cur}/{total}");
                };

                await Task.Run(() => encoder.Encode(_framesFolder, mjpegPath), _cts.Token);
                Log($"MJPEG saved: {Path.GetFileName(mjpegPath)}", LogColor.Green);

                _mjpegOutPath = mjpegPath;
            }

            SetStatus("Encode complete.");
            Log("Encode complete.", LogColor.Green);
            btnPlay.Enabled = _mjpegOutPath is not null;
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
    /// Декодирует ADPCM в WAV
    /// </summary>
    /// <param name="sender">Объект, вызвавший событие</param>
    /// <param name="e">Аргументы события</param>
    private async void BtnDecode_Click(object? sender, EventArgs e)
    {
        // Останавливаем плеер — иначе MCI держит хэндл на decoded.wav и File.Create упадёт с IOException
        _player?.Stop();
        _player = null;

        // Выбор папки для сохранения (один диалог)
        string? decodedWavPath = null;
        if (_wavPath is not null)
        {
            var adpcmSrc = _adpcmOutPath ?? Path.ChangeExtension(_wavPath, ".adpcm");
            if (!File.Exists(adpcmSrc))
            {
                Log("ADPCM файл не найден — аудио декодирование пропущено.", LogColor.Amber);
            }
            else
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description = "Выберите папку для сохранения decoded WAV",
                    UseDescriptionForTitle = true,
                    InitialDirectory = Path.GetDirectoryName(_wavPath)
                };
                if (dlg.ShowDialog() == DialogResult.OK)
                    decodedWavPath = Path.Combine(
                        dlg.SelectedPath,
                        Path.GetFileNameWithoutExtension(_wavPath) + ".decoded.wav");
            }
        }

        if (decodedWavPath is null && _mjpegOutPath is null)
        {
            Log("Нечего декодировать.", LogColor.Amber);
            return;
        }

        SetBusy(true);
        _cts = new CancellationTokenSource();

        try
        {
            // Audio: ADPCM → WAV
            if (_wavPath is not null && decodedWavPath is not null)
            {
                var adpcmSrc = _adpcmOutPath ?? Path.ChangeExtension(_wavPath, ".adpcm");
                Log("Decoding ADPCM → PCM...", LogColor.Purple);
                await Task.Run(() =>
                {
                    var adpcm = File.ReadAllBytes(adpcmSrc);
                    var wav = WavReader.Load(_wavPath);
                    var decoder = new ImaAdpcmDecoder();
                    var pcm = decoder.Decode(adpcm, wav.Channels);

                    WavReader.Save(decodedWavPath, pcm, wav.SampleRate, wav.Channels);
                    Log($"PCM saved: {Path.GetFileName(decodedWavPath)}", LogColor.Purple);
                }, _cts.Token);

                _decodedWavOutPath = decodedWavPath;
            }

            // Video: MJPEG → PNG кадры на диск
            if (_mjpegOutPath is not null)
            {
                Log("Декодирование MJPEG кадров...", LogColor.Purple);

                if (!File.Exists(_mjpegOutPath))
                {
                    Log("MJPEG файл не найден. Сначала выполните кодирование.", LogColor.Amber);
                    SetBusy(false);
                    return;
                }

                // Выбор папки для сохранения PNG кадров
                string? framesOutDir = null;
                this.InvokeIfRequired(() =>
                {
                    using var dlg = new FolderBrowserDialog
                    {
                        Description = "Выберите папку для сохранения декодированных кадров",
                        UseDescriptionForTitle = true,
                        InitialDirectory = Path.GetDirectoryName(_mjpegOutPath)
                    };
                    if (dlg.ShowDialog() == DialogResult.OK)
                        framesOutDir = dlg.SelectedPath;
                });

                if (framesOutDir is null)
                {
                    Log("Декодирование видео отменено.", LogColor.Muted);
                }
                else
                {
                    var mjpegPath = _mjpegOutPath;
                    var decoder = new MjpegDecoder();
                    int i = 0;

                    await Task.Run(() =>
                    {
                        foreach (var frame in decoder.DecodeFrames(mjpegPath))
                        {
                            i++;

                            // Сохраняем кадр как PNG
                            var pngPath = Path.Combine(framesOutDir, $"frame_{i:D4}.png");
                            frame.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);

                            // Показываем последний декодированный кадр в превью
                            pictureBoxFrame.InvokeIfRequired(() =>
                            {
                                pictureBoxFrame.Image?.Dispose();
                                pictureBoxFrame.Image = (Bitmap)frame.Clone();
                            });

                            frame.Dispose();
                            SetStatus($"Декодирован кадр {i}");
                            SetProgress((int)((double)i / MjpegDecoder.ReadFrameCount(mjpegPath) * 100));
                        }
                    }, _cts.Token);

                    Log($"Декодировано {i} кадров → {Path.GetFileName(framesOutDir)}", LogColor.Purple);
                }
            }

            SetStatus("Декодирование завершено.");
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
    /// Запускает воспроизведение
    /// </summary>
    /// <param name="sender">Объект, вызвавший событие</param>
    /// <param name="e">Аргументы события</param>
    private void BtnPlay_Click(object? sender, EventArgs e)
    {
        if (_mjpegOutPath is null || !File.Exists(_mjpegOutPath))
        {
            Log("MJPEG-файл не найден. Сначала выполните кодирование.", LogColor.Amber);
            return;
        }

        var mjpegPath = _mjpegOutPath;

        // Берём decoded.wav из пути, выбранного при декодировании
        string? wavPath = _decodedWavOutPath is not null && File.Exists(_decodedWavOutPath)
            ? _decodedWavOutPath
            : null;

        // Останавливаем предыдущий плеер — иначе MCI-аудио накладывается
        _player?.Stop();
        _player?.Dispose();
        _player = new MjpegPlayer();

        _player.FrameReady += (bmp, cur, total) =>
        {
            pictureBoxFrame.InvokeIfRequired(() =>
            {
                var old = pictureBoxFrame.Image;
                pictureBoxFrame.Image = bmp;
                old?.Dispose();
            });
            SetStatus($"Кадр {cur} / {total}");
        };

        _player.TimeUpdated += (curMs, totalMs) =>
        {
            lblTimer.InvokeIfRequired(() =>
                lblTimer.Text = $"{FormatTime(curMs)} / {FormatTime(totalMs)}");
            if (totalMs > 0)
                SetProgress((int)((double)curMs / totalMs * 100));
        };

        _player.PlaybackStopped += () =>
        {
            this.InvokeIfRequired(() =>
            {
                btnPlay.Enabled = true;
                btnStop.Enabled = false;
                SetProgress(0);
                lblTimer.Text = "00:00 / 00:00";
                SetStatus("Воспроизведение завершено.");
                Log("Воспроизведение завершено.", LogColor.Muted);
            });
        };

        var audioInfo = wavPath is not null
            ? $" + {Path.GetFileName(wavPath)}"
            : " (только видео)";
        Log($"Воспроизведение: {Path.GetFileName(mjpegPath)}{audioInfo}  30 fps", LogColor.Amber);

        btnPlay.Enabled = false;
        btnStop.Enabled = true;

        _player.Play(mjpegPath, wavPath, fps: 30);
    }

    /// <summary>
    /// Останавливает воспроизведение
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _cts?.Cancel();

        _player?.Stop();
        _player = null;

        btnPlay.Enabled = true;
        btnStop.Enabled = false;
        lblTimer.Text = "00:00 / 00:00";
        SetProgress(0);
        Log("Остановлено.", LogColor.Muted);
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
    /// Добавляет строку в rtbLog
    /// </summary>
    /// <param name="message">Строка для добавления в лог</param>
    /// <param name="color">Цвет текста</param>
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
    /// Устанавливает текст в lblStatus
    /// </summary>
    /// <param name="text"></param>
    private void SetStatus(string text) =>
        lblStatus.InvokeIfRequired(() => lblStatus.Text = text);

    /// <summary>
    /// Устанавливает состояние прогресс-бара
    /// </summary>
    /// <param name="value">Число от 0 до 100</param>
    private void SetProgress(int value) =>
        progressBar.InvokeIfRequired(() => progressBar.Value = Math.Clamp(value, 0, 100));

    /// <summary>
    /// Форматирование времени в виде "MM:SS" для отображения в lblTimer
    /// </summary>
    /// <param name="ms">Время в миллисекундах</param>
    /// <returns>Строка в формате "MM:SS"</returns>
    private static string FormatTime(int ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
    }

    /// <summary>
    /// Устанавливает состояние "занятости" панели инструментов: отключает кнопки загрузки/кодирования/декодирования и включает кнопку "Стоп"
    /// </summary>
    /// <param name="busy">Если true, панель инструментов считается занятой</param>
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
    /// Стилизует кнопку в едином стиле для всей панели инструментов
    /// </summary>
    /// <param name="btn">Кнопка для стилизации</param>
    /// <param name="text">Текст кнопки</param>
    /// <param name="x">Позиция по оси X</param>
    /// <param name="width">Ширина кнопки</param>
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