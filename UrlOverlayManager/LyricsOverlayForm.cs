using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class LyricsOverlayForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private static readonly Color TransparentKeyColor = Color.FromArgb(1, 2, 3);
        public event Action<int>? PageChanged;
        public event Action? OverlayStateChanged;

        private string lyricsText = "";
        private string[] pages = Array.Empty<string>();
        private string lyricFontFamily = UiTheme.AppFontFamilyName;
        private int currentPageIndex;
        private readonly ContextMenuStrip overlayMenu = new ContextMenuStrip();
        private bool clickThrough;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        public LyricsOverlayForm(UiSettings settings)
        {
            Text = "UrlOverlayManager Lyrics Overlay";
            StartPosition = FormStartPosition.Manual;
            Bounds = ResolveBounds(settings);
            MinimumSize = new Size(640, 140);
            TopMost = true;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = TransparentKeyColor;
            TransparencyKey = TransparentKeyColor;
            DoubleBuffered = true;
            Font = UiTheme.BoldFont(34F);
            Cursor = Cursors.SizeAll;
            KeyPreview = true;
            clickThrough = settings.LyricsOverlayClickThrough;
            InitOverlayMenu();
            Move += OverlayBoundsChanged;
            Resize += OverlayBoundsChanged;
        }

        private void InitOverlayMenu()
        {
            ToolStripMenuItem clearItem = new ToolStripMenuItem("가사 비우기");
            clearItem.Click += (s, e) => SetPages(Array.Empty<string>(), 0);

            ToolStripMenuItem closeItem = new ToolStripMenuItem("오버레이 닫기");
            closeItem.Click += (s, e) => Close();

            overlayMenu.Items.Add(clearItem);
            overlayMenu.Items.Add(closeItem);
            ContextMenuStrip = overlayMenu;
        }

        public bool ClickThrough => clickThrough;

        public void SetClickThrough(bool enabled)
        {
            bool changed = clickThrough != enabled;
            clickThrough = enabled;
            ApplyClickThrough();

            if (changed)
            {
                OverlayStateChanged?.Invoke();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyClickThrough();
            ApplyClickThroughDeferred();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyClickThrough();
            ApplyClickThroughDeferred();
        }

        private void ApplyClickThroughDeferred()
        {
            if (!IsHandleCreated)
                return;

            BeginInvoke(new Action(ApplyClickThrough));
        }

        private void ApplyClickThrough()
        {
            if (!IsHandleCreated)
                return;

            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);

            if (clickThrough)
            {
                exStyle |= WS_EX_LAYERED;
                exStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }

            SetWindowLong(Handle, GWL_EXSTYLE, exStyle);
            SetWindowPos(
                Handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            Cursor = clickThrough ? Cursors.Default : Cursors.SizeAll;
        }

        private void OverlayBoundsChanged(object? sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
            {
                OverlayStateChanged?.Invoke();
            }
        }

        private static Rectangle ResolveBounds(UiSettings settings)
        {
            int width = Math.Max(640, settings.LyricsOverlayWidth);
            int height = Math.Max(140, settings.LyricsOverlayHeight);
            Rectangle bounds = new Rectangle(settings.LyricsOverlayX, settings.LyricsOverlayY, width, height);

            if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds)))
                return bounds;

            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            return new Rectangle(
                workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2),
                Math.Min(width, workingArea.Width),
                Math.Min(height, workingArea.Height));
        }

        public void SetLyrics(string text)
        {
            lyricsText = text;
            Invalidate();
        }

        public void SetPages(string[] newPages, int pageIndex)
        {
            pages = newPages.ToArray();

            if (pages.Length == 0)
            {
                currentPageIndex = 0;
                SetLyrics("");
                return;
            }

            currentPageIndex = Math.Max(0, Math.Min(pageIndex, pages.Length - 1));
            SetLyrics(pages[currentPageIndex]);
        }

        public void SetLyricFontFamily(string fontFamily)
        {
            if (string.IsNullOrWhiteSpace(fontFamily))
                return;

            lyricFontFamily = fontFamily;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.PageUp)
            {
                MovePage(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Space)
            {
                MovePage(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                MoveToPage(0);
                e.Handled = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            overlayMenu.Dispose();
            base.OnFormClosed(e);
        }

        private void MovePage(int direction)
        {
            if (pages.Length == 0)
                return;

            MoveToPage(currentPageIndex + direction);
        }

        private void MoveToPage(int pageIndex)
        {
            if (pages.Length == 0)
                return;

            int nextIndex = Math.Max(0, Math.Min(pageIndex, pages.Length - 1));

            if (nextIndex == currentPageIndex)
                return;

            currentPageIndex = nextIndex;
            SetLyrics(pages[currentPageIndex]);
            PageChanged?.Invoke(currentPageIndex);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            Rectangle panelBounds = new Rectangle(24, 18, ClientSize.Width - 48, ClientSize.Height - 36);

            if (panelBounds.Width <= 0 || panelBounds.Height <= 0)
                return;

            if (string.IsNullOrWhiteSpace(lyricsText))
                return;

            Rectangle textBounds = Rectangle.Inflate(panelBounds, -36, -18);
            using StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.Trimming = StringTrimming.EllipsisCharacter;

            string[] lines = GetLyricLines(lyricsText);
            using Font drawFont = CreateFittedFont(e.Graphics, lines, textBounds);
            DrawTextWithOutline(e.Graphics, lines, drawFont, textBounds, format);
        }

        private Font CreateFittedFont(Graphics graphics, string[] lines, Rectangle bounds)
        {
            float fontSize = Math.Min(42F, Math.Max(18F, bounds.Height / 3.2F));

            while (fontSize > 16F)
            {
                using Font testFont = CreateSafeFont(lyricFontFamily, fontSize);
                SizeF size = MeasureLyricText(graphics, lines, testFont, bounds.Width);

                if (size.Height <= bounds.Height && size.Width <= bounds.Width + 8)
                    break;

                fontSize -= 1F;
            }

            return CreateSafeFont(lyricFontFamily, fontSize);
        }

        private static string[] GetLyricLines(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
        }

        private static SizeF MeasureLyricText(Graphics graphics, string[] lines, Font font, int width)
        {
            if (lines.Length == 0)
                return SizeF.Empty;

            float maxWidth = 0F;

            foreach (string line in lines)
            {
                SizeF lineSize = graphics.MeasureString(line, font, width);
                maxWidth = Math.Max(maxWidth, lineSize.Width);
            }

            float lineHeight = font.GetHeight(graphics);
            float gap = GetLineGap(font, lines.Length);
            float height = lineHeight * lines.Length + gap * Math.Max(0, lines.Length - 1);
            return new SizeF(maxWidth, height);
        }

        private static float GetLineGap(Font font, int lineCount)
        {
            return lineCount >= 3 ? font.GetHeight() * 0.32F : 0F;
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

        private static void DrawTextWithOutline(Graphics graphics, string[] lines, Font font, Rectangle bounds, StringFormat format)
        {
            if (lines.Length == 0)
                return;

            if (lines.Length == 1)
            {
                DrawTextLineWithOutline(graphics, lines[0], font, bounds, format);
                return;
            }

            float lineHeight = font.GetHeight(graphics);
            float gap = GetLineGap(font, lines.Length);
            float totalHeight = lineHeight * lines.Length + gap * (lines.Length - 1);
            float top = bounds.Top + (bounds.Height - totalHeight) / 2F;

            using StringFormat lineFormat = new StringFormat();
            lineFormat.Alignment = StringAlignment.Center;
            lineFormat.LineAlignment = StringAlignment.Center;
            lineFormat.Trimming = StringTrimming.EllipsisCharacter;

            for (int i = 0; i < lines.Length; i++)
            {
                RectangleF lineBounds = new RectangleF(
                    bounds.Left,
                    top + i * (lineHeight + gap),
                    bounds.Width,
                    lineHeight);

                DrawTextLineWithOutline(graphics, lines[i], font, Rectangle.Round(lineBounds), lineFormat);
            }
        }

        private static void DrawTextLineWithOutline(Graphics graphics, string text, Font font, Rectangle bounds, StringFormat format)
        {
            try
            {
                using GraphicsPath textPath = new GraphicsPath();
                using Pen outlinePen = new Pen(Color.Black, 7F);
                using SolidBrush textBrush = new SolidBrush(Color.White);

                outlinePen.LineJoin = LineJoin.Round;
                textPath.AddString(
                    text,
                    font.FontFamily,
                    (int)font.Style,
                    graphics.DpiY * font.SizeInPoints / 72F,
                    bounds,
                    format);

                graphics.DrawPath(outlinePen, textPath);
                graphics.FillPath(textBrush, textPath);
            }
            catch
            {
                DrawTextWithOffsetOutline(graphics, text, font, bounds, format);
            }
        }

        private static void DrawTextWithOffsetOutline(Graphics graphics, string text, Font font, Rectangle bounds, StringFormat format)
        {
            using SolidBrush outlineBrush = new SolidBrush(Color.Black);
            using SolidBrush textBrush = new SolidBrush(Color.White);

            for (int x = -3; x <= 3; x += 3)
            {
                for (int y = -3; y <= 3; y += 3)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Rectangle offsetBounds = bounds;
                    offsetBounds.Offset(x, y);
                    graphics.DrawString(text, font, outlineBrush, offsetBounds, format);
                }
            }

            graphics.DrawString(text, font, textBrush, bounds, format);
        }
    }
}
