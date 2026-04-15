namespace MediaCodec;

partial class Form1
{
    #region Fields

    private System.ComponentModel.IContainer components = null; // переменная контейнера компонентов
    private Panel pnlToolbar; // панель инструментов

    private Button btnLoadWav; // кнопка загрузки WAV
    private Button btnLoadFrames; // кнопка загрузки кадров
    private Button btnGenFrames; // кнопка генерации кадров
    private Button btnEncode; // кнопка кодирования
    private Button btnDecode; // кнопка декодирования
    private Button btnPlay; // кнопка воспроизведения
    private Button btnStop; // кнопка остановки

    private SplitContainer splitMain; // разделитель
    private PictureBox pictureBoxFrame; // картинка
    private RichTextBox rtbLog; // текстовый блок

    private Panel pnlBottom; // нижняя панель
    private ProgressBar progressBar; // панель прогресса
    private Label lblStatus; // статус
    private Label lblTimer; // таймер

    #endregion

    #region Dispose

    /// <summary>
    /// Освобождение ресурсов
    /// </summary>
    /// <param name="disposing">Признак освобождения управляемых ресурсов</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    #endregion

    #region InitializeComponent

    /// <summary>
    /// Метод инициализации компонентов
    /// </summary>
    private void InitializeComponent()
    {
        pnlToolbar = new Panel();
        btnLoadWav = new Button();
        btnLoadFrames = new Button();
        btnGenFrames = new Button();
        btnEncode = new Button();
        btnDecode = new Button();
        btnPlay = new Button();
        btnStop = new Button();
        splitMain = new SplitContainer();
        pictureBoxFrame = new PictureBox();
        rtbLog = new RichTextBox();
        pnlBottom = new Panel();
        progressBar = new ProgressBar();
        lblStatus = new Label();
        lblTimer = new Label();

        SuspendLayout();

        Text = "MediaCodec";
        Size = new Size(1100, 700);
        MinimumSize = new Size(800, 560);
        BackColor = Color.FromArgb(18, 18, 28);
        ForeColor = Color.FromArgb(220, 220, 240);
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;

        pnlToolbar.Dock = DockStyle.Top;
        pnlToolbar.Height = 52;
        pnlToolbar.BackColor = Color.FromArgb(22, 22, 36);

        StyleBtn(btnLoadWav, "Load WAV", 10, 100, Color.FromArgb(79, 142, 247));
        StyleBtn(btnLoadFrames, "Load Frames", 118, 110, Color.FromArgb(79, 142, 247));
        StyleBtn(btnGenFrames, "Gen Frames", 236, 100, Color.FromArgb(99, 179, 237));
        StyleBtn(btnEncode, "Encode", 344, 80, Color.FromArgb(62, 207, 122));
        StyleBtn(btnDecode, "Decode", 432, 80, Color.FromArgb(62, 207, 122));
        StyleBtn(btnPlay, "▶  Play", 568, 90, Color.FromArgb(245, 166, 35));
        StyleBtn(btnStop, "■  Stop", 666, 90, Color.FromArgb(167, 139, 250));

        btnPlay.Enabled = false;
        btnStop.Enabled = false;

        pnlToolbar.Controls.AddRange(new Control[]
        {
            btnLoadWav, btnLoadFrames, btnGenFrames, btnEncode, btnDecode, btnPlay, btnStop
        });

        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Dock = DockStyle.Fill;
        splitMain.Orientation = Orientation.Vertical;
        splitMain.SplitterWidth = 4;
        splitMain.BackColor = Color.FromArgb(35, 35, 50);

        ((System.ComponentModel.ISupportInitialize)pictureBoxFrame).BeginInit();
        pictureBoxFrame.Dock = DockStyle.Fill;
        pictureBoxFrame.BackColor = Color.Black;
        pictureBoxFrame.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBoxFrame.BorderStyle = BorderStyle.None;
        ((System.ComponentModel.ISupportInitialize)pictureBoxFrame).EndInit();

        rtbLog.Dock = DockStyle.Fill;
        rtbLog.BackColor = Color.FromArgb(14, 14, 22);
        rtbLog.ForeColor = Color.FromArgb(160, 160, 200);
        rtbLog.Font = new Font("Consolas", 8.5f);
        rtbLog.BorderStyle = BorderStyle.None;
        rtbLog.ReadOnly = true;
        rtbLog.Padding = new Padding(6);
        rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;

        splitMain.Panel1.BackColor = Color.Black;
        splitMain.Panel2.BackColor = Color.FromArgb(14, 14, 22);
        splitMain.Panel1.Controls.Add(pictureBoxFrame);
        splitMain.Panel2.Controls.Add(rtbLog);
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();

        pnlBottom.Dock = DockStyle.Bottom;
        pnlBottom.Height = 38;
        pnlBottom.BackColor = Color.FromArgb(22, 22, 36);

        progressBar.Location = new Point(10, 10);
        progressBar.Size = new Size(540, 18);
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.BackColor = Color.FromArgb(32, 32, 48);
        progressBar.ForeColor = Color.FromArgb(62, 207, 122);

        lblStatus.Location = new Point(562, 12);
        lblStatus.AutoSize = true;
        lblStatus.Text = "Ready";
        lblStatus.ForeColor = Color.FromArgb(107, 107, 128);
        lblStatus.Font = new Font("Consolas", 8.5f);

        lblTimer.Location = new Point(760, 12);
        lblTimer.AutoSize = true;
        lblTimer.Text = "00:00 / 00:00";
        lblTimer.ForeColor = Color.FromArgb(107, 107, 128);
        lblTimer.Font = new Font("Consolas", 8.5f);

        pnlBottom.Controls.AddRange(new Control[] { progressBar, lblStatus, lblTimer });

        Controls.Add(splitMain);
        Controls.Add(pnlBottom);
        Controls.Add(pnlToolbar);

        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}