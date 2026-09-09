using System;
using System.Drawing;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class UiSettingsForm : Form
    {
        private readonly FlowLayoutPanel themePanel = new FlowLayoutPanel();
        private readonly Button btnSystemFont = new NoFocusButton();
        private readonly Button btnAppFont = new NoFocusButton();
        private readonly CheckBox chkShowBorder = new CheckBox();
        private string selectedThemeName = "Light";
        private string selectedFontMode = UiTheme.AppFontMode;

        public UiSettings Result { get; private set; }

        public UiSettingsForm(UiSettings settings)
        {
            Result = new UiSettings
            {
                ThemeName = settings.ThemeName,
                FontMode = settings.FontMode,
                ShowMainWindowBorder = settings.ShowMainWindowBorder,
                LyricsSearchDirectory = settings.LyricsSearchDirectory
            };

            selectedThemeName = Result.ThemeName;
            selectedFontMode = Result.FontMode;
            InitializeForm();
            BuildLayout();
            UiChrome.Apply(this, "화면 설정", false);
            LoadValues();
        }

        private void InitializeForm()
        {
            Text = "화면 설정";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(580, 500);
            MinimumSize = new Size(580, 500);
            BackColor = UiTheme.AppBackColor;
            ForeColor = UiTheme.TextColor;
            Font = UiTheme.RegularFont();
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(22);
            root.ColumnCount = 1;
            root.RowCount = 7;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            Label title = new Label();
            title.Text = "화면 설정";
            title.Dock = DockStyle.Fill;
            title.Font = UiTheme.BoldFont(17F);
            title.TextAlign = ContentAlignment.MiddleLeft;

            Label themeLabel = new Label();
            themeLabel.Text = "색 테마";
            themeLabel.Dock = DockStyle.Fill;
            themeLabel.ForeColor = UiTheme.TextColor;
            themeLabel.TextAlign = ContentAlignment.MiddleLeft;

            themePanel.Dock = DockStyle.Fill;
            themePanel.WrapContents = false;
            themePanel.FlowDirection = FlowDirection.LeftToRight;
            themePanel.BackColor = UiTheme.AppBackColor;

            themePanel.Controls.Add(CreateThemeSwatch(
                "Studio Dark",
                "Dark",
                Color.FromArgb(30, 30, 30),
                Color.FromArgb(37, 37, 38),
                Color.FromArgb(0, 122, 204)));
            themePanel.Controls.Add(CreateThemeSwatch(
                "Studio Light",
                "Light",
                Color.FromArgb(243, 243, 243),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(0, 122, 204)));
            themePanel.Controls.Add(CreateThemeSwatch(
                "Mint",
                "Mint",
                Color.FromArgb(242, 249, 247),
                Color.White,
                Color.FromArgb(58, 150, 134)));
            themePanel.Controls.Add(CreateThemeSwatch(
                "Sky",
                "Sky",
                Color.FromArgb(239, 248, 253),
                Color.FromArgb(252, 254, 255),
                Color.FromArgb(104, 174, 214)));

            Label fontLabel = new Label();
            fontLabel.Text = "글꼴";
            fontLabel.Dock = DockStyle.Fill;
            fontLabel.ForeColor = UiTheme.TextColor;
            fontLabel.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel fontPanel = new FlowLayoutPanel();
            fontPanel.Dock = DockStyle.Fill;
            fontPanel.WrapContents = false;
            fontPanel.FlowDirection = FlowDirection.LeftToRight;
            fontPanel.BackColor = UiTheme.AppBackColor;

            ConfigureFontButton(btnSystemFont, "맑은 고딕", UiTheme.SystemFontMode);
            ConfigureFontButton(btnAppFont, "마비옛체", UiTheme.AppFontMode);

            fontPanel.Controls.Add(btnSystemFont);
            fontPanel.Controls.Add(btnAppFont);

            TableLayoutPanel optionArea = new TableLayoutPanel();
            optionArea.Dock = DockStyle.Fill;
            optionArea.ColumnCount = 1;
            optionArea.RowCount = 2;
            optionArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            optionArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            chkShowBorder.Text = "첫 화면 외곽선 표시";
            chkShowBorder.Dock = DockStyle.Fill;
            chkShowBorder.ForeColor = UiTheme.TextColor;

            Label hint = new Label();
            hint.Text = "색 테마와 외곽선 설정은 저장 즉시 첫 화면에 반영됩니다.";
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = UiTheme.MutedTextColor;
            hint.TextAlign = ContentAlignment.TopLeft;

            optionArea.Controls.Add(chkShowBorder, 0, 0);
            optionArea.Controls.Add(hint, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;

            Button okButton = CreateButton("확인", UiTheme.AccentColor, Color.White);
            okButton.Click += OkButton_Click;

            Button cancelButton = CreateButton("취소", UiTheme.PanelBackColor, UiTheme.TextColor);
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttons.Controls.Add(okButton);
            buttons.Controls.Add(cancelButton);

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(themeLabel, 0, 1);
            root.Controls.Add(themePanel, 0, 2);
            root.Controls.Add(fontLabel, 0, 3);
            root.Controls.Add(fontPanel, 0, 4);
            root.Controls.Add(optionArea, 0, 5);
            root.Controls.Add(buttons, 0, 6);

            Controls.Add(root);
        }

        private ThemeSwatchButton CreateThemeSwatch(string label, string themeName, Color backColor, Color panelColor, Color accentColor)
        {
            ThemeSwatchButton button = new ThemeSwatchButton(label, themeName, backColor, panelColor, accentColor);
            button.Margin = new Padding(0, 0, 8, 0);
            button.Click += (s, e) =>
            {
                selectedThemeName = themeName;
                RefreshSwatches();
            };

            return button;
        }

        private static Button CreateButton(string text, Color backColor, Color foreColor)
        {
            Button button = new NoFocusButton();
            button.Text = text;
            button.Width = 86;
            button.Margin = new Padding(8, 0, 0, 0);
            UiTheme.StyleButton(button, backColor, foreColor);
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void ConfigureFontButton(Button button, string text, string fontMode)
        {
            button.Text = text;
            button.Size = new Size(110, 32);
            button.Margin = new Padding(0, 0, 10, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = UiTheme.RegularFont();
            button.Cursor = Cursors.Hand;
            button.Click += (s, e) =>
            {
                selectedFontMode = fontMode;
                RefreshFontButtons();
            };
        }

        private void LoadValues()
        {
            chkShowBorder.Checked = Result.ShowMainWindowBorder;
            selectedFontMode = string.Equals(Result.FontMode, UiTheme.SystemFontMode, StringComparison.OrdinalIgnoreCase)
                ? UiTheme.SystemFontMode
                : UiTheme.AppFontMode;
            RefreshSwatches();
            RefreshFontButtons();
        }

        private void RefreshSwatches()
        {
            foreach (Control control in themePanel.Controls)
            {
                if (control is ThemeSwatchButton swatch)
                {
                    swatch.Selected = string.Equals(swatch.ThemeName, selectedThemeName, StringComparison.OrdinalIgnoreCase);
                    swatch.Invalidate();
                }
            }
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            Result.ThemeName = selectedThemeName;
            Result.FontMode = selectedFontMode;
            Result.ShowMainWindowBorder = chkShowBorder.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void RefreshFontButtons()
        {
            StyleFontButton(btnAppFont, string.Equals(selectedFontMode, UiTheme.AppFontMode, StringComparison.OrdinalIgnoreCase));
            StyleFontButton(btnSystemFont, string.Equals(selectedFontMode, UiTheme.SystemFontMode, StringComparison.OrdinalIgnoreCase));
        }

        private static void StyleFontButton(Button button, bool selected)
        {
            button.BackColor = selected ? UiTheme.AccentColor : UiTheme.PanelBackColor;
            button.ForeColor = selected ? Color.White : UiTheme.TextColor;
        }

        private sealed class NoFocusButton : Button
        {
            protected override bool ShowFocusCues => false;
        }

        private sealed class ThemeSwatchButton : Button
        {
            private readonly string label;
            private readonly Color appColor;
            private readonly Color panelColor;
            private readonly Color accentColor;

            public string ThemeName { get; }
            public bool Selected { get; set; }

            public ThemeSwatchButton(string label, string themeName, Color appColor, Color panelColor, Color accentColor)
            {
                this.label = label;
                this.appColor = appColor;
                this.panelColor = panelColor;
                this.accentColor = accentColor;
                ThemeName = themeName;

                Size = new Size(96, 78);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = UiTheme.AppBackColor;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                Graphics graphics = pevent.Graphics;
                graphics.Clear(Parent?.BackColor ?? UiTheme.AppBackColor);

                Rectangle outer = new Rectangle(1, 1, Width - 3, Height - 3);
                Rectangle swatch = new Rectangle(10, 9, Width - 20, 42);
                Rectangle left = new Rectangle(swatch.Left, swatch.Top, swatch.Width / 2, swatch.Height);
                Rectangle right = new Rectangle(swatch.Left + swatch.Width / 2, swatch.Top, swatch.Width / 2, swatch.Height);
                Rectangle accent = new Rectangle(swatch.Left + 12, swatch.Bottom - 12, swatch.Width - 24, 7);

                using Pen borderPen = new Pen(Selected ? UiTheme.AccentColor : UiTheme.BorderColor, Selected ? 2 : 1);
                using SolidBrush appBrush = new SolidBrush(appColor);
                using SolidBrush panelBrush = new SolidBrush(panelColor);
                using SolidBrush accentBrush = new SolidBrush(accentColor);
                using SolidBrush textBrush = new SolidBrush(UiTheme.TextColor);
                using StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                graphics.FillRectangle(appBrush, left);
                graphics.FillRectangle(panelBrush, right);
                graphics.FillRectangle(accentBrush, accent);
                if (Selected)
                {
                    graphics.DrawRectangle(borderPen, outer);
                }

                graphics.DrawString(label, UiTheme.BoldFont(9F), textBrush, new Rectangle(0, 54, Width, 18), format);
            }
        }
    }
}
