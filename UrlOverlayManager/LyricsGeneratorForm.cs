using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class LyricsGeneratorForm : Form
    {
        private const int SideButtonHeight = 28;
        private const int SideButtonRowHeight = 34;
        private const int SideLabelRowHeight = 20;
        private const int SideInnerGap = 6;

        private readonly TextBox txtLyrics = new TextBox();
        private readonly Label lblHeaderTitle = new Label();
        private readonly Label lblFileName = new Label();
        private readonly Label lblPageStatus = new Label();
        private readonly Button btnLoadLyrics = new Button();
        private readonly Button btnSaveLyrics = new Button();
        private readonly Button btnSplitOneLine = new Button();
        private readonly Button btnSplitTwoLines = new Button();
        private readonly Button btnSplitThreeLines = new Button();
        private readonly Button btnShowOverlay = new Button();
        private readonly Button btnHideOverlay = new Button();
        private readonly Button btnPrevPage = new Button();
        private readonly Button btnNextPage = new Button();
        private readonly Button btnResetPage = new Button();
        private readonly CheckBox chkOverlayClickThrough = new CheckBox();
        private readonly TextBox txtLyricsSearch = new TextBox();
        private readonly Label lblLyricsSearchFolder = new Label();
        private readonly ListBox lstLyricsFiles = new ListBox();
        private readonly Button btnSelectLyricsFolder = new Button();
        private readonly Button btnRefreshLyricsFiles = new Button();

        private static LyricsOverlayForm? sharedOverlayForm;
        private LyricsOverlayForm? overlayForm;
        private string currentFilePath = "";
        private string lyricsSearchDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private string lyricFontFamily = UiTheme.AppFontFamilyName;
        private int currentPageIndex;
        private readonly UiSettings settings;

        private static Color AppBackColor => UiTheme.AppBackColor;
        private static Color PanelBackColor => UiTheme.PanelBackColor;
        private static Color TextColor => UiTheme.TextColor;
        private static Color MutedTextColor => UiTheme.MutedTextColor;
        private static Color AccentColor => UiTheme.AccentColor;
        private static Color BorderColor => UiTheme.BorderColor;
        private static Color DangerColor => UiTheme.DangerColor;

        public LyricsGeneratorForm(bool hostedMode = false)
        {
            settings = UiSettingsStore.Load();
            if (Directory.Exists(settings.LyricsSearchDirectory))
            {
                lyricsSearchDirectory = settings.LyricsSearchDirectory;
            }

            InitializeForm();
            BuildLayout();
            UiTheme.ApplyFonts(this);

            if (!hostedMode)
            {
                UiChrome.Apply(this, "가사 오버레이", true);
            }

            UpdateView();
            EnsureOverlayForm();
            overlayForm?.Show();
        }

        public void ApplyCurrentUiSettings()
        {
            UiTheme.ApplyFonts(this);
            lyricFontFamily = UiTheme.AppFontFamilyName;
            ApplyLyricFont();
        }

        private void InitializeForm()
        {
            Text = "가사 오버레이";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1120, 640);
            MinimumSize = new Size(920, 520);
            BackColor = AppBackColor;
            ForeColor = TextColor;
            Font = UiTheme.RegularFont();
            KeyPreview = true;
            KeyDown += LyricsGeneratorForm_KeyDown;
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 2;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 314));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel fileInfo = new Panel();
            fileInfo.Dock = DockStyle.Fill;

            lblHeaderTitle.Text = "새 가사";
            lblHeaderTitle.Font = UiTheme.BoldFont(15F);
            lblHeaderTitle.ForeColor = TextColor;
            lblHeaderTitle.Location = new Point(0, 0);
            lblHeaderTitle.Size = new Size(420, 26);
            lblHeaderTitle.AutoEllipsis = true;

            lblFileName.Text = "아직 저장되지 않음";
            lblFileName.ForeColor = MutedTextColor;
            lblFileName.Location = new Point(2, 29);
            lblFileName.Size = new Size(420, 20);
            lblFileName.AutoEllipsis = true;

            TableLayoutPanel headerActions = new TableLayoutPanel();
            headerActions.Dock = DockStyle.Fill;
            headerActions.ColumnCount = 5;
            headerActions.RowCount = 1;
            headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            headerActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            headerActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            headerActions.Padding = new Padding(0, 2, 0, 0);
            headerActions.Margin = Padding.Empty;

            fileInfo.Controls.Add(lblHeaderTitle);
            fileInfo.Controls.Add(lblFileName);

            headerActions.Controls.Add(btnSplitOneLine, 1, 0);
            headerActions.Controls.Add(btnSplitTwoLines, 2, 0);
            headerActions.Controls.Add(btnSplitThreeLines, 3, 0);
            headerActions.Controls.Add(btnSaveLyrics, 4, 0);

            header.Controls.Add(fileInfo, 0, 0);
            header.Controls.Add(headerActions, 1, 0);

            ConfigureButton(btnLoadLyrics, "불러오기", AccentColor);
            UiTheme.StyleToolbarButton(btnSaveLyrics, "저장", AccentColor, Color.White, 58);
            UiTheme.StyleToolbarButton(btnSplitOneLine, "1줄", PanelBackColor, TextColor, 58);
            UiTheme.StyleToolbarButton(btnSplitTwoLines, "2줄", PanelBackColor, TextColor, 58);
            UiTheme.StyleToolbarButton(btnSplitThreeLines, "3줄", PanelBackColor, TextColor, 58);
            ConfigureButton(btnShowOverlay, "오버레이 열기", AccentColor);
            ConfigureButton(btnHideOverlay, "가사 비우기", DangerColor);

            btnLoadLyrics.Click += LoadLyricsButton_Click;
            btnSaveLyrics.Click += SaveLyricsButton_Click;
            btnSplitOneLine.Click += SplitOneLineButton_Click;
            btnSplitTwoLines.Click += SplitTwoLinesButton_Click;
            btnSplitThreeLines.Click += SplitThreeLinesButton_Click;
            btnShowOverlay.Click += ShowOverlayButton_Click;
            btnHideOverlay.Click += HideOverlayButton_Click;

            chkOverlayClickThrough.Text = "클릭 무시";
            chkOverlayClickThrough.Dock = DockStyle.Fill;
            chkOverlayClickThrough.Margin = new Padding(0, 2, 0, 4);
            chkOverlayClickThrough.ForeColor = TextColor;
            chkOverlayClickThrough.BackColor = PanelBackColor;
            chkOverlayClickThrough.Checked = settings.LyricsOverlayClickThrough;
            chkOverlayClickThrough.CheckedChanged += OverlayClickThrough_CheckedChanged;

            txtLyrics.Dock = DockStyle.Fill;
            txtLyrics.Multiline = true;
            txtLyrics.ScrollBars = ScrollBars.Vertical;
            txtLyrics.AcceptsReturn = true;
            txtLyrics.AcceptsTab = true;
            txtLyrics.BorderStyle = BorderStyle.None;
            txtLyrics.Margin = new Padding(8);
            txtLyrics.BackColor = PanelBackColor;
            txtLyrics.ForeColor = TextColor;
            txtLyrics.Font = UiTheme.RegularFont(11F);
            txtLyrics.PlaceholderText = "가사를 붙여넣으세요. 빈 줄 하나 이상이 다음 페이지 기준입니다.";
            txtLyrics.TextChanged += (s, e) => UpdateView();

            ConfigureButton(btnNextPage, "다음", AccentColor);
            ConfigureButton(btnPrevPage, "이전", PanelBackColor, TextColor);
            ConfigureButton(btnResetPage, "처음", PanelBackColor, TextColor);

            lblPageStatus.Text = "0 / 0";
            lblPageStatus.AutoSize = false;
            lblPageStatus.Size = new Size(74, 28);
            lblPageStatus.Margin = new Padding(0, 0, 6, 6);
            lblPageStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblPageStatus.ForeColor = MutedTextColor;
            lblPageStatus.Font = UiTheme.BoldFont(10F);

            btnPrevPage.Click += PrevPageButton_Click;
            btnNextPage.Click += NextPageButton_Click;
            btnResetPage.Click += ResetPageButton_Click;

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(CreateLyricsEditorPanel(), 0, 1);

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 480));
            shell.Controls.Add(root, 0, 0);
            shell.Controls.Add(CreateLyricsCommandPanel(), 1, 0);

            Controls.Add(shell);
            RefreshLyricsFileList();
        }

        private void ConfigureButton(Button button, string text, Color backColor)
        {
            ConfigureButton(button, text, backColor, Color.White);
        }

        private Control CreateLyricsEditorPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 6);
            panel.Padding = new Padding(1);
            panel.BackColor = BorderColor;

            txtLyrics.Dock = DockStyle.Fill;
            panel.Controls.Add(txtLyrics);
            return panel;
        }

        private Control CreateLyricsCommandPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(0, 12, 12, 12);
            panel.ColumnCount = 1;
            panel.RowCount = 2;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 198));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.BackColor = AppBackColor;

            PrepareSideButton(btnLoadLyrics);
            PrepareSideButton(btnShowOverlay);
            PrepareSideButton(btnHideOverlay);
            PrepareSideButton(btnPrevPage);
            PrepareSideButton(btnNextPage);
            PrepareSideButton(btnResetPage);

            TableLayoutPanel overlaySection = CreateSideSection("", 6);
            overlaySection.RowStyles[0] = new RowStyle(SizeType.Absolute, 20);
            overlaySection.RowStyles[1] = new RowStyle(SizeType.Absolute, 34);
            overlaySection.RowStyles[2] = new RowStyle(SizeType.Absolute, 32);
            overlaySection.RowStyles[3] = new RowStyle(SizeType.Absolute, 20);
            overlaySection.RowStyles[4] = new RowStyle(SizeType.Absolute, 34);
            overlaySection.RowStyles[5] = new RowStyle(SizeType.Absolute, 26);
            Label overlayLabel = CreateSectionSubLabel("오버레이");
            overlaySection.Controls.Add(overlayLabel, 0, 0);

            TableLayoutPanel overlayButtons = CreateButtonGrid(2);
            overlayButtons.Controls.Add(btnShowOverlay, 0, 0);
            overlayButtons.Controls.Add(btnHideOverlay, 2, 0);
            overlaySection.Controls.Add(overlayButtons, 0, 1);
            overlaySection.Controls.Add(chkOverlayClickThrough, 0, 2);

            Label pageLabel = CreateSectionSubLabel("페이지");
            overlaySection.Controls.Add(pageLabel, 0, 3);

            TableLayoutPanel pageButtons = CreateButtonGrid(3);
            pageButtons.Controls.Add(btnPrevPage, 0, 0);
            pageButtons.Controls.Add(btnNextPage, 2, 0);
            pageButtons.Controls.Add(btnResetPage, 4, 0);
            overlaySection.Controls.Add(pageButtons, 0, 4);

            lblPageStatus.Dock = DockStyle.Fill;
            lblPageStatus.Margin = Padding.Empty;
            overlaySection.Controls.Add(lblPageStatus, 0, 5);

            panel.Controls.Add(overlaySection, 0, 0);
            panel.Controls.Add(CreateLyricsSearchPanel(), 0, 1);
            return panel;
        }

        private Control CreateLyricsSearchPanel()
        {
            TableLayoutPanel panel = CreateSideSection("가사 검색", 3);
            panel.Margin = Padding.Empty;
            panel.Padding = new Padding(10, 8, 10, 6);
            panel.RowStyles[0] = new RowStyle(SizeType.Absolute, 36);
            panel.RowStyles[1] = new RowStyle(SizeType.Absolute, 38);
            panel.RowStyles[2] = new RowStyle(SizeType.Percent, 100);

            Control? title = panel.GetControlFromPosition(0, 0);

            if (title == null)
            {
                title = CreateSectionTitleLabel("가사 검색");
            }
            else
            {
                panel.Controls.Remove(title);
            }
            
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 4;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Margin = Padding.Empty;

            title.Dock = DockStyle.Fill;
            title.Margin = Padding.Empty;

            btnLoadLyrics.Text = "";
            btnLoadLyrics.Dock = DockStyle.Fill;
            btnLoadLyrics.Margin = new Padding(6, 2, 0, 4);
            btnLoadLyrics.Paint -= FileButton_Paint;
            btnLoadLyrics.Paint += FileButton_Paint;

            btnSelectLyricsFolder.Text = "";
            btnSelectLyricsFolder.Dock = DockStyle.Fill;
            btnSelectLyricsFolder.Margin = new Padding(6, 2, 0, 4);
            btnSelectLyricsFolder.FlatStyle = FlatStyle.Flat;
            btnSelectLyricsFolder.FlatAppearance.BorderSize = 1;
            btnSelectLyricsFolder.FlatAppearance.BorderColor = BorderColor;
            btnSelectLyricsFolder.BackColor = PanelBackColor;
            btnSelectLyricsFolder.ForeColor = TextColor;
            btnSelectLyricsFolder.Cursor = Cursors.Hand;
            btnSelectLyricsFolder.Paint -= FolderButton_Paint;
            btnSelectLyricsFolder.Paint += FolderButton_Paint;
            btnSelectLyricsFolder.Click += SelectLyricsFolderButton_Click;

            btnRefreshLyricsFiles.Text = "";
            btnRefreshLyricsFiles.Dock = DockStyle.Fill;
            btnRefreshLyricsFiles.Margin = new Padding(6, 2, 0, 4);
            btnRefreshLyricsFiles.FlatStyle = FlatStyle.Flat;
            btnRefreshLyricsFiles.FlatAppearance.BorderSize = 1;
            btnRefreshLyricsFiles.FlatAppearance.BorderColor = BorderColor;
            btnRefreshLyricsFiles.BackColor = PanelBackColor;
            btnRefreshLyricsFiles.ForeColor = TextColor;
            btnRefreshLyricsFiles.Cursor = Cursors.Hand;
            btnRefreshLyricsFiles.Paint -= RefreshButton_Paint;
            btnRefreshLyricsFiles.Paint += RefreshButton_Paint;
            btnRefreshLyricsFiles.Click += (s, e) => RefreshLyricsFileList();

            txtLyricsSearch.Dock = DockStyle.Fill;
            txtLyricsSearch.Margin = new Padding(0, 2, 0, 4);
            UiTheme.StyleTextBox(txtLyricsSearch);
            txtLyricsSearch.PlaceholderText = "파일명 검색";
            txtLyricsSearch.TextChanged += (s, e) => RefreshLyricsFileList();

            lstLyricsFiles.Dock = DockStyle.Fill;
            lstLyricsFiles.Margin = Padding.Empty;
            UiTheme.StyleListBox(lstLyricsFiles);
            lstLyricsFiles.Font = UiTheme.RegularFont(12F);
            lstLyricsFiles.ItemHeight = 28;
            lstLyricsFiles.IntegralHeight = false;
            lstLyricsFiles.DoubleClick += (s, e) => OpenSelectedLyricsFile();
            lstLyricsFiles.KeyDown += LyricsFilesList_KeyDown;

            header.Controls.Add(title, 0, 0);
            header.Controls.Add(btnLoadLyrics, 1, 0);
            header.Controls.Add(btnSelectLyricsFolder, 2, 0);
            header.Controls.Add(btnRefreshLyricsFiles, 3, 0);
            panel.Controls.Add(header, 0, 0);
            panel.Controls.Add(txtLyricsSearch, 0, 1);
            panel.Controls.Add(lstLyricsFiles, 0, 2);
            return panel;
        }

        private static TableLayoutPanel CreateSideSection(string title, int rowCount)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Fill;
            section.ColumnCount = 1;
            section.RowCount = rowCount;
            section.Padding = new Padding(10, 8, 10, 10);
            section.Margin = new Padding(0, 0, 0, 10);
            section.BackColor = UiTheme.PanelBackColor;
            section.Paint += (s, e) =>
            {
                Rectangle borderBounds = new Rectangle(0, 0, section.ClientSize.Width - 1, section.ClientSize.Height - 1);
                ControlPaint.DrawBorder(e.Graphics, borderBounds, UiTheme.BorderColor, ButtonBorderStyle.Solid);
            };

            bool hasTitle = !string.IsNullOrWhiteSpace(title);

            for (int i = 0; i < rowCount; i++)
            {
                if (hasTitle && i == 0)
                {
                    section.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                }
                else if (!hasTitle && (i == 0 || i == 3))
                {
                    section.RowStyles.Add(new RowStyle(SizeType.Absolute, SideLabelRowHeight));
                }
                else
                {
                    section.RowStyles.Add(new RowStyle(SizeType.Absolute, SideButtonRowHeight));
                }
            }

            if (hasTitle)
            {
                section.Controls.Add(CreateSectionTitleLabel(title), 0, 0);
            }

            return section;
        }

        private static Label CreateSectionTitleLabel(string title)
        {
            Label titleLabel = new Label();
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Text = title;
            titleLabel.ForeColor = UiTheme.TextColor;
            titleLabel.Font = UiTheme.BoldFont(10F);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            return titleLabel;
        }

        private static Label CreateSectionSubLabel(string title)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.Text = title;
            label.ForeColor = UiTheme.MutedTextColor;
            label.Font = UiTheme.BoldFont(9F);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, 4, 0, 0);
            return label;
        }

        private static TableLayoutPanel CreateButtonGrid(int buttonCount)
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = buttonCount * 2 - 1;
            grid.RowCount = 1;

            for (int i = 0; i < grid.ColumnCount; i++)
            {
                bool gapColumn = i % 2 == 1;
                grid.ColumnStyles.Add(gapColumn
                    ? new ColumnStyle(SizeType.Absolute, SideInnerGap)
                    : new ColumnStyle(SizeType.Percent, 100F / buttonCount));
            }

            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.Margin = Padding.Empty;
            return grid;
        }

        private static void PrepareSideButton(Button button)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 1, 0, 5);
            button.Height = SideButtonHeight;
        }

        private static void FolderButton_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button)
                return;

            e.Graphics.Clear(button.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            Rectangle icon = new Rectangle(
                (button.Width - 18) / 2,
                (button.Height - 14) / 2 + 1,
                18,
                13);

            using SolidBrush fillBrush = new SolidBrush(UiTheme.FieldBackColor);
            using Pen borderPen = new Pen(UiTheme.TextColor);

            Rectangle borderBounds = new Rectangle(0, 0, button.ClientSize.Width - 1, button.ClientSize.Height - 1);
            ControlPaint.DrawBorder(e.Graphics, borderBounds, UiTheme.BorderColor, ButtonBorderStyle.Solid);

            Point[] tab =
            {
                new Point(icon.Left, icon.Top + 4),
                new Point(icon.Left + 6, icon.Top + 4),
                new Point(icon.Left + 8, icon.Top),
                new Point(icon.Left + 14, icon.Top),
                new Point(icon.Left + 16, icon.Top + 4),
                new Point(icon.Right, icon.Top + 4),
                new Point(icon.Right, icon.Bottom),
                new Point(icon.Left, icon.Bottom)
            };

            e.Graphics.FillPolygon(fillBrush, tab);
            e.Graphics.DrawPolygon(borderPen, tab);
        }

        private static void FileButton_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button)
                return;

            e.Graphics.Clear(button.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle borderBounds = new Rectangle(0, 0, button.ClientSize.Width - 1, button.ClientSize.Height - 1);
            ControlPaint.DrawBorder(e.Graphics, borderBounds, UiTheme.BorderColor, ButtonBorderStyle.Solid);

            Rectangle page = new Rectangle(
                (button.Width - 16) / 2,
                (button.Height - 19) / 2,
                16,
                19);

            Point[] outline =
            {
                new Point(page.Left, page.Top),
                new Point(page.Right - 5, page.Top),
                new Point(page.Right, page.Top + 5),
                new Point(page.Right, page.Bottom),
                new Point(page.Left, page.Bottom)
            };

            using SolidBrush fillBrush = new SolidBrush(UiTheme.FieldBackColor);
            using Pen pen = new Pen(UiTheme.TextColor, 1.4F);
            e.Graphics.FillPolygon(fillBrush, outline);
            e.Graphics.DrawPolygon(pen, outline);
            e.Graphics.DrawLine(pen, page.Right - 5, page.Top, page.Right - 5, page.Top + 5);
            e.Graphics.DrawLine(pen, page.Right - 5, page.Top + 5, page.Right, page.Top + 5);
            e.Graphics.DrawLine(pen, page.Left + 4, page.Top + 10, page.Right - 4, page.Top + 10);
            e.Graphics.DrawLine(pen, page.Left + 4, page.Top + 14, page.Right - 4, page.Top + 14);
        }

        private static void RefreshButton_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button button)
                return;

            e.Graphics.Clear(button.BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle borderBounds = new Rectangle(0, 0, button.ClientSize.Width - 1, button.ClientSize.Height - 1);
            ControlPaint.DrawBorder(e.Graphics, borderBounds, UiTheme.BorderColor, ButtonBorderStyle.Solid);

            Rectangle arc = new Rectangle(
                (button.Width - 17) / 2,
                (button.Height - 17) / 2,
                17,
                17);

            using Pen pen = new Pen(UiTheme.TextColor, 1.7F);
            e.Graphics.DrawArc(pen, arc, 35, 285);

            Point[] arrow =
            {
                new Point(arc.Right - 1, arc.Top + 6),
                new Point(arc.Right - 7, arc.Top + 4),
                new Point(arc.Right - 4, arc.Top + 10)
            };

            using SolidBrush brush = new SolidBrush(UiTheme.TextColor);
            e.Graphics.FillPolygon(brush, arrow);
        }

        private void ConfigureButton(Button button, string text, Color backColor, Color foreColor)
        {
            UiTheme.StyleToolbarButton(button, text, backColor, foreColor, 96);
        }

        private static Control CreateToolbarSpacer()
        {
            Panel spacer = new Panel();
            spacer.Size = new Size(10, 32);
            spacer.Margin = new Padding(0, 0, 8, 8);
            return spacer;
        }

        private static FlowLayoutPanel CreateToolbarRow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
        }

        private void LoadLyricsButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "가사 파일 불러오기";
            dialog.Filter = "가사 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*";

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            LoadLyricsFromFile(dialog.FileName);
        }

        private void SelectLyricsFolderButton_Click(object? sender, EventArgs e)
        {
            using FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "가사 파일 폴더를 선택하세요.";
            dialog.UseDescriptionForTitle = true;

            if (Directory.Exists(lyricsSearchDirectory))
            {
                dialog.SelectedPath = lyricsSearchDirectory;
            }

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            lyricsSearchDirectory = dialog.SelectedPath;
            SaveLyricsSearchDirectory();
            RefreshLyricsFileList();
        }

        private void LyricsFilesList_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            OpenSelectedLyricsFile();
            e.Handled = true;
        }

        private void RefreshLyricsFileList()
        {
            lblLyricsSearchFolder.Text = Directory.Exists(lyricsSearchDirectory)
                ? lyricsSearchDirectory
                : "폴더 없음";

            lstLyricsFiles.BeginUpdate();
            lstLyricsFiles.Items.Clear();

            if (!Directory.Exists(lyricsSearchDirectory))
            {
                lstLyricsFiles.EndUpdate();
                return;
            }

            string keyword = txtLyricsSearch.Text.Trim();
            string[] files = Directory.EnumerateFiles(lyricsSearchDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => IsLyricsFile(path))
                .Where(path => string.IsNullOrWhiteSpace(keyword)
                    || Path.GetFileNameWithoutExtension(path).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Take(300)
                .ToArray();

            foreach (string file in files)
            {
                lstLyricsFiles.Items.Add(new LyricsFileItem(file));
            }

            lstLyricsFiles.EndUpdate();
        }

        private static bool IsLyricsFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".lrc", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenSelectedLyricsFile()
        {
            if (lstLyricsFiles.SelectedItem is not LyricsFileItem selectedItem)
                return;

            LoadLyricsFromFile(selectedItem.FilePath);
        }

        private void LoadLyricsFromFile(string filePath)
        {
            currentFilePath = filePath;
            txtLyrics.Text = File.ReadAllText(filePath, Encoding.UTF8).Trim();
            currentPageIndex = 0;
            UpdateView();
        }

        private void SaveLyricsSearchDirectory()
        {
            UiSettings settings = UiSettingsStore.Load();
            settings.LyricsSearchDirectory = lyricsSearchDirectory;
            UiSettingsStore.Save(settings);
        }

        private void SaveLyricsButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentFilePath))
            {
                if (!EnsureLyricsSearchDirectory())
                    return;

                string? fileName = PromptForNewLyricsFileName();

                if (string.IsNullOrWhiteSpace(fileName))
                    return;

                currentFilePath = Path.Combine(lyricsSearchDirectory, NormalizeLyricsFileName(fileName));

                if (File.Exists(currentFilePath))
                {
                    DialogResult overwriteResult = MessageBox.Show(
                        this,
                        "이미 같은 이름의 가사 파일이 있습니다. 덮어쓰시겠습니까?",
                        "가사 파일 저장",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (overwriteResult != DialogResult.Yes)
                    {
                        currentFilePath = "";
                        return;
                    }
                }
            }

            File.WriteAllText(currentFilePath, txtLyrics.Text.Trim(), new UTF8Encoding(false));
            RefreshLyricsFileList();
            UpdateView();
        }

        private bool EnsureLyricsSearchDirectory()
        {
            if (Directory.Exists(lyricsSearchDirectory))
                return true;

            using FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "가사 파일을 저장할 폴더를 선택하세요.";
            dialog.UseDescriptionForTitle = true;

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;

            lyricsSearchDirectory = dialog.SelectedPath;
            SaveLyricsSearchDirectory();
            RefreshLyricsFileList();
            return true;
        }

        private string? PromptForNewLyricsFileName()
        {
            using Form dialog = new Form();
            dialog.Text = "가사 파일 이름";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.ClientSize = new Size(360, 126);
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.ShowInTaskbar = false;
            dialog.Font = UiTheme.RegularFont();
            dialog.BackColor = AppBackColor;
            dialog.ForeColor = TextColor;

            Label label = new Label();
            label.Text = "저장할 가사 파일 이름";
            label.Location = new Point(14, 14);
            label.Size = new Size(330, 22);
            label.ForeColor = TextColor;

            TextBox input = new TextBox();
            input.Location = new Point(14, 42);
            input.Size = new Size(332, 26);
            UiTheme.StyleTextBox(input);

            Button okButton = new Button();
            okButton.Text = "저장";
            okButton.Location = new Point(174, 84);
            okButton.Size = new Size(82, 28);
            UiTheme.StyleToolbarButton(okButton, "저장", AccentColor, Color.White, 82);
            okButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "취소";
            cancelButton.Location = new Point(264, 84);
            cancelButton.Size = new Size(82, 28);
            UiTheme.StyleToolbarButton(cancelButton, "취소", PanelBackColor, TextColor, 82);
            cancelButton.DialogResult = DialogResult.Cancel;

            dialog.Controls.Add(label);
            dialog.Controls.Add(input);
            dialog.Controls.Add(okButton);
            dialog.Controls.Add(cancelButton);
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;
            UiTheme.ApplyFonts(dialog);

            return dialog.ShowDialog(this) == DialogResult.OK
                ? input.Text.Trim()
                : null;
        }

        private static string NormalizeLyricsFileName(string fileName)
        {
            string normalized = fileName.Trim();

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                normalized = normalized.Replace(invalidChar, '_');
            }

            if (!string.Equals(Path.GetExtension(normalized), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                normalized += ".txt";
            }

            return normalized;
        }

        private void SplitOneLineButton_Click(object? sender, EventArgs e)
        {
            string[] lines = GetNonEmptyLines();

            if (lines.Length == 0)
            {
                MessageBox.Show(this, "먼저 가사를 입력해 주세요.", "1줄 분할");
                return;
            }

            txtLyrics.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
            currentPageIndex = 0;
            UpdateView();
        }

        private void SplitTwoLinesButton_Click(object? sender, EventArgs e)
        {
            string[] lines = GetNonEmptyLines();

            if (lines.Length == 0)
            {
                MessageBox.Show(this, "먼저 가사를 입력해 주세요.", "2줄 분할");
                return;
            }

            string[] blocks = Enumerable.Range(0, (lines.Length + 1) / 2)
                .Select(index => string.Join(Environment.NewLine, lines.Skip(index * 2).Take(2)))
                .ToArray();

            txtLyrics.Text = string.Join(Environment.NewLine + Environment.NewLine, blocks);
            currentPageIndex = 0;
            UpdateView();
        }

        private void SplitThreeLinesButton_Click(object? sender, EventArgs e)
        {
            string[] lines = GetNonEmptyLines();

            if (lines.Length == 0)
            {
                MessageBox.Show(this, "먼저 가사를 입력해 주세요.", "3줄 분할");
                return;
            }

            string[] blocks = Enumerable.Range(0, (lines.Length + 2) / 3)
                .Select(index => string.Join(Environment.NewLine, lines.Skip(index * 3).Take(3)))
                .ToArray();

            txtLyrics.Text = string.Join(Environment.NewLine + Environment.NewLine, blocks);
            currentPageIndex = 0;
            UpdateView();
        }

        private void ShowOverlayButton_Click(object? sender, EventArgs e)
        {
            EnsureOverlayForm();
            overlayForm?.Show();
            overlayForm?.BringToFront();
            UpdateView();
        }

        private void HideOverlayButton_Click(object? sender, EventArgs e)
        {
            EnsureOverlayForm();
            overlayForm?.Show();
            overlayForm?.SetPages(Array.Empty<string>(), 0);
        }

        private void OverlayClickThrough_CheckedChanged(object? sender, EventArgs e)
        {
            EnsureOverlayForm();
        }

        private void PrevPageButton_Click(object? sender, EventArgs e)
        {
            string[] pages = GetPages();

            if (pages.Length == 0)
                return;

            currentPageIndex = Math.Max(0, currentPageIndex - 1);
            UpdateView();
        }

        private void NextPageButton_Click(object? sender, EventArgs e)
        {
            string[] pages = GetPages();

            if (pages.Length == 0)
                return;

            currentPageIndex = Math.Min(pages.Length - 1, currentPageIndex + 1);
            UpdateView();
        }

        private void ResetPageButton_Click(object? sender, EventArgs e)
        {
            currentPageIndex = 0;
            UpdateView();
        }

        private void LyricsGeneratorForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.PageUp)
            {
                PrevPageButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.PageDown)
            {
                NextPageButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home && e.Control)
            {
                ResetPageButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void EnsureOverlayForm()
        {
            if (sharedOverlayForm == null || sharedOverlayForm.IsDisposed)
            {
                sharedOverlayForm = new LyricsOverlayForm(settings);
                sharedOverlayForm.FormClosed += (s, e) => sharedOverlayForm = null;
            }

            overlayForm = sharedOverlayForm;
            overlayForm.PageChanged -= OverlayForm_PageChanged;
            overlayForm.PageChanged += OverlayForm_PageChanged;
            overlayForm.OverlayStateChanged -= OverlayForm_StateChanged;
            overlayForm.OverlayStateChanged += OverlayForm_StateChanged;
            overlayForm.SetLyricFontFamily(lyricFontFamily);
            overlayForm.SetClickThrough(chkOverlayClickThrough.Checked);
        }

        private void OverlayForm_StateChanged()
        {
            SaveOverlaySettings();
        }

        private void SaveOverlaySettings()
        {
            if (overlayForm == null || overlayForm.IsDisposed)
                return;

            UiSettings currentSettings = UiSettingsStore.Load();
            currentSettings.LyricsOverlayClickThrough = overlayForm.ClickThrough;

            if (overlayForm.WindowState == FormWindowState.Normal)
            {
                currentSettings.LyricsOverlayX = overlayForm.Left;
                currentSettings.LyricsOverlayY = overlayForm.Top;
                currentSettings.LyricsOverlayWidth = overlayForm.Width;
                currentSettings.LyricsOverlayHeight = overlayForm.Height;
            }

            UiSettingsStore.Save(currentSettings);
        }

        private void OverlayForm_PageChanged(int pageIndex)
        {
            string[] pages = GetPages();

            if (pages.Length == 0)
                return;

            currentPageIndex = Math.Max(0, Math.Min(pageIndex, pages.Length - 1));
            lblPageStatus.Text = (currentPageIndex + 1) + " / " + pages.Length;
        }

        private void ApplyLyricFont()
        {
            overlayForm?.SetLyricFontFamily(lyricFontFamily);
            UpdateView();
        }

        private static Font CreateSafeFont(string fontFamilyName, float size)
        {
            if (string.Equals(fontFamilyName, UiTheme.AppFontFamilyName, StringComparison.OrdinalIgnoreCase))
                return UiTheme.BoldFont(size);

            try
            {
                using FontFamily family = new FontFamily(fontFamilyName);
                FontStyle style = family.IsStyleAvailable(FontStyle.Bold)
                    ? FontStyle.Bold
                    : FontStyle.Regular;

                return new Font(family, size, style);
            }
            catch
            {
                return UiTheme.BoldFont(size);
            }
        }

        private void UpdateView()
        {
            string[] pages = GetPages();
            UpdateHeaderInfo();

            if (pages.Length == 0)
            {
                currentPageIndex = 0;
                lblPageStatus.Text = "0 / 0";
                overlayForm?.SetPages(Array.Empty<string>(), 0);
                return;
            }

            currentPageIndex = Math.Max(0, Math.Min(currentPageIndex, pages.Length - 1));
            string currentText = pages[currentPageIndex];

            lblPageStatus.Text = (currentPageIndex + 1) + " / " + pages.Length;
            overlayForm?.SetPages(pages, currentPageIndex);
        }

        private void UpdateHeaderInfo()
        {
            if (string.IsNullOrWhiteSpace(currentFilePath))
            {
                lblHeaderTitle.Text = "새 가사";
                lblFileName.Text = "아직 저장되지 않음";
                return;
            }

            lblHeaderTitle.Text = Path.GetFileName(currentFilePath);

            if (File.Exists(currentFilePath))
            {
                lblFileName.Text = "수정 일자: " + File.GetLastWriteTime(currentFilePath).ToString("yyyy-MM-dd HH:mm");
            }
            else
            {
                lblFileName.Text = "아직 저장되지 않음";
            }
        }

        private string[] GetNonEmptyLines()
        {
            return txtLyrics.Text
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
        }

        private string[] GetPages()
        {
            string normalized = txtLyrics.Text.Replace("\r\n", "\n").Trim();

            if (string.IsNullOrWhiteSpace(normalized))
                return Array.Empty<string>();

            return Regex.Split(normalized, @"\n\s*\n+")
                .Select(block => string.Join(
                    Environment.NewLine,
                    block.Split('\n')
                        .Select(line => line.Trim())
                        .Where(line => line.Length > 0)))
                .Where(block => block.Length > 0)
                .ToArray();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (overlayForm != null)
            {
                overlayForm.PageChanged -= OverlayForm_PageChanged;
                overlayForm.OverlayStateChanged -= OverlayForm_StateChanged;
            }

            overlayForm = null;
            base.OnFormClosed(e);
        }

        private sealed class LyricsFileItem
        {
            public LyricsFileItem(string filePath)
            {
                FilePath = filePath;
            }

            public string FilePath { get; }

            public override string ToString()
            {
                return Path.GetFileNameWithoutExtension(FilePath);
            }
        }
    }
}
