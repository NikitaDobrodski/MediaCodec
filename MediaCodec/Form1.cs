using MediaCodec.Audio;

namespace MediaCodec
{
    public partial class Form1 : Form
    {
        private string? _wavPath;
        private string? _framesFolder;

        public Form1()
        {
            InitializeComponent();
            WireEvents();
            Log("MediaCodec ready. Load a WAV file to start.", Clr.Muted);
        }

        // ── Привязка событий ──────────────────────────────────────────────────

        private void WireEvents()
        {
            btnLoadWav.Click += BtnLoadWav_Click;
            btnLoadFrames.Click += BtnLoadFrames_Click;
            btnEncode.Click += BtnEncode_Click;
            btnDecode.Click += BtnDecode_Click;
            btnPlay.Click += BtnPlay_Click;
            btnStop.Click += BtnStop_Click;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            splitMain.Panel1MinSize = 300;
            splitMain.Panel2MinSize = 220;
            splitMain.SplitterDistance = (int)(ClientSize.Width * 0.63);
        }

        // ── Load ──────────────────────────────────────────────────────────────

        private void BtnLoadWav_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select WAV file",
                Filter = "WAV files (*.wav)|*.wav"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _wavPath = dlg.FileName;
            var info = new FileInfo(_wavPath);
            Log($"WAV loaded: {info.Name}  ({info.Length / 1024} KB)", Clr.Blue);
            SetStatus($"WAV: {info.Name}");

            // Разблокируем следующие кнопки
            btnEncode.Enabled = true;
            btnLoadFrames.Enabled = true;
        }

        private void BtnLoadFrames_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select folder with PNG frames",
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _framesFolder = dlg.SelectedPath;
            int count = Directory.GetFiles(_framesFolder, "*.png").Length;
            Log($"Frames: {Path.GetFileName(_framesFolder)}  ({count} PNG files)", Clr.Blue);
            SetStatus($"Frames: {count} files");
        }

        // ── Encode / Decode ───────────────────────────────────────────────────

        private async void BtnEncode_Click(object? sender, EventArgs e)
        {
            if (_wavPath is null) { Log("Load a WAV file first.", Clr.Amber); return; }

            SetBusy(true);
            try
            {
                var wavPath = _wavPath;
                Log("Reading WAV...", Clr.Muted);

                await Task.Run(() =>
                {
                    // 1. Читаем WAV
                    var wav = WavReader.Load(wavPath);
                    Log($"  Sample rate: {wav.SampleRate} Hz  |  Channels: {wav.Channels}  |  Samples: {wav.Samples.Length}", Clr.Muted);

                    // 2. Кодируем
                    Log("Encoding IMA ADPCM...", Clr.Green);
                    var encoder = new ImaAdpcmEncoder();
                    var adpcm = encoder.Encode(wav.Samples, wav.Channels);

                    // 3. Сохраняем рядом с WAV
                    var outPath = Path.ChangeExtension(wavPath, ".adpcm");
                    File.WriteAllBytes(outPath, adpcm);

                    double ratio = (double)wav.Samples.Length * 2 / adpcm.Length;
                    Log($"Saved: {Path.GetFileName(outPath)}  ({adpcm.Length / 1024} KB)  compression: {ratio:F1}x", Clr.Green);
                });

                SetStatus("Encode done.");
                btnDecode.Enabled = true;
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}", Clr.Red);
                SetStatus("Error.");
            }
            finally { SetBusy(false); }
        }

        private async void BtnDecode_Click(object? sender, EventArgs e)
        {
            if (_wavPath is null) { Log("Load a WAV file first.", Clr.Amber); return; }

            var adpcmPath = Path.ChangeExtension(_wavPath, ".adpcm");
            if (!File.Exists(adpcmPath))
            {
                Log("ADPCM file not found — encode first.", Clr.Amber);
                return;
            }

            SetBusy(true);
            try
            {
                var wavPath = _wavPath;
                var adpcmFile = adpcmPath;

                await Task.Run(() =>
                {
                    Log("Decoding ADPCM → PCM...", Clr.Purple);
                    var adpcm = File.ReadAllBytes(adpcmFile);
                    var srcWav = WavReader.Load(wavPath);

                    var decoder = new ImaAdpcmDecoder();
                    var pcm = decoder.Decode(adpcm, srcWav.Channels);

                    var outPath = Path.ChangeExtension(wavPath, ".decoded.wav");
                    WavReader.Save(outPath, pcm, srcWav.SampleRate, srcWav.Channels);

                    Log($"Saved: {Path.GetFileName(outPath)}  ({new FileInfo(outPath).Length / 1024} KB)", Clr.Purple);
                    Log("Roundtrip complete. Compare original and decoded on hearing.", Clr.Green);
                });

                SetStatus("Decode done.");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}", Clr.Red);
                SetStatus("Error.");
            }
            finally { SetBusy(false); }
        }

        // ── Playback (Part 3 — заглушки) ──────────────────────────────────────

        private void BtnPlay_Click(object? sender, EventArgs e)
        {
            Log("Playback — not yet implemented (Part 3).", Clr.Amber);
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            Log("Stopped.", Clr.Muted);
            btnPlay.Enabled = true;
            btnStop.Enabled = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static class Clr
        {
            public static readonly Color Blue = Color.FromArgb(79, 142, 247);
            public static readonly Color Green = Color.FromArgb(62, 207, 122);
            public static readonly Color Amber = Color.FromArgb(245, 166, 35);
            public static readonly Color Purple = Color.FromArgb(167, 139, 250);
            public static readonly Color Red = Color.FromArgb(240, 80, 80);
            public static readonly Color Muted = Color.FromArgb(107, 107, 128);
        }

        private void Log(string message, Color? color = null)
        {
            var c = color ?? Color.FromArgb(160, 160, 200);
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(() => AppendLog(message, c));
            }
            else
            {
                AppendLog(message, c);
            }
        }

        private void AppendLog(string message, Color color)
        {
            rtbLog.SelectionColor = Color.FromArgb(70, 70, 90);
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(message + "\n");
            rtbLog.ScrollToCaret();
        }

        private void SetStatus(string text)
        {
            if (lblStatus.InvokeRequired) lblStatus.Invoke(() => lblStatus.Text = text);
            else lblStatus.Text = text;
        }

        private void SetProgress(int value)
        {
            if (progressBar.InvokeRequired) progressBar.Invoke(() => progressBar.Value = Math.Clamp(value, 0, 100));
            else progressBar.Value = Math.Clamp(value, 0, 100);
        }

        private void SetBusy(bool busy)
        {
            if (InvokeRequired) { Invoke(() => SetBusy(busy)); return; }
            btnLoadWav.Enabled = !busy;
            btnLoadFrames.Enabled = !busy && _wavPath is not null;
            btnEncode.Enabled = !busy && _wavPath is not null;
            btnDecode.Enabled = !busy;
            btnStop.Enabled = busy;
        }
    }
}
