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
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_TOGGLE_OVERLAYS = 100;
        private const int HOTKEY_EDIT_MODE = 101;
        private const int HOTKEY_TOGGLE_CLICK_THROUGH = 102;
        private const int HOTKEY_MOVE_TO_CURRENT_SCREEN = 103;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly BindingSource itemBindingSource = new BindingSource();
        private readonly Dictionary<Guid, OverlayForm> overlayForms = new Dictionary<Guid, OverlayForm>();
        private readonly string configPath = GetConfigPath();
        private readonly string legacyConfigPath = Path.Combine(Application.StartupPath, "overlay-config.json");

        private List<OverlayItemConfig> items = new List<OverlayItemConfig>();
        private HotkeySettings hotkeys = HotkeySettings.CreateDefault();
        private List<PresetConfig> presets = new List<PresetConfig>();
        private OverlayItemConfig? selectedItem;
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private bool isLoadingItem;
        private bool isRefreshingGrid;
        private bool isRealExit;
        private bool hotkeysRegistered;

        private static readonly Color AppBackColor = Color.FromArgb(244, 250, 253);
        private static readonly Color PanelBackColor = Color.FromArgb(232, 244, 250);
        private static readonly Color FieldBackColor = Color.White;
        private static readonly Color TextColor = Color.FromArgb(38, 66, 82);
        private static readonly Color AccentColor = Color.FromArgb(116, 174, 207);
        private static readonly Color AccentDarkColor = Color.FromArgb(80, 139, 174);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public Form1()
        {
            InitializeComponent();

            InitGrid();
            InitOpacityControl();

            LoadConfig();
            RepairOffscreenOverlays();

            itemBindingSource.DataSource = items;
            RefreshGrid();

            InitEditorEvents();
            ShowVisibleOverlays();
            InitTrayIcon();

            Resize += Form1_Resize;

            AddMainActionButtons();
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
        }

        private void ApplyModernDesign()
        {
            Text = "URL Overlay Manager";
            BackColor = AppBackColor;
            ForeColor = TextColor;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(550, 300);
            LoadAppIcon();

            ApplyControlStyle(this);

            if (btnDelete != null)
            {
                btnDelete.BackColor = Color.FromArgb(214, 112, 112);
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

        private void AddMainActionButtons()
        {
            Button presetsButton = CreateMainActionButton("프리셋", 12, 194, 94);
            presetsButton.Click += (s, e) => ShowPresetManager();

            Button hotkeysButton = CreateMainActionButton("단축키", 112, 194, 94);
            hotkeysButton.Click += (s, e) => ShowHotkeySettings();

            Button recoverButton = CreateMainActionButton("화면으로 모으기", 212, 194, 140);
            recoverButton.Click += (s, e) => MoveOverlaysToCurrentScreen();

            Controls.Add(presetsButton);
            Controls.Add(hotkeysButton);
            Controls.Add(recoverButton);
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

        private void ApplyControlStyle(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label)
                {
                    control.ForeColor = TextColor;
                    control.Font = new Font("맑은 고딕", 9F, FontStyle.Regular);
                }
                else if (control is TextBox textBox)
                {
                    textBox.BackColor = FieldBackColor;
                    textBox.ForeColor = TextColor;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.Font = new Font("맑은 고딕", 9F);
                }
                else if (control is Button button)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.BackColor = AccentColor;
                    button.ForeColor = Color.White;
                    button.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
                    button.Height = 32;
                    button.Cursor = Cursors.Hand;
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.ForeColor = TextColor;
                    checkBox.Font = new Font("맑은 고딕", 9F);
                }
                else if (control is NumericUpDown numeric)
                {
                    numeric.BackColor = FieldBackColor;
                    numeric.ForeColor = TextColor;
                    numeric.Font = new Font("맑은 고딕", 9F);
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

            dgvItems.BackgroundColor = AppBackColor;
            dgvItems.BorderStyle = BorderStyle.None;
            dgvItems.EnableHeadersVisualStyles = false;
            dgvItems.ColumnHeadersDefaultCellStyle.BackColor = PanelBackColor;
            dgvItems.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            dgvItems.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            dgvItems.ColumnHeadersDefaultCellStyle.SelectionBackColor = PanelBackColor;
            dgvItems.DefaultCellStyle.BackColor = FieldBackColor;
            dgvItems.DefaultCellStyle.ForeColor = TextColor;
            dgvItems.DefaultCellStyle.SelectionBackColor = AccentDarkColor;
            dgvItems.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvItems.DefaultCellStyle.Font = new Font("맑은 고딕", 9F);
            dgvItems.RowHeadersVisible = false;
            dgvItems.GridColor = Color.FromArgb(196, 224, 238);
            dgvItems.AllowUserToResizeRows = false;
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

        private void ShowMainForm()
        {
            Show();

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();
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

        private void ExitApplication()
        {
            isRealExit = true;
            SaveConfig();
            UnregisterHotkeys();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            foreach (OverlayForm form in new List<OverlayForm>(overlayForms.Values))
            {
                form.Close();
            }

            overlayForms.Clear();
            Application.Exit();
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

        private void SelectItem(OverlayItemConfig item)
        {
            Size = new Size(550, 400);
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
