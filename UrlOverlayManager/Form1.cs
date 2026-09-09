using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public partial class Form1 : Form
    {
        private const int CommandButtonHeight = 28;
        private const int CommandButtonRowHeight = 34;
        private const int CommandSectionTitleHeight = 22;

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_TOGGLE_OVERLAYS = 100;
        private const int HOTKEY_EDIT_MODE = 101;
        private const int HOTKEY_TOGGLE_CLICK_THROUGH = 102;
        private const int HOTKEY_MOVE_TO_CURRENT_SCREEN = 103;
        private const int TITLE_BAR_HEIGHT = 60;
        private const int CONTENT_MARGIN = 18;
        private const int RESIZE_HANDLE_SIZE = 8;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly BindingSource itemBindingSource = new BindingSource();
        private readonly Dictionary<Guid, OverlayForm> overlayForms = new Dictionary<Guid, OverlayForm>();
        private readonly string configPath = GetConfigPath();
        private readonly string legacyConfigPath = Path.Combine(Application.StartupPath, "overlay-config.json");
        private readonly bool hostedMode;

        private List<OverlayItemConfig> items = new List<OverlayItemConfig>();
        private HotkeySettings hotkeys = HotkeySettings.CreateDefault();
        private List<PresetConfig> presets = new List<PresetConfig>();
        private OverlayItemConfig? selectedItem;
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private Panel? titleBar;
        private Label? titleLabel;
        private Button? btnTitleMinimize;
        private Button? btnTitleClose;
        private Button? btnPresets;
        private Button? btnHotkeys;
        private Button? btnRecover;
        private Button? btnAddClock;
        private TableLayoutPanel? managerLayout;
        private TableLayoutPanel? listSection;
        private TableLayoutPanel? editorSection;
        private readonly bool useTrayIntegration;
        private readonly bool preserveOverlaysOnClose;
        private bool isLoadingItem;
        private bool isRefreshingGrid;
        private bool isRealExit;
        private bool hotkeysRegistered;

        private static Color AppBackColor => UiTheme.AppBackColor;
        private static Color PanelBackColor => UiTheme.PanelBackColor;
        private static Color FieldBackColor => UiTheme.FieldBackColor;
        private static Color TextColor => UiTheme.TextColor;
        private static Color AccentColor => UiTheme.AccentColor;
        private static Color AccentDarkColor => UiTheme.AccentDarkColor;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public Form1(bool useTrayIntegration = true, bool preserveOverlaysOnClose = false, bool hostedMode = false)
        {
            this.useTrayIntegration = useTrayIntegration;
            this.preserveOverlaysOnClose = preserveOverlaysOnClose;
            this.hostedMode = hostedMode;

            InitializeComponent();

            InitGrid();
            InitOpacityControl();

            LoadConfig();
            RepairOffscreenOverlays();

            itemBindingSource.DataSource = items;
            RefreshGrid();

            InitEditorEvents();
            ShowVisibleOverlays();
            if (useTrayIntegration)
            {
                InitTrayIcon();
            }

            Resize += Form1_Resize;

            if (!hostedMode)
            {
                InitTitleBar();
            }

            AddMainActionButtons();
            ApplyModernDesign();
        }

        public void ApplyCurrentUiSettings()
        {
            ApplyModernDesign();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys(showErrors: false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotkeys();
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                HandleHotkey(m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);

            if (!hostedMode && m.Msg == WM_NCHITTEST && m.Result.ToInt32() == HTCLIENT)
            {
                m.Result = new IntPtr(GetResizeHitTestResult(m.LParam));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (hostedMode)
                return;

            using Pen borderPen = new Pen(UiTheme.BorderColor);
            Rectangle border = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            e.Graphics.DrawRectangle(borderPen, border);
        }

        private void ApplyModernDesign()
        {
            SetStyle(ControlStyles.ResizeRedraw, true);
            Text = "오버레이 관리";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = AppBackColor;
            ForeColor = TextColor;
            StartPosition = FormStartPosition.CenterScreen;

            if (!hostedMode)
            {
                ClientSize = new Size(760, 560);
                MinimumSize = new Size(720, 540);
            }

            LoadAppIcon();
            LayoutTitleBar();
            EnsureManagerLayout();
            LayoutMainControls();

            ApplyControlStyle(this);
            UiTheme.ApplyFonts(this);

            if (btnDelete != null)
            {
                btnDelete.BackColor = UiTheme.DangerColor;
            }

        }

        private void LoadAppIcon()
        {
            string iconPath = Path.Combine(Application.StartupPath, "Assets", "AppIcon.ico");

            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
            else
            {
                Icon? associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                if (associatedIcon != null)
                {
                    Icon = associatedIcon;
                }
            }

            ShowIcon = true;

            if (trayIcon != null)
            {
                trayIcon.Icon = Icon;
            }
        }

        private void InitTitleBar()
        {
            titleBar = new Panel();
            titleBar.Height = TITLE_BAR_HEIGHT;
            titleBar.BackColor = PanelBackColor;
            titleBar.MouseDown += TitleBar_MouseDown;

            titleLabel = new Label();
            titleLabel.Text = "오버레이 관리";
            titleLabel.AutoSize = false;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.ForeColor = TextColor;
            titleLabel.Font = UiTheme.BoldFont(13F);
            titleLabel.MouseDown += TitleBar_MouseDown;

            btnTitleMinimize = CreateTitleButton("_");
            btnTitleMinimize.Click += (s, e) =>
            {
                WindowState = FormWindowState.Minimized;
            };

            btnTitleClose = CreateTitleButton("X");
            btnTitleClose.Click += (s, e) =>
            {
                Close();
            };

            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(btnTitleMinimize);
            titleBar.Controls.Add(btnTitleClose);
            Controls.Add(titleBar);
            titleBar.BringToFront();
        }

        private static Button CreateTitleButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.Transparent;
            button.ForeColor = UiTheme.TextColor;
            button.Font = UiTheme.BoldFont();
            button.Cursor = Cursors.Hand;
            return button;
        }

        private void LayoutTitleBar()
        {
            if (titleBar == null || titleLabel == null || btnTitleMinimize == null || btnTitleClose == null)
                return;

            titleBar.Location = new Point(0, 0);
            titleBar.Size = new Size(ClientSize.Width, TITLE_BAR_HEIGHT);
            titleBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnTitleClose.Size = new Size(44, TITLE_BAR_HEIGHT);
            btnTitleClose.Location = new Point(ClientSize.Width - 44, 0);
            btnTitleClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnTitleMinimize.Size = new Size(44, TITLE_BAR_HEIGHT);
            btnTitleMinimize.Location = new Point(ClientSize.Width - 88, 0);
            btnTitleMinimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            titleLabel.Location = new Point(20, 0);
            titleLabel.Size = new Size(ClientSize.Width - 116, TITLE_BAR_HEIGHT);
            titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        private int GetResizeHitTestResult(IntPtr lParam)
        {
            int x = unchecked((short)(long)lParam);
            int y = unchecked((short)((long)lParam >> 16));
            Point cursor = PointToClient(new Point(x, y));

            bool left = cursor.X <= RESIZE_HANDLE_SIZE;
            bool right = cursor.X >= ClientSize.Width - RESIZE_HANDLE_SIZE;
            bool top = cursor.Y <= RESIZE_HANDLE_SIZE;
            bool bottom = cursor.Y >= ClientSize.Height - RESIZE_HANDLE_SIZE;

            if (left && top)
                return HTTOPLEFT;
            if (right && top)
                return HTTOPRIGHT;
            if (left && bottom)
                return HTBOTTOMLEFT;
            if (right && bottom)
                return HTBOTTOMRIGHT;
            if (left)
                return HTLEFT;
            if (right)
                return HTRIGHT;
            if (top)
                return HTTOP;
            if (bottom)
                return HTBOTTOM;

            return HTCLIENT;
        }

        private void AddMainActionButtons()
        {
            int actionTop = TITLE_BAR_HEIGHT + CONTENT_MARGIN + 302;

            btnPresets = CreateMainActionButton("프리셋", CONTENT_MARGIN, actionTop, 96);
            btnPresets.Click += (s, e) => ShowPresetManager();

            btnHotkeys = CreateMainActionButton("단축키", CONTENT_MARGIN + 104, actionTop, 96);
            btnHotkeys.Click += (s, e) => ShowHotkeySettings();

            btnRecover = CreateMainActionButton("화면으로 모으기", CONTENT_MARGIN + 208, actionTop, 140);
            btnRecover.Click += (s, e) => MoveOverlaysToCurrentScreen();

            btnAddClock = CreateMainActionButton("시계 추가", CONTENT_MARGIN + 356, actionTop, 96);
            btnAddClock.Click += (s, e) => AddClockOverlay();

            Controls.Add(btnPresets);
            Controls.Add(btnHotkeys);
            Controls.Add(btnRecover);
            Controls.Add(btnAddClock);
        }

        private void EnsureManagerLayout()
        {
            if (managerLayout != null)
                return;

            managerLayout = new TableLayoutPanel();
            managerLayout.ColumnCount = 1;
            managerLayout.RowCount = 2;
            managerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            managerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            managerLayout.BackColor = AppBackColor;

            listSection = CreatePanelSection("오버레이 목록");
            editorSection = CreatePanelSection("선택 항목");

            Reparent(dgvItems, listSection, 0, 1);

            TableLayoutPanel workArea = new TableLayoutPanel();
            workArea.Dock = DockStyle.Fill;
            workArea.ColumnCount = 2;
            workArea.RowCount = 1;
            workArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
            workArea.BackColor = AppBackColor;
            workArea.Controls.Add(listSection, 0, 0);
            workArea.Controls.Add(CreateManagerCommandPanel(), 1, 0);

            BuildEditorSection();

            managerLayout.Controls.Add(workArea, 0, 0);
            managerLayout.Controls.Add(editorSection, 0, 1);
            Controls.Add(managerLayout);
            managerLayout.SendToBack();
        }

        private Control CreateManagerCommandPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 3;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.BackColor = UiTheme.AppBackColor;
            panel.Margin = new Padding(8, 0, 0, 6);

            TableLayoutPanel editSection = CreateCommandSection("관리", 3);
            Reparent(btnAdd, editSection, 0, 1);
            Reparent(btnDelete, editSection, 0, 2);

            TableLayoutPanel toolSection = CreateCommandSection("도구", 5);
            ReparentIfPresent(btnPresets, toolSection, 0, 1);
            ReparentIfPresent(btnHotkeys, toolSection, 0, 2);
            ReparentIfPresent(btnRecover, toolSection, 0, 3);
            ReparentIfPresent(btnAddClock, toolSection, 0, 4);

            panel.Controls.Add(editSection, 0, 0);
            panel.Controls.Add(toolSection, 0, 1);
            return panel;
        }

        private static TableLayoutPanel CreateCommandSection(string titleText, int rowCount)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Fill;
            section.ColumnCount = 1;
            section.RowCount = rowCount;
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, CommandSectionTitleHeight));
            for (int i = 1; i < rowCount; i++)
            {
                section.RowStyles.Add(new RowStyle(SizeType.Absolute, CommandButtonRowHeight));
            }

            section.Padding = new Padding(8, 6, 8, 8);
            section.Margin = new Padding(0, 0, 0, 8);
            section.BackColor = UiTheme.PanelBackColor;
            section.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, section.ClientRectangle, UiTheme.BorderColor, ButtonBorderStyle.Solid);
            };

            Label title = new Label();
            title.Dock = DockStyle.Fill;
            title.Text = titleText;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = UiTheme.TextColor;
            title.Font = UiTheme.BoldFont(10F);
            section.Controls.Add(title, 0, 0);
            return section;
        }

        private static TableLayoutPanel CreatePanelSection(string titleText)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Fill;
            section.ColumnCount = 1;
            section.RowCount = 2;
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            section.Padding = new Padding(6, 4, 6, 6);
            section.BackColor = UiTheme.PanelBackColor;
            section.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            section.Margin = new Padding(0, 0, 0, 6);
            section.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(
                    e.Graphics,
                    section.ClientRectangle,
                    UiTheme.BorderColor,
                    ButtonBorderStyle.Solid);
            };

            Label title = new Label();
            title.Dock = DockStyle.Fill;
            title.Text = titleText;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = UiTheme.TextColor;
            title.Font = UiTheme.BoldFont(10F);

            section.Controls.Add(title, 0, 0);
            return section;
        }

        private static FlowLayoutPanel CreateActionRow(FlowDirection flowDirection)
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = flowDirection,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 2, 0, 0),
                Margin = Padding.Empty,
                BackColor = UiTheme.AppBackColor
            };
        }

        private static void Reparent(Control control, Control parent)
        {
            control.Parent?.Controls.Remove(control);
            parent.Controls.Add(control);
        }

        private static void ReparentIfPresent(Control? control, Control parent)
        {
            if (control == null)
                return;

            Reparent(control, parent);
        }

        private static void ReparentIfPresent(Control? control, TableLayoutPanel parent, int column, int row)
        {
            if (control == null)
                return;

            Reparent(control, parent, column, row);
        }

        private static void Reparent(Control control, TableLayoutPanel parent, int column, int row)
        {
            control.Parent?.Controls.Remove(control);
            parent.Controls.Add(control, column, row);
        }

        private void BuildEditorSection()
        {
            if (editorSection == null)
                return;

            TableLayoutPanel editorGrid = new TableLayoutPanel();
            editorGrid.Dock = DockStyle.Fill;
            editorGrid.ColumnCount = 8;
            editorGrid.RowCount = 2;
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            editorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            editorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            editorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            editorGrid.BackColor = UiTheme.PanelBackColor;

            Reparent(name, editorGrid, 0, 0);
            Reparent(txtName, editorGrid, 1, 0);
            Reparent(chkEnabled, editorGrid, 2, 0);

            Reparent(opacity, editorGrid, 5, 0);
            Reparent(numOpacity, editorGrid, 6, 0);
            Reparent(chkClickThrough, editorGrid, 7, 0);

            Reparent(url, editorGrid, 0, 1);
            Reparent(txtUrl, editorGrid, 1, 1);
            editorGrid.SetColumnSpan(txtUrl, 3);

            editorSection.Controls.Add(editorGrid, 0, 1);
        }

        private static Button CreateMainActionButton(string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 23);
            button.UseVisualStyleBackColor = true;
            return button;
        }

        private void LayoutMainControls()
        {
            int top = (hostedMode ? 0 : TITLE_BAR_HEIGHT) + CONTENT_MARGIN;
            int left = CONTENT_MARGIN;
            int width = ClientSize.Width - CONTENT_MARGIN * 2;

            if (managerLayout != null)
            {
                int hostedMargin = hostedMode ? 10 : CONTENT_MARGIN;
                int contentLeft = hostedMode ? 12 : left;
                int contentTop = hostedMode ? 10 : top;
                int contentWidth = ClientSize.Width - contentLeft * 2;
                managerLayout.Location = new Point(contentLeft, contentTop);
                managerLayout.Size = new Size(contentWidth, ClientSize.Height - contentTop - hostedMargin);
                managerLayout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                dgvItems.Dock = DockStyle.Fill;
                dgvItems.Margin = Padding.Empty;

                SetActionButtonLayout(btnPresets, 96);
                SetActionButtonLayout(btnHotkeys, 96);
                SetActionButtonLayout(btnRecover, 140);
                SetActionButtonLayout(btnAddClock, 96);
                SetActionButtonLayout(btnAdd, 76);
                SetActionButtonLayout(btnDelete, 78);

                SetEditorLabelLayout(name);
                SetEditorLabelLayout(url);
                SetEditorLabelLayout(opacity);

                txtName.Dock = DockStyle.Fill;
                txtName.Margin = new Padding(0, 3, 10, 3);
                txtUrl.Dock = DockStyle.Fill;
                txtUrl.Margin = new Padding(0, 3, 10, 3);
                numOpacity.Dock = DockStyle.Fill;
                numOpacity.Margin = new Padding(0, 3, 10, 3);

                chkEnabled.Dock = DockStyle.Fill;
                chkEnabled.Margin = new Padding(0, 4, 12, 2);
                chkClickThrough.Dock = DockStyle.Fill;
                chkClickThrough.Margin = new Padding(0, 4, 0, 2);
                return;
            }

            dgvItems.Location = new Point(left, top);
            dgvItems.Size = new Size(width, 260);
            dgvItems.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            int actionTop = top + 302;

            if (btnPresets != null)
            {
                btnPresets.Location = new Point(left, actionTop);
                btnPresets.Size = new Size(96, 32);
                btnPresets.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }

            if (btnHotkeys != null)
            {
                btnHotkeys.Location = new Point(left + 104, actionTop);
                btnHotkeys.Size = new Size(96, 32);
                btnHotkeys.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }

            if (btnRecover != null)
            {
                btnRecover.Location = new Point(left + 208, actionTop);
                btnRecover.Size = new Size(140, 32);
                btnRecover.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }

            if (btnAddClock != null)
            {
                btnAddClock.Location = new Point(left + 356, actionTop);
                btnAddClock.Size = new Size(96, 32);
                btnAddClock.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }

            btnAdd.Location = new Point(ClientSize.Width - 178, actionTop);
            btnAdd.Size = new Size(76, 32);
            btnAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnDelete.Location = new Point(ClientSize.Width - 96, actionTop);
            btnDelete.Size = new Size(78, 32);
            btnDelete.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            name.Location = new Point(left + 2, top + 274);
            name.Size = new Size(70, 24);
            txtName.Location = new Point(left + 78, top + 274);
            txtName.Size = new Size(240, 24);

            url.Location = new Point(left + 2, top + 354);
            url.Size = new Size(70, 24);
            txtUrl.Location = new Point(left + 78, top + 354);
            txtUrl.Size = new Size(ClientSize.Width - (left + 78) - CONTENT_MARGIN, 24);
            txtUrl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            opacity.Location = new Point(left + 2, top + 396);
            opacity.Size = new Size(70, 24);
            numOpacity.Location = new Point(left + 78, top + 396);
            numOpacity.Size = new Size(120, 24);

            chkEnabled.Location = new Point(left + 354, top + 274);
            chkEnabled.Size = new Size(130, 24);
            chkClickThrough.Location = new Point(left + 354, top + 396);
            chkClickThrough.Size = new Size(110, 24);
        }

        private static void SetActionButtonLayout(Button? button, int width)
        {
            if (button == null)
                return;

            button.Width = width;
            button.Height = CommandButtonHeight;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 2, 0, 4);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        }

        private static void SetEditorLabelLayout(Label label)
        {
            label.Dock = DockStyle.Fill;
            label.Margin = Padding.Empty;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        private void ApplyControlStyle(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label)
                {
                    control.ForeColor = TextColor;
                    control.Font = UiTheme.RegularFont();
                }
                else if (control is TextBox textBox)
                {
                    UiTheme.StyleTextBox(textBox);
                }
                else if (control is Button button)
                {
                    UiTheme.StyleButton(button, AccentColor, Color.White);
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.ForeColor = TextColor;
                    checkBox.Font = UiTheme.RegularFont();
                }
                else if (control is NumericUpDown numeric)
                {
                    UiTheme.StyleNumeric(numeric);
                }

                if (control.HasChildren)
                {
                    ApplyControlStyle(control);
                }
            }

            ApplyGridStyle();
        }

        private void ApplyGridStyle()
        {
            if (dgvItems == null)
                return;

            UiTheme.StyleGrid(dgvItems);
            dgvItems.AllowUserToResizeColumns = false;
            dgvItems.RowTemplate.Height = 42;
            dgvItems.ColumnHeadersHeight = 28;
        }

        private void InitTrayIcon()
        {
            trayMenu = new ContextMenuStrip();

            AddTrayMenuItem("열기", ShowMainForm);
            AddTrayMenuItem("메인 창 숨기기", Hide);
            trayMenu.Items.Add(new ToolStripSeparator());
            AddTrayMenuItem("모든 오버레이 숨김", HideAllOverlays);
            AddTrayMenuItem("표시 설정 복구", ShowConfiguredOverlays);
            AddTrayMenuItem("편집 모드", EnterEditMode);
            AddTrayMenuItem("현재 화면으로 모으기", MoveOverlaysToCurrentScreen);
            trayMenu.Items.Add(new ToolStripSeparator());
            AddTrayMenuItem("프리셋 관리", ShowPresetManager);
            AddTrayMenuItem("단축키 설정", ShowHotkeySettings);
            trayMenu.Items.Add(new ToolStripSeparator());
            AddTrayMenuItem("종료", ExitApplication);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "URL Overlay Manager";
            trayIcon.Icon = Icon != null ? Icon : SystemIcons.Application;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => ShowMainForm();
        }

        private void AddTrayMenuItem(string text, Action action)
        {
            if (trayMenu == null)
                return;

            ToolStripMenuItem menuItem = new ToolStripMenuItem(text);
            menuItem.Click += (s, e) => action();
            trayMenu.Items.Add(menuItem);
        }

        public void ShowMainForm()
        {
            Show();

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();
            SetForegroundWindow(Handle);
        }

        private void HideAllOverlays()
        {
            foreach (OverlayItemConfig item in items)
            {
                item.Visible = false;
            }

            foreach (OverlayForm form in new List<OverlayForm>(overlayForms.Values))
            {
                form.Hide();
            }

            SyncSelectedEditorChecks();
            dgvItems.Refresh();
            SaveConfig();
        }

        private void ShowConfiguredOverlays()
        {
            foreach (OverlayItemConfig item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Url))
                {
                    item.Visible = true;
                    ShowOverlay(item);
                }
            }

            SyncSelectedEditorChecks();
            dgvItems.Refresh();
            SaveConfig();
        }

        private void ToggleOverlayWindows()
        {
            bool anyVisible = false;

            foreach (OverlayForm form in overlayForms.Values)
            {
                if (form.Visible)
                {
                    anyVisible = true;
                    break;
                }
            }

            if (anyVisible)
            {
                foreach (OverlayForm form in overlayForms.Values)
                {
                    form.Hide();
                }
            }
            else
            {
                ShowVisibleOverlays();
            }
        }

        private void EnterEditMode()
        {
            foreach (OverlayItemConfig item in items)
            {
                if (item.Visible && item.ClickThrough)
                {
                    item.ClickThrough = false;
                    ShowOverlay(item);
                }
            }

            ShowMainForm();
            SyncSelectedEditorChecks();
            dgvItems.Refresh();
            SaveConfig();
        }

        private void ToggleAllClickThrough()
        {
            bool enable = false;

            foreach (OverlayItemConfig item in items)
            {
                if (item.Visible && !item.ClickThrough)
                {
                    enable = true;
                    break;
                }
            }

            foreach (OverlayItemConfig item in items)
            {
                if (item.Visible)
                {
                    item.ClickThrough = enable;
                    ShowOverlay(item);
                }
            }

            SyncSelectedEditorChecks();
            dgvItems.Refresh();
            SaveConfig();
        }

        private void MoveOverlaysToCurrentScreen()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int offset = 0;

            foreach (OverlayItemConfig item in items)
            {
                MoveOverlayIntoArea(item, workingArea, offset);
                offset += 24;

                if (overlayForms.TryGetValue(item.Id, out OverlayForm? form))
                {
                    form.ApplyConfig();
                }
            }

            SaveConfig();
        }

        public void ExitApplication()
        {
            PrepareForApplicationExit();
            Application.Exit();
        }

        public void PrepareForApplicationExit()
        {
            if (isRealExit)
                return;

            isRealExit = true;
            SaveConfig();
            UnregisterHotkeys();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }

            foreach (OverlayForm form in new List<OverlayForm>(overlayForms.Values))
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                }
            }

            overlayForms.Clear();
        }

        private void InitEditorEvents()
        {
            txtName.Leave += EditorValueChanged;
            txtUrl.Leave += EditorValueChanged;
            numOpacity.ValueChanged += EditorValueChanged;
            chkEnabled.CheckedChanged += EditorValueChanged;
            chkClickThrough.CheckedChanged += EditorValueChanged;
        }

        private void EditorValueChanged(object? sender, EventArgs e)
        {
            ApplySelectedItemConfig();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveConfig();

            if (!useTrayIntegration)
            {
                if (preserveOverlaysOnClose && !isRealExit)
                {
                    e.Cancel = true;
                    Hide();
                    return;
                }

                foreach (OverlayForm form in new List<OverlayForm>(overlayForms.Values))
                {
                    form.Close();
                }

                overlayForms.Clear();
                return;
            }

            if (!isRealExit)
            {
                e.Cancel = true;
                Hide();

                trayIcon?.ShowBalloonTip(
                    1000,
                    "URL Overlay Manager",
                    "프로그램이 시스템 트레이에서 계속 실행 중입니다.",
                    ToolTipIcon.Info
                );
            }
        }

        private void Form1_Resize(object? sender, EventArgs e)
        {
            LayoutTitleBar();
            LayoutMainControls();

            if (!useTrayIntegration)
                return;

            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string? configDirectory = Path.GetDirectoryName(configPath);

                if (!string.IsNullOrWhiteSpace(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                AppConfig config = new AppConfig
                {
                    Items = items,
                    Hotkeys = hotkeys,
                    Presets = presets
                };

                JsonSerializerOptions options = new JsonSerializerOptions();
                options.WriteIndented = true;

                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("설정 파일을 저장하는 중 오류가 발생했습니다.\n" + ex.Message);
            }
        }

        private void LoadConfig()
        {
            string sourcePath = File.Exists(configPath) ? configPath : legacyConfigPath;

            if (!File.Exists(sourcePath))
            {
                items = new List<OverlayItemConfig>();
                hotkeys = HotkeySettings.CreateDefault();
                presets = new List<PresetConfig>();
                return;
            }

            try
            {
                string json = File.ReadAllText(sourcePath);
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    items = JsonSerializer.Deserialize<List<OverlayItemConfig>>(json) ?? new List<OverlayItemConfig>();
                    hotkeys = HotkeySettings.CreateDefault();
                    presets = new List<PresetConfig>();
                }
                else
                {
                    AppConfig? loadedConfig = JsonSerializer.Deserialize<AppConfig>(json);
                    items = loadedConfig?.Items ?? new List<OverlayItemConfig>();
                    hotkeys = loadedConfig?.Hotkeys ?? HotkeySettings.CreateDefault();
                    presets = loadedConfig?.Presets ?? new List<PresetConfig>();
                }

                if (sourcePath == legacyConfigPath || document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    SaveConfig();
                }
            }
            catch
            {
                items = new List<OverlayItemConfig>();
                hotkeys = HotkeySettings.CreateDefault();
                presets = new List<PresetConfig>();
                MessageBox.Show("설정 파일을 불러오는 중 오류가 발생했습니다.");
            }
        }

        private void ShowVisibleOverlays()
        {
            foreach (OverlayItemConfig item in items)
            {
                if (item.Visible)
                {
                    ShowOverlay(item);
                }
            }
        }

        private void InitGrid()
        {
            dgvItems.AutoGenerateColumns = false;
            dgvItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect = false;
            dgvItems.AllowUserToAddRows = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.ReadOnly = true;
            dgvItems.RowTemplate.Height = 42;
            dgvItems.ColumnHeadersHeight = 28;
            dgvItems.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvItems.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvItems.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvItems.AdvancedCellBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;
            dgvItems.AdvancedCellBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
            dgvItems.AdvancedCellBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.None;
            dgvItems.AdvancedCellBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.Single;
            dgvItems.CellClick += dgvItems_CellClick;
            dgvItems.Columns.Clear();

            dgvItems.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "표시",
                DataPropertyName = "Visible",
                Width = 50
            });

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "이름",
                DataPropertyName = "Name",
                Width = 120
            });

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "URL",
                DataPropertyName = "Url",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            itemBindingSource.DataSource = items;
            dgvItems.DataSource = itemBindingSource;
        }

        private void InitOpacityControl()
        {
            numOpacity.Minimum = 10;
            numOpacity.Maximum = 100;
            numOpacity.Value = 90;
        }

        private void RefreshGrid()
        {
            isRefreshingGrid = true;

            try
            {
                itemBindingSource.ResetBindings(false);
            }
            finally
            {
                isRefreshingGrid = false;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            OverlayItemConfig item = new OverlayItemConfig
            {
                Name = "새 오버레이",
                Url = "",
                Visible = false,
                ClickThrough = false,
                Opacity = 0.9
            };

            items.Add(item);
            RefreshGrid();
            selectedItem = item;
            SelectItem(item);

            BeginInvoke(new Action(() => SelectGridItem(item)));
            SaveConfig();
        }

        private void AddClockOverlay()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;

            OverlayItemConfig item = new OverlayItemConfig
            {
                Name = "시계",
                Url = ClockOverlayContent.Url,
                Visible = true,
                ClickThrough = false,
                Opacity = 1.0,
                Width = 420,
                Height = 120,
                X = workingArea.Left + 80,
                Y = workingArea.Top + 80
            };

            items.Add(item);
            RefreshGrid();
            selectedItem = item;
            SelectItem(item);
            ShowOverlay(item);

            BeginInvoke(new Action(() => SelectGridItem(item)));
            SaveConfig();
        }

        private void SelectGridItem(OverlayItemConfig targetItem)
        {
            if (dgvItems.Rows.Count == 0)
                return;

            isRefreshingGrid = true;

            try
            {
                dgvItems.ClearSelection();

                foreach (DataGridViewRow row in dgvItems.Rows)
                {
                    if (row.IsNewRow)
                        continue;

                    OverlayItemConfig? rowItem = row.DataBoundItem as OverlayItemConfig;

                    if (rowItem != null && rowItem.Id == targetItem.Id)
                    {
                        row.Selected = true;
                        dgvItems.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }
            finally
            {
                isRefreshingGrid = false;
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (selectedItem == null)
            {
                MessageBox.Show("삭제할 항목을 선택하세요.");
                return;
            }

            OverlayItemConfig deleteItem = selectedItem;

            DialogResult result = MessageBox.Show(
                "선택한 항목을 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButtons.YesNo
            );

            if (result != DialogResult.Yes)
                return;

            if (overlayForms.TryGetValue(deleteItem.Id, out OverlayForm? form))
            {
                overlayForms.Remove(deleteItem.Id);
                form.Close();
            }

            items.Remove(deleteItem);
            selectedItem = null;
            RefreshGrid();
            ClearEditor();
            SaveConfig();
        }

        private void ClearEditor()
        {
            isLoadingItem = true;

            try
            {
                txtName.Text = "";
                txtUrl.Text = "";
                numOpacity.Value = 90;
                chkEnabled.Checked = false;
                chkClickThrough.Checked = false;
            }
            finally
            {
                isLoadingItem = false;
            }
        }

        private void dgvItems_SelectionChanged(object sender, EventArgs e)
        {
            if (isRefreshingGrid || isLoadingItem)
                return;

            if (dgvItems.CurrentRow == null || dgvItems.CurrentRow.Index < 0 || dgvItems.CurrentRow.IsNewRow)
                return;

            OverlayItemConfig? item = dgvItems.CurrentRow.DataBoundItem as OverlayItemConfig;

            if (item != null)
            {
                SelectItem(item);
            }
        }

        private void dgvItems_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (isRefreshingGrid || isLoadingItem)
                return;

            if (e.RowIndex < 0 || e.ColumnIndex != 0)
                return;

            DataGridViewRow row = dgvItems.Rows[e.RowIndex];

            if (row.IsNewRow)
                return;

            if (row.DataBoundItem is not OverlayItemConfig item)
                return;

            item.Visible = !item.Visible;

            if (item.Visible)
            {
                ShowOverlay(item);
            }
            else
            {
                HideOverlay(item);
            }

            if (selectedItem == null || selectedItem.Id == item.Id)
            {
                selectedItem = item;
                SyncSelectedEditorChecks();
            }

            dgvItems.Refresh();
            SaveConfig();
        }

        private void SelectItem(OverlayItemConfig item)
        {
            isLoadingItem = true;

            try
            {
                selectedItem = item;
                txtName.Text = item.Name;
                txtUrl.Text = item.Url;

                decimal opacityValue = Convert.ToDecimal(item.Opacity * 100);
                opacityValue = Math.Max(numOpacity.Minimum, Math.Min(numOpacity.Maximum, opacityValue));
                numOpacity.Value = opacityValue;

                chkEnabled.Checked = item.Visible;
                chkClickThrough.Checked = item.ClickThrough;
            }
            finally
            {
                isLoadingItem = false;
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (selectedItem == null)
            {
                MessageBox.Show("적용할 항목을 선택하세요.");
                return;
            }

            ApplySelectedItemConfig();
        }

        private void ApplySelectedItemConfig()
        {
            if (isLoadingItem || isRefreshingGrid || selectedItem == null)
                return;

            selectedItem.Name = txtName.Text.Trim();
            selectedItem.Url = txtUrl.Text.Trim();
            selectedItem.Opacity = Convert.ToDouble(numOpacity.Value) / 100.0;
            selectedItem.Visible = chkEnabled.Checked;
            selectedItem.ClickThrough = chkClickThrough.Checked;

            dgvItems.Refresh();

            if (selectedItem.Visible)
            {
                ShowOverlay(selectedItem);
            }
            else
            {
                HideOverlay(selectedItem);
            }

            SaveConfig();
        }

        private void ShowOverlay(OverlayItemConfig item)
        {
            if (overlayForms.TryGetValue(item.Id, out OverlayForm? existingForm))
            {
                existingForm.ApplyConfig();
                existingForm.Show();
                existingForm.BringToFront();
                return;
            }

            OverlayForm form = new OverlayForm(item);
            form.ConfigChanged += () =>
            {
                dgvItems.Refresh();
                SyncSelectedEditorChecks();
                SaveConfig();
            };

            form.FormClosed += (s, e) =>
            {
                overlayForms.Remove(item.Id);
            };

            overlayForms.Add(item.Id, form);
            form.Show();
        }

        private void HideOverlay(OverlayItemConfig item)
        {
            if (overlayForms.TryGetValue(item.Id, out OverlayForm? form))
            {
                form.Hide();
            }
        }

        private void txtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplySelectedItemConfig();
                e.SuppressKeyPress = true;
            }
        }

        private void SyncSelectedEditorChecks()
        {
            if (selectedItem == null)
                return;

            isLoadingItem = true;

            try
            {
                chkEnabled.Checked = selectedItem.Visible;
                chkClickThrough.Checked = selectedItem.ClickThrough;
            }
            finally
            {
                isLoadingItem = false;
            }
        }

        private void RepairOffscreenOverlays()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int offset = 0;
            bool changed = false;

            foreach (OverlayItemConfig item in items)
            {
                if (!IsOverlayOnAnyScreen(item))
                {
                    MoveOverlayIntoArea(item, workingArea, offset);
                    offset += 24;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveConfig();
            }
        }

        private static bool IsOverlayOnAnyScreen(OverlayItemConfig item)
        {
            Rectangle bounds = new Rectangle(item.X, item.Y, Math.Max(1, item.Width), Math.Max(1, item.Height));

            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle intersection = Rectangle.Intersect(bounds, screen.WorkingArea);

                if (intersection.Width >= 40 && intersection.Height >= 40)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MoveOverlayIntoArea(OverlayItemConfig item, Rectangle workingArea, int offset)
        {
            item.Width = Math.Max(150, Math.Min(item.Width, workingArea.Width));
            item.Height = Math.Max(100, Math.Min(item.Height, workingArea.Height));
            item.X = workingArea.Left + 40 + offset;
            item.Y = workingArea.Top + 40 + offset;

            if (item.X + item.Width > workingArea.Right)
            {
                item.X = workingArea.Right - item.Width;
            }

            if (item.Y + item.Height > workingArea.Bottom)
            {
                item.Y = workingArea.Bottom - item.Height;
            }
        }

        private void ShowPresetManager()
        {
            using PresetManagerForm form = new PresetManagerForm(presets, items);

            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            presets = form.ResultPresets;

            if (form.PresetToApply != null)
            {
                ApplyPreset(form.PresetToApply);
                return;
            }

            if (form.PresetsChanged)
            {
                SaveConfig();
            }
        }

        private void ApplyPreset(PresetConfig preset)
        {
            foreach (OverlayForm form in new List<OverlayForm>(overlayForms.Values))
            {
                form.Close();
            }

            overlayForms.Clear();
            items = PresetConfig.CloneItems(preset.Items);
            itemBindingSource.DataSource = items;
            selectedItem = null;
            RefreshGrid();
            ClearEditor();
            RepairOffscreenOverlays();
            ShowVisibleOverlays();
            SaveConfig();
        }

        private void ShowHotkeySettings()
        {
            using HotkeySettingsForm form = new HotkeySettingsForm(hotkeys);

            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            hotkeys = form.Result;
            SaveConfig();
            RegisterHotkeys(showErrors: true);
        }

        private void HandleHotkey(int id)
        {
            switch (id)
            {
                case HOTKEY_TOGGLE_OVERLAYS:
                    ToggleOverlayWindows();
                    break;
                case HOTKEY_EDIT_MODE:
                    EnterEditMode();
                    break;
                case HOTKEY_TOGGLE_CLICK_THROUGH:
                    ToggleAllClickThrough();
                    break;
                case HOTKEY_MOVE_TO_CURRENT_SCREEN:
                    MoveOverlaysToCurrentScreen();
                    break;
            }
        }

        private void RegisterHotkeys(bool showErrors)
        {
            if (!IsHandleCreated)
                return;

            UnregisterHotkeys();

            List<string> failedHotkeys = new List<string>();
            TryRegisterHotkey(HOTKEY_TOGGLE_OVERLAYS, hotkeys.ToggleOverlays, "전체 표시/숨김", failedHotkeys);
            TryRegisterHotkey(HOTKEY_EDIT_MODE, hotkeys.EditMode, "편집 모드", failedHotkeys);
            TryRegisterHotkey(HOTKEY_TOGGLE_CLICK_THROUGH, hotkeys.ToggleClickThrough, "클릭 무시 토글", failedHotkeys);
            TryRegisterHotkey(HOTKEY_MOVE_TO_CURRENT_SCREEN, hotkeys.MoveToCurrentScreen, "현재 화면으로 모으기", failedHotkeys);

            hotkeysRegistered = true;

            if (showErrors && failedHotkeys.Count > 0)
            {
                MessageBox.Show("이미 다른 프로그램에서 사용 중인 단축키가 있습니다.\n\n" + string.Join("\n", failedHotkeys));
            }
        }

        private void TryRegisterHotkey(int id, HotkeyConfig hotkey, string name, List<string> failedHotkeys)
        {
            if (!hotkey.IsValid())
                return;

            uint modifiers = MOD_NOREPEAT;

            if (hotkey.Control)
                modifiers |= MOD_CONTROL;

            if (hotkey.Alt)
                modifiers |= MOD_ALT;

            if (hotkey.Shift)
                modifiers |= MOD_SHIFT;

            if (hotkey.Win)
                modifiers |= MOD_WIN;

            if (!RegisterHotKey(Handle, id, modifiers, (uint)hotkey.Key))
            {
                failedHotkeys.Add(name + ": " + hotkey);
            }
        }

        private void UnregisterHotkeys()
        {
            if (!hotkeysRegistered || !IsHandleCreated)
                return;

            UnregisterHotKey(Handle, HOTKEY_TOGGLE_OVERLAYS);
            UnregisterHotKey(Handle, HOTKEY_EDIT_MODE);
            UnregisterHotKey(Handle, HOTKEY_TOGGLE_CLICK_THROUGH);
            UnregisterHotKey(Handle, HOTKEY_MOVE_TO_CURRENT_SCREEN);
            hotkeysRegistered = false;
        }

        private static string GetConfigPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "UrlOverlayManager", "overlay-config.json");
        }
    }
}
