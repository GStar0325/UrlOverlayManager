using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace UrlOverlayManager
{
    public partial class OverlayForm : Form
    {
        public event Action? ConfigChanged;
        private readonly OverlayItemConfig config;

        private WebView2 webView = null!;
        private Panel movePanel = null!;
        private Button btnClose = null!;
        private System.Windows.Forms.Timer hoverTimer = null!;
        private readonly List<Panel> resizeHandles = new List<Panel>();
        private bool editChromeVisible;
        private const int MOVE_PANEL_HEIGHT = 28;

        private string currentUrl = "";

        private const int WM_NCHITTEST = 0x84;

        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int RESIZE_HANDLE_SIZE = 8;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private static readonly Color TransparentKeyColor = Color.FromArgb(1, 2, 3);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);


        public OverlayForm(OverlayItemConfig config)
        {
            InitializeComponent();

            this.config = config;

            InitOverlayForm();
            InitControls();
        }

        private void InitOverlayForm()
        {
            this.Text = BuildWindowTitle();

            this.StartPosition = FormStartPosition.Manual;

            this.Left = config.X;
            this.Top = config.Y;
            this.Width = config.Width;
            this.Height = config.Height;

            // 오버레이 프로그램 목적상 항상 위 고정
            this.TopMost = true;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;

            this.Opacity = config.Opacity;

            this.BackColor = TransparentKeyColor;
            this.TransparencyKey = TransparentKeyColor;

            this.MinimumSize = new Size(150, 100);
            this.FormBorderStyle = FormBorderStyle.None;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyConfig();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetClickThrough(config.ClickThrough);
        }

        private async void ApplyPageStyle()
        {
            if (webView.CoreWebView2 == null)
                return;

            string script = @"
        document.documentElement.style.overflow = 'hidden';
        document.documentElement.style.background = 'transparent';
        document.body.style.overflow = 'hidden';
        document.body.style.margin = '0';
        document.body.style.padding = '0';
        document.body.style.width = '100vw';
        document.body.style.height = '100vh';
        document.body.style.background = 'transparent';
    ";

            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        private void InitControls()
        {
            // 웹뷰는 창 전체를 채움
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.Transparent;

            this.Controls.Add(webView);

            // 상단 이동바
            movePanel = new Panel();
            movePanel.Height = MOVE_PANEL_HEIGHT;
            movePanel.Width = this.ClientSize.Width;
            movePanel.Left = 0;
            movePanel.Top = 0;
            movePanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            movePanel.BackColor = Color.FromArgb(116, 174, 207);
            movePanel.Visible = false;
            movePanel.MouseDown += MovePanel_MouseDown;

            // 닫기 버튼
            btnClose = new Button();
            btnClose.Text = "X";
            btnClose.Width = 40;
            btnClose.Height = MOVE_PANEL_HEIGHT;
            btnClose.Dock = DockStyle.Right;
            btnClose.Click += BtnClose_Click;

            movePanel.Controls.Add(btnClose);

            this.Controls.Add(movePanel);
            movePanel.BringToFront();
            InitResizeHandles();


            // 마우스 위치 감지용 타이머
            hoverTimer = new System.Windows.Forms.Timer();
            hoverTimer.Interval = 100;
            hoverTimer.Tick += HoverTimer_Tick;
            hoverTimer.Start();

            this.Load += OverlayForm_Load;
            this.Move += OverlayForm_LocationSizeChanged;
            this.Resize += OverlayForm_LocationSizeChanged;
        }
        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            UpdateMovePanelVisibility();
        }
        private void UpdateMovePanelVisibility()
        {
            if (movePanel == null)
                return;

            // 클릭무시 상태에서는 이동바를 절대 표시하지 않음
            if (config.ClickThrough)
            {
                HideEditChrome();
                return;
            }

            // 현재 마우스 위치가 이 오버레이 창 안에 있는지 확인
            bool mouseInside = this.Bounds.Contains(Cursor.Position);

            if (mouseInside)
                ShowEditChrome();
            else
                HideEditChrome();
        }

        private void InitResizeHandles()
        {
            AddResizeHandle(HTLEFT, Cursors.SizeWE);
            AddResizeHandle(HTRIGHT, Cursors.SizeWE);
            AddResizeHandle(HTTOP, Cursors.SizeNS);
            AddResizeHandle(HTBOTTOM, Cursors.SizeNS);
            AddResizeHandle(HTTOPLEFT, Cursors.SizeNWSE);
            AddResizeHandle(HTTOPRIGHT, Cursors.SizeNESW);
            AddResizeHandle(HTBOTTOMLEFT, Cursors.SizeNESW);
            AddResizeHandle(HTBOTTOMRIGHT, Cursors.SizeNWSE);

            UpdateResizeHandleBounds();
        }

        private void AddResizeHandle(int hitTestValue, Cursor cursor)
        {
            Panel handle = new Panel();
            handle.Tag = hitTestValue;
            handle.Cursor = cursor;
            handle.BackColor = Color.Transparent;
            handle.Visible = false;
            handle.MouseDown += ResizeHandle_MouseDown;

            resizeHandles.Add(handle);
            Controls.Add(handle);
            handle.BringToFront();
        }

        private void UpdateResizeHandleBounds()
        {
            foreach (Panel handle in resizeHandles)
            {
                int hitTestValue = handle.Tag is int value ? value : HTCLIENT;

                switch (hitTestValue)
                {
                    case HTLEFT:
                        handle.Bounds = new Rectangle(0, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, ClientSize.Height - RESIZE_HANDLE_SIZE * 2);
                        break;
                    case HTRIGHT:
                        handle.Bounds = new Rectangle(ClientSize.Width - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, ClientSize.Height - RESIZE_HANDLE_SIZE * 2);
                        break;
                    case HTTOP:
                        handle.Bounds = new Rectangle(RESIZE_HANDLE_SIZE, 0, ClientSize.Width - RESIZE_HANDLE_SIZE * 2, RESIZE_HANDLE_SIZE);
                        break;
                    case HTBOTTOM:
                        handle.Bounds = new Rectangle(RESIZE_HANDLE_SIZE, ClientSize.Height - RESIZE_HANDLE_SIZE, ClientSize.Width - RESIZE_HANDLE_SIZE * 2, RESIZE_HANDLE_SIZE);
                        break;
                    case HTTOPLEFT:
                        handle.Bounds = new Rectangle(0, 0, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
                        break;
                    case HTTOPRIGHT:
                        handle.Bounds = new Rectangle(ClientSize.Width - RESIZE_HANDLE_SIZE, 0, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
                        break;
                    case HTBOTTOMLEFT:
                        handle.Bounds = new Rectangle(0, ClientSize.Height - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
                        break;
                    case HTBOTTOMRIGHT:
                        handle.Bounds = new Rectangle(ClientSize.Width - RESIZE_HANDLE_SIZE, ClientSize.Height - RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE);
                        break;
                }

                handle.BringToFront();
            }

            movePanel?.BringToFront();
        }

        private void ShowEditChrome()
        {
            if (config.ClickThrough)
            {
                HideEditChrome(detachControls: true);
                return;
            }

            EnsureEditChromeAttached();

            if (editChromeVisible)
                return;

            movePanel.Visible = true;
            movePanel.BringToFront();

            UpdateResizeHandleBounds();

            foreach (Panel handle in resizeHandles)
            {
                handle.Visible = true;
                handle.BringToFront();
            }

            movePanel.BringToFront();
            editChromeVisible = true;
            Invalidate();
        }

        private void HideEditChrome(bool detachControls = false)
        {
            if (movePanel != null)
            {
                movePanel.Visible = false;
            }

            foreach (Panel handle in resizeHandles)
            {
                handle.Visible = false;
            }

            editChromeVisible = false;
            Invalidate();

            if (detachControls)
                DetachEditChrome();
        }

        private void EnsureEditChromeAttached()
        {
            if (movePanel != null && !Controls.Contains(movePanel))
            {
                Controls.Add(movePanel);
            }

            foreach (Panel handle in resizeHandles)
            {
                if (!Controls.Contains(handle))
                    Controls.Add(handle);
            }
        }

        private void DetachEditChrome()
        {
            if (movePanel != null && Controls.Contains(movePanel))
            {
                Controls.Remove(movePanel);
            }

            foreach (Panel handle in resizeHandles)
            {
                if (Controls.Contains(handle))
                    Controls.Remove(handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (config.ClickThrough)
                return;

            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            {
                int x = unchecked((short)(long)m.LParam);
                int y = unchecked((short)((long)m.LParam >> 16));
                Point screenPoint = new Point(x, y);

                Point clientPoint = this.PointToClient(screenPoint);

                bool left = clientPoint.X <= RESIZE_HANDLE_SIZE;
                bool right = clientPoint.X >= this.ClientSize.Width - RESIZE_HANDLE_SIZE;
                bool top = clientPoint.Y <= RESIZE_HANDLE_SIZE;
                bool bottom = clientPoint.Y >= this.ClientSize.Height - RESIZE_HANDLE_SIZE;

                if (left && top)
                    m.Result = (IntPtr)HTTOPLEFT;
                else if (right && top)
                    m.Result = (IntPtr)HTTOPRIGHT;
                else if (left && bottom)
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (right && bottom)
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left)
                    m.Result = (IntPtr)HTLEFT;
                else if (right)
                    m.Result = (IntPtr)HTRIGHT;
                else if (top)
                    m.Result = (IntPtr)HTTOP;
                else if (bottom)
                    m.Result = (IntPtr)HTBOTTOM;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!editChromeVisible || config.ClickThrough)
                return;

            using Pen borderPen = new Pen(Color.FromArgb(116, 174, 207), 2F);
            Rectangle border = new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
            e.Graphics.DrawRectangle(borderPen, border);
        }

        private async void OverlayForm_Load(object? sender, EventArgs e)
        {
            await webView.EnsureCoreWebView2Async();

            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            ApplyConfig();
        }
        private void CoreWebView2_NavigationCompleted(
    object? sender,
    Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            ApplyPageStyle();
        }
        private void Navigate()
        {
            if (webView.CoreWebView2 == null)
                return;

            string targetUrl = NormalizeUrl(config.Url);

            if (string.IsNullOrWhiteSpace(targetUrl))
                return;

            currentUrl = targetUrl;

            if (ClockOverlayContent.IsClockUrl(targetUrl))
            {
                webView.NavigateToString(ClockOverlayContent.CreateHtml());
                return;
            }

            webView.CoreWebView2.Navigate(targetUrl);
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            config.Visible = false;

            this.Hide();

            ConfigChanged?.Invoke();
        }

        private void OverlayForm_LocationSizeChanged(object? sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Normal)
                return;

            config.X = this.Left;
            config.Y = this.Top;
            config.Width = this.Width;
            config.Height = this.Height;

            if (movePanel != null)
            {
                movePanel.Width = this.ClientSize.Width;
            }

            UpdateResizeHandleBounds();
            ConfigChanged?.Invoke();
        }


        private void SetClickThrough(bool enabled)
        {
            if (!this.IsHandleCreated)
                return;

            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);

            if (enabled)
            {
                HideEditChrome(detachControls: true);

                exStyle |= WS_EX_LAYERED;
                exStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
                EnsureEditChromeAttached();
            }

            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
            Refresh();
        }
        public void ApplyConfig()
        {
            this.Text = BuildWindowTitle();
            this.Opacity = config.Opacity;
            this.TopMost = true;

            Rectangle targetBounds = new Rectangle(config.X, config.Y, config.Width, config.Height);

            if (this.Bounds != targetBounds)
            {
                this.Bounds = targetBounds;
            }

            SetClickThrough(config.ClickThrough);

            UpdateMovePanelVisibility();

            string targetUrl = NormalizeUrl(config.Url);

            if (currentUrl != targetUrl)
            {
                Navigate();
            }
        }
        private string BuildWindowTitle()
        {
            string displayName = string.IsNullOrWhiteSpace(config.Name) ? "이름 없음" : config.Name.Trim();
            return "UrlOverlayManager Web Overlay " + config.Id.ToString("N") + " - " + displayName;
        }

        private string NormalizeUrl(string url)
        {
            string targetUrl = url.Trim();

            if (string.IsNullOrWhiteSpace(targetUrl))
                return "";

            if (ClockOverlayContent.IsClockUrl(targetUrl))
                return ClockOverlayContent.Url;

            if (targetUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return targetUrl;

            if (System.IO.Path.IsPathFullyQualified(targetUrl) && System.IO.File.Exists(targetUrl))
                return new Uri(targetUrl).AbsoluteUri;

            if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = "https://" + targetUrl;
            }

            return targetUrl;
        }

        private void MovePanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (config.ClickThrough)
                return;

            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void ResizeHandle_MouseDown(object? sender, MouseEventArgs e)
        {
            if (config.ClickThrough)
                return;

            if (e.Button != MouseButtons.Left)
                return;

            if (sender is not Panel handle || handle.Tag is not int hitTestValue)
                return;

            ReleaseCapture();
            SendMessage(this.Handle, WM_NCLBUTTONDOWN, hitTestValue, 0);
        }

        #region 창 드래그 이동 처리

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (hoverTimer != null)
            {
                hoverTimer.Stop();
                hoverTimer.Dispose();
            }

            base.OnFormClosed(e);
        }
    }
}
