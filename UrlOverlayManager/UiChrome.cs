using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public static class UiChrome
    {
        private const int TitleBarHeight = 42;
        private const int TitleButtonWidth = 46;
        private const int TitleButtonHeight = 38;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public static void Apply(Form form, string title, bool showMinimize)
        {
            if (form.Tag is string tag && tag == "UiChromeApplied")
                return;

            List<Control> existingControls = new List<Control>();

            foreach (Control control in form.Controls)
            {
                existingControls.Add(control);
            }

            form.Controls.Clear();
            form.Tag = "UiChromeApplied";
            form.Text = title;
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Padding = new Padding(1);
            form.BackColor = UiTheme.BorderColor;
            form.ForeColor = UiTheme.TextColor;
            form.Font = UiTheme.RegularFont();
            form.ShowIcon = false;

            Panel surface = new Panel();
            surface.Dock = DockStyle.Fill;
            surface.BackColor = UiTheme.AppBackColor;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel titleBar = CreateTitleBar(form, title, showMinimize);

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = UiTheme.AppBackColor;

            foreach (Control control in existingControls)
            {
                content.Controls.Add(control);
            }

            root.Controls.Add(titleBar, 0, 0);
            root.Controls.Add(content, 0, 1);
            surface.Controls.Add(root);
            form.Controls.Add(surface);
            UiTheme.ApplyFonts(form);
        }

        private static Panel CreateTitleBar(Form form, string title, bool showMinimize)
        {
            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Fill;
            titleBar.BackColor = UiTheme.AppBackColor;
            titleBar.MouseDown += (s, e) => DragWindow(form, e);

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Padding = new Padding(12, 0, 0, 0);
            titleLabel.ForeColor = UiTheme.TextColor;
            titleLabel.Font = UiTheme.BoldFont();
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.MouseDown += (s, e) => DragWindow(form, e);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Width = showMinimize ? 104 : 52;
            buttons.BackColor = UiTheme.AppBackColor;

            Button closeButton = CreateTitleButton("X");
            closeButton.Click += (s, e) => form.Close();
            buttons.Controls.Add(closeButton);

            if (showMinimize)
            {
                Button minimizeButton = CreateTitleButton("_");
                minimizeButton.Click += (s, e) => form.WindowState = FormWindowState.Minimized;
                buttons.Controls.Add(minimizeButton);
            }

            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(buttons);

            return titleBar;
        }

        private static Button CreateTitleButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(TitleButtonWidth, TitleButtonHeight);
            button.Margin = new Padding(4, 2, 0, 2);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = UiTheme.AppBackColor;
            button.ForeColor = UiTheme.MutedTextColor;
            button.Font = UiTheme.BoldFont(11F);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static void DragWindow(Form form, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }
}
