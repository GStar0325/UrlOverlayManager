using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class BroadcastToolboxForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly Dictionary<string, Form> pageForms = new Dictionary<string, Form>();
        private readonly List<ToolPage> toolPages;

        private Form1? overlayManagerForm;
        private LyricsGeneratorForm? lyricsGeneratorForm;
        private UiSettings settings = UiSettingsStore.Load();
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private Panel? contentHost;
        private FlowLayoutPanel? tabStrip;
        private string activePageKey = "";
        private bool isRealExit;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public BroadcastToolboxForm()
        {
            UiSettingsStore.Apply(settings);
            toolPages = CreateToolPages();
            InitializeForm();
            BuildLayout();
            InitTrayIcon();
            InitializeLyricsOverlay();
            ShowPage(toolPages[0].Key);
        }

        private void InitializeLyricsOverlay()
        {
            Form form = CreateLyricsGenerator();
            pageForms["lyrics"] = form;
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
        }

        private void InitializeForm()
        {
            Text = "방송 도구";
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1240, 780);
            MinimumSize = new Size(980, 640);
            FormBorderStyle = FormBorderStyle.None;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ShowIcon = false;
            ApplyWindowChrome();
        }

        private void ApplyWindowChrome()
        {
            Padding = settings.ShowMainWindowBorder ? new Padding(1) : Padding.Empty;
            BackColor = settings.ShowMainWindowBorder ? UiTheme.BorderColor : UiTheme.AppBackColor;
            ForeColor = UiTheme.TextColor;
            Font = UiTheme.RegularFont();
        }

        private static List<ToolPage> CreateToolPages()
        {
            return new List<ToolPage>
            {
                new ToolPage("overlays", "오버레이"),
                new ToolPage("lyrics", "가사")
            };
        }

        private void BuildLayout()
        {
            DetachHostedPages();
            Controls.Clear();

            Panel surface = new Panel();
            surface.Dock = DockStyle.Fill;
            surface.Padding = new Padding(10);
            surface.BackColor = UiTheme.AppBackColor;
            surface.MouseDown += DragWindow;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.MouseDown += DragWindow;

            root.Controls.Add(CreateTopBar(), 0, 0);
            root.Controls.Add(CreateTabStrip(), 0, 1);
            root.Controls.Add(CreateContentPanel(), 0, 2);

            surface.Controls.Add(root);
            Controls.Add(surface);
            UiTheme.ApplyFonts(this);
            RefreshNavigationButtons();
        }

        private Panel CreateTopBar()
        {
            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Fill;
            topBar.MouseDown += DragWindow;

            FlowLayoutPanel topButtons = new FlowLayoutPanel();
            topButtons.Dock = DockStyle.Right;
            topButtons.FlowDirection = FlowDirection.RightToLeft;
            topButtons.WrapContents = false;
            topButtons.Width = 120;
            topButtons.BackColor = UiTheme.AppBackColor;

            Button closeButton = CreateTopButton("X");
            closeButton.Click += (s, e) => Close();

            Button minimizeButton = CreateTopButton("-");
            minimizeButton.Click += (s, e) => WindowState = FormWindowState.Minimized;

            Button settingsButton = new GearButton();
            settingsButton.Click += SettingsButton_Click;

            topButtons.Controls.Add(closeButton);
            topButtons.Controls.Add(minimizeButton);
            topButtons.Controls.Add(settingsButton);
            topBar.Controls.Add(topButtons);

            return topBar;
        }

        private Control CreateTabStrip()
        {
            Panel frame = new Panel();
            frame.Dock = DockStyle.Fill;
            frame.BackColor = UiTheme.PanelBackColor;

            tabStrip = new FlowLayoutPanel();
            tabStrip.Dock = DockStyle.Fill;
            tabStrip.FlowDirection = FlowDirection.LeftToRight;
            tabStrip.WrapContents = false;
            tabStrip.AutoScroll = false;
            tabStrip.Padding = new Padding(0, 2, 0, 0);
            tabStrip.BackColor = UiTheme.PanelBackColor;

            foreach (ToolPage page in toolPages)
            {
                tabStrip.Controls.Add(CreateNavigationButton(page));
            }

            frame.Controls.Add(tabStrip);
            return frame;
        }

        private Control CreateContentPanel()
        {
            Panel frame = new Panel();
            frame.Dock = DockStyle.Fill;
            frame.Padding = new Padding(1);
            frame.BackColor = UiTheme.BorderColor;

            contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = UiTheme.AppBackColor;
            frame.Controls.Add(contentHost);
            return frame;
        }

        private Button CreateTopButton(string text)
        {
            return new TopBarButton(text);
        }

        private Button CreateNavigationButton(ToolPage page)
        {
            NavigationButton button = new NavigationButton(page.MenuText);
            button.Tag = page.Key;
            button.Click += (s, e) => ShowPage(page.Key);
            return button;
        }

        private void InitTrayIcon()
        {
            trayMenu = new ContextMenuStrip();

            ToolStripMenuItem openItem = new ToolStripMenuItem("열기");
            openItem.Click += (s, e) => ShowMainForm();
            trayMenu.Items.Add(openItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("종료");
            exitItem.Click += (s, e) => ExitApplication();
            trayMenu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "URL Overlay Manager";
            trayIcon.Icon = Icon ?? SystemIcons.Application;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => ShowMainForm();
        }

        private void SettingsButton_Click(object? sender, EventArgs e)
        {
            using UiSettingsForm form = new UiSettingsForm(settings);

            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            settings = form.Result;
            UiSettingsStore.Save(settings);
            UiSettingsStore.Apply(settings);
            ApplyWindowChrome();
            overlayManagerForm?.ApplyCurrentUiSettings();
            lyricsGeneratorForm?.ApplyCurrentUiSettings();
            BuildLayout();
            ShowPage(string.IsNullOrWhiteSpace(activePageKey) ? toolPages[0].Key : activePageKey);
        }

        private void ShowPage(string key)
        {
            ToolPage? page = toolPages.Find(item => item.Key == key);

            if (page == null || contentHost == null)
                return;

            Form form = GetOrCreatePageForm(key);

            foreach (Control control in contentHost.Controls)
            {
                control.Visible = false;
            }

            if (form.Parent != contentHost)
            {
                form.TopLevel = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.MinimumSize = Size.Empty;
                form.Dock = DockStyle.Fill;
                contentHost.Controls.Add(form);
            }

            activePageKey = key;
            form.Visible = true;
            form.Show();
            form.BringToFront();
            RefreshNavigationButtons();
        }

        private Form GetOrCreatePageForm(string key)
        {
            if (pageForms.TryGetValue(key, out Form? existingForm) && !existingForm.IsDisposed)
                return existingForm;

            Form form = key switch
            {
                "overlays" => CreateOverlayManager(),
                "lyrics" => CreateLyricsGenerator(),
                _ => throw new InvalidOperationException("Unknown page key: " + key)
            };

            pageForms[key] = form;
            return form;
        }

        private Form1 CreateOverlayManager()
        {
            overlayManagerForm = new Form1(
                useTrayIntegration: false,
                preserveOverlaysOnClose: true,
                hostedMode: true);
            overlayManagerForm.FormClosed += (s, e) =>
            {
                overlayManagerForm = null;
                pageForms.Remove("overlays");
            };
            return overlayManagerForm;
        }

        private LyricsGeneratorForm CreateLyricsGenerator()
        {
            lyricsGeneratorForm = new LyricsGeneratorForm(hostedMode: true);
            lyricsGeneratorForm.FormClosed += (s, e) =>
            {
                lyricsGeneratorForm = null;
                pageForms.Remove("lyrics");
            };
            return lyricsGeneratorForm;
        }

        private void RefreshNavigationButtons()
        {
            if (tabStrip == null)
                return;

            foreach (Control control in tabStrip.Controls)
            {
                if (control is not Button button)
                    continue;

                bool selected = string.Equals(button.Tag as string, activePageKey, StringComparison.Ordinal);

                if (button is NavigationButton navigationButton)
                {
                    navigationButton.Selected = selected;
                }
                else
                {
                    button.BackColor = selected ? UiTheme.AccentColor : UiTheme.PanelBackColor;
                    button.ForeColor = selected ? Color.White : UiTheme.TextColor;
                    button.FlatAppearance.BorderColor = selected ? UiTheme.AccentDarkColor : UiTheme.BorderColor;
                }
            }
        }

        private void DetachHostedPages()
        {
            if (contentHost == null)
                return;

            foreach (Form form in pageForms.Values)
            {
                if (form.Parent == contentHost)
                {
                    contentHost.Controls.Remove(form);
                }
            }
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        private void ExitApplication()
        {
            if (isRealExit)
                return;

            isRealExit = true;

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }

            trayMenu?.Dispose();
            trayMenu = null;

            overlayManagerForm?.PrepareForApplicationExit();
            CloseHostedPages();
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!isRealExit)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            overlayManagerForm?.PrepareForApplicationExit();
            CloseHostedPages();

            base.OnFormClosing(e);
        }

        private void CloseHostedPages()
        {
            foreach (Form form in new List<Form>(pageForms.Values))
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                }
            }

            pageForms.Clear();
        }

        private sealed class ToolPage
        {
            public ToolPage(string key, string menuText)
            {
                Key = key;
                MenuText = menuText;
            }

            public string Key { get; }
            public string MenuText { get; }
        }

        private sealed class NavigationButton : Button
        {
            private readonly string label;
            private bool isHovering;
            private bool selected;

            public NavigationButton(string label)
            {
                this.label = label;
                Width = 112;
                Height = 29;
                Margin = new Padding(0, 0, 1, 0);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = UiTheme.AppBackColor;
                Cursor = Cursors.Hand;
                TabStop = true;
                Text = "";
            }

            public bool Selected
            {
                get => selected;
                set
                {
                    selected = value;
                    Invalidate();
                }
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                isHovering = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                isHovering = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                Graphics graphics = pevent.Graphics;
                graphics.Clear(Parent?.BackColor ?? UiTheme.AppBackColor);

                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                Color backColor = selected
                    ? UiTheme.AppBackColor
                    : isHovering ? UiTheme.FieldBackColor : UiTheme.PanelBackColor;
                Color textColor = selected ? UiTheme.TextColor : UiTheme.MutedTextColor;

                using SolidBrush backgroundBrush = new SolidBrush(backColor);
                using Pen borderPen = new Pen(UiTheme.BorderColor);
                graphics.FillRectangle(backgroundBrush, bounds);
                graphics.DrawLine(borderPen, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
                graphics.DrawLine(borderPen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
                graphics.DrawLine(borderPen, bounds.Right, bounds.Top, bounds.Right, bounds.Bottom);

                if (selected)
                {
                    using SolidBrush stripBrush = new SolidBrush(UiTheme.AccentColor);
                    graphics.FillRectangle(stripBrush, 0, 0, Width, 2);
                }

                using Font font = selected ? UiTheme.BoldFont(9F) : UiTheme.RegularFont(9F);
                using SolidBrush textBrush = new SolidBrush(textColor);
                using StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                Rectangle textBounds = new Rectangle(8, 1, Width - 16, Height - 2);
                graphics.DrawString(label, font, textBrush, textBounds, format);
            }
        }

        private sealed class GearButton : Button
        {
            public GearButton()
            {
                Size = new Size(36, 32);
                Margin = new Padding(6, 2, 0, 2);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = UiTheme.AppBackColor;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                Graphics graphics = pevent.Graphics;
                graphics.Clear(BackColor);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                PointF center = new PointF(Width / 2F, Height / 2F);
                using System.Drawing.Drawing2D.GraphicsPath gearPath = new System.Drawing.Drawing2D.GraphicsPath();
                using SolidBrush gearBrush = new SolidBrush(UiTheme.MutedTextColor);
                using SolidBrush holeBrush = new SolidBrush(BackColor);

                const int toothCount = 8;
                const float outerRadius = 10.5F;
                const float innerRadius = 7.5F;

                PointF[] points = new PointF[toothCount * 2];

                for (int i = 0; i < points.Length; i++)
                {
                    double angle = -Math.PI / 2 + Math.PI * 2 * i / points.Length;
                    float radius = i % 2 == 0 ? outerRadius : innerRadius;
                    points[i] = new PointF(
                        center.X + (float)Math.Cos(angle) * radius,
                        center.Y + (float)Math.Sin(angle) * radius);
                }

                gearPath.AddPolygon(points);
                graphics.FillPath(gearBrush, gearPath);
                graphics.FillEllipse(holeBrush, center.X - 3.5F, center.Y - 3.5F, 7F, 7F);
            }
        }

        private sealed class TopBarButton : Button
        {
            private readonly string symbol;
            private bool isHovering;

            public TopBarButton(string symbol)
            {
                this.symbol = symbol;
                Size = new Size(36, 32);
                Margin = new Padding(6, 2, 0, 2);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = UiTheme.AppBackColor;
                Cursor = Cursors.Hand;
                TabStop = false;
                Text = "";
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                isHovering = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                isHovering = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                Graphics graphics = pevent.Graphics;
                graphics.Clear(isHovering ? UiTheme.FieldBackColor : UiTheme.AppBackColor);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using Font font = UiTheme.BoldFont(11F);
                using SolidBrush brush = new SolidBrush(UiTheme.MutedTextColor);
                using StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                graphics.DrawString(symbol, font, brush, ClientRectangle, format);
            }
        }
    }
}
