namespace MediaCodec
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private Panel pnlToolbar;
        private FlowLayoutPanel flowButtons;
        private Button btnLoadWav;
        private Button btnLoadFrames;
        private Label lblSep1;
        private Button btnEncode;
        private Button btnDecode;
        private Label lblSep2;
        private Button btnPlay;
        private Button btnStop;
        private SplitContainer splitMain;
        private PictureBox pictureBoxFrame;
        private Panel pnlLog;
        private Label lblLogHeader;
        private RichTextBox rtbLog;
        private Panel pnlBottom;
        private ProgressBar progressBar;
        private Label lblStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pnlToolbar = new Panel();
            flowButtons = new FlowLayoutPanel();
            btnLoadWav = new Button();
            btnLoadFrames = new Button();
            lblSep1 = new Label();
            btnEncode = new Button();
            btnDecode = new Button();
            lblSep2 = new Label();
            btnPlay = new Button();
            btnStop = new Button();
            splitMain = new SplitContainer();
            pictureBoxFrame = new PictureBox();
            pnlLog = new Panel();
            lblLogHeader = new Label();
            rtbLog = new RichTextBox();
            pnlBottom = new Panel();
            progressBar = new ProgressBar();
            lblStatus = new Label();

            SuspendLayout();

            // ── Form ──────────────────────────────────────────────────────────
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "MediaCodec";
            Size = new Size(1100, 700);
            MinimumSize = new Size(800, 560);
            BackColor = Color.FromArgb(24, 24, 36);
            StartPosition = FormStartPosition.CenterScreen;

            // ── Toolbar ───────────────────────────────────────────────────────
            pnlToolbar.Dock = DockStyle.Top;
            pnlToolbar.Height = 58;
            pnlToolbar.BackColor = Color.FromArgb(16, 16, 26);
            pnlToolbar.Padding = new Padding(8, 10, 8, 8);

            flowButtons.Dock = DockStyle.Fill;
            flowButtons.FlowDirection = FlowDirection.LeftToRight;
            flowButtons.WrapContents = false;
            flowButtons.BackColor = Color.Transparent;
            flowButtons.Padding = new Padding(0);

            // Кнопки — группа 1: Load
            Btn(btnLoadWav, "⬆  Load WAV", Color.FromArgb(58, 130, 246));
            Btn(btnLoadFrames, "⬆  Load Frames", Color.FromArgb(58, 130, 246));
            btnLoadFrames.Enabled = false;

            // Разделитель
            Sep(lblSep1);

            // Группа 2: Encode/Decode
            Btn(btnEncode, "⚙  Encode", Color.FromArgb(34, 197, 94));
            Btn(btnDecode, "⚙  Decode", Color.FromArgb(34, 197, 94));
            btnEncode.Enabled = false;
            btnDecode.Enabled = false;

            Sep(lblSep2);

            // Группа 3: Play/Stop
            Btn(btnPlay, "▶  Play", Color.FromArgb(234, 179, 8));
            Btn(btnStop, "■  Stop", Color.FromArgb(168, 85, 247));
            btnPlay.Enabled = false;
            btnStop.Enabled = false;

            flowButtons.Controls.AddRange(new Control[]
            {
                btnLoadWav, btnLoadFrames, lblSep1,
                btnEncode,  btnDecode,     lblSep2,
                btnPlay,    btnStop
            });

            pnlToolbar.Controls.Add(flowButtons);

            // ── SplitContainer ────────────────────────────────────────────────
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Dock = DockStyle.Fill;
            splitMain.Orientation = Orientation.Vertical;
            
            splitMain.SplitterWidth = 4;
            splitMain.BackColor = Color.FromArgb(40, 40, 55);
            splitMain.Panel1MinSize = 0;
            splitMain.Panel2MinSize = 0;

            // PictureBox
            ((System.ComponentModel.ISupportInitialize)pictureBoxFrame).BeginInit();
            pictureBoxFrame.Dock = DockStyle.Fill;
            pictureBoxFrame.BackColor = Color.FromArgb(8, 8, 14);
            pictureBoxFrame.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxFrame.BorderStyle = BorderStyle.None;
            ((System.ComponentModel.ISupportInitialize)pictureBoxFrame).EndInit();
            splitMain.Panel1.Controls.Add(pictureBoxFrame);
            splitMain.Panel1.BackColor = Color.FromArgb(8, 8, 14);

            // Log panel
            pnlLog.Dock = DockStyle.Fill;
            pnlLog.BackColor = Color.FromArgb(14, 14, 22);

            lblLogHeader.Dock = DockStyle.Top;
            lblLogHeader.Height = 28;
            lblLogHeader.Text = "  OUTPUT";
            lblLogHeader.Font = new Font("Consolas", 8f, FontStyle.Bold);
            lblLogHeader.ForeColor = Color.FromArgb(80, 80, 110);
            lblLogHeader.BackColor = Color.FromArgb(12, 12, 20);

            rtbLog.Dock = DockStyle.Fill;
            rtbLog.BackColor = Color.FromArgb(14, 14, 22);
            rtbLog.ForeColor = Color.FromArgb(180, 180, 220);
            rtbLog.Font = new Font("Consolas", 9f);
            rtbLog.BorderStyle = BorderStyle.None;
            rtbLog.ReadOnly = true;
            rtbLog.Padding = new Padding(8);
            rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;

            pnlLog.Controls.Add(rtbLog);
            pnlLog.Controls.Add(lblLogHeader);
            splitMain.Panel2.Controls.Add(pnlLog);
            splitMain.Panel2.BackColor = Color.FromArgb(14, 14, 22);

            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();

            // ── Bottom ────────────────────────────────────────────────────────
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 36;
            pnlBottom.BackColor = Color.FromArgb(12, 12, 20);
            pnlBottom.Padding = new Padding(8, 8, 8, 0);

            progressBar.Dock = DockStyle.Left;
            progressBar.Width = 480;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.BackColor = Color.FromArgb(28, 28, 42);
            progressBar.ForeColor = Color.FromArgb(34, 197, 94);

            lblStatus.Dock = DockStyle.Fill;
            lblStatus.Text = "Ready";
            lblStatus.ForeColor = Color.FromArgb(110, 110, 145);
            lblStatus.Font = new Font("Consolas", 9f);
            lblStatus.Padding = new Padding(12, 0, 0, 0);
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(progressBar);

            // ── Assemble ──────────────────────────────────────────────────────
            Controls.Add(splitMain);
            Controls.Add(pnlBottom);
            Controls.Add(pnlToolbar);

            ResumeLayout(false);
        }

        private static void Btn(Button btn, string text, Color color)
        {
            btn.Text = text;
            btn.Height = 36;
            btn.AutoSize = false;
            btn.Width = TextRenderer.MeasureText(text, new Font("Segoe UI", 9f)).Width + 28;
            btn.Margin = new Padding(0, 0, 6, 0);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = color;
            btn.BackColor = Color.FromArgb(
                (int)(color.R * 0.15f + 16),
                (int)(color.G * 0.15f + 16),
                (int)(color.B * 0.15f + 20));
            btn.ForeColor = color;
            btn.Font = new Font("Segoe UI", 9f);
            btn.Cursor = Cursors.Hand;
        }

        private static void Sep(Label lbl)
        {
            lbl.Text = "";
            lbl.Width = 1;
            lbl.Height = 36;
            lbl.BackColor = Color.FromArgb(50, 50, 70);
            lbl.Margin = new Padding(4, 0, 10, 0);
        }
    }
}