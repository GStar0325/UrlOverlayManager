using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class HotkeySettingsForm : Form
    {
        private readonly HotkeyTextBox txtToggleOverlays = new HotkeyTextBox();
        private readonly HotkeyTextBox txtEditMode = new HotkeyTextBox();
        private readonly HotkeyTextBox txtToggleClickThrough = new HotkeyTextBox();
        private readonly HotkeyTextBox txtMoveToCurrentScreen = new HotkeyTextBox();

        public HotkeySettings Result { get; private set; }

        public HotkeySettingsForm(HotkeySettings settings)
        {
            Result = settings.Clone();
            InitializeForm();
            BuildLayout();
            UiChrome.Apply(this, "단축키 설정", false);
            LoadValues(Result);
        }

        private void InitializeForm()
        {
            Text = "단축키 설정";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 330);
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
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            Label title = new Label();
            title.Text = "단축키 설정";
            title.Dock = DockStyle.Fill;
            title.Font = UiTheme.BoldFont(17F);
            title.TextAlign = ContentAlignment.MiddleLeft;

            TableLayoutPanel fields = new TableLayoutPanel();
            fields.Dock = DockStyle.Fill;
            fields.BackColor = UiTheme.PanelBackColor;
            fields.Padding = new Padding(18);
            fields.ColumnCount = 2;
            fields.RowCount = 4;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(fields, 0, "전체 표시/숨김", txtToggleOverlays);
            AddRow(fields, 1, "편집 모드", txtEditMode);
            AddRow(fields, 2, "클릭 무시 전환", txtToggleClickThrough);
            AddRow(fields, 3, "현재 화면으로 모으기", txtMoveToCurrentScreen);

            Label hint = new Label();
            hint.Text = "입력 칸을 선택한 뒤 원하는 키 조합을 누르세요. Backspace, Delete, Esc는 단축키를 비웁니다.";
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = UiTheme.MutedTextColor;
            hint.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;

            Button okButton = CreateButton("확인", 92, UiTheme.AccentColor, Color.White);
            okButton.Click += OkButton_Click;

            Button cancelButton = CreateButton("취소", 92, UiTheme.PanelBackColor, UiTheme.TextColor);
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Button recommendedButton = CreateButton("추천값 적용", 116, UiTheme.PanelBackColor, UiTheme.TextColor);
            recommendedButton.Click += (s, e) => LoadValues(HotkeySettings.CreateRecommended());

            buttons.Controls.Add(okButton);
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(recommendedButton);

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(fields, 0, 1);
            root.Controls.Add(hint, 0, 2);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
        }

        private static void AddRow(TableLayoutPanel layout, int row, string labelText, HotkeyTextBox textBox)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            Label label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = UiTheme.TextColor;

            textBox.Dock = DockStyle.Fill;
            textBox.ReadOnly = true;
            UiTheme.StyleTextBox(textBox);

            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(textBox, 1, row);
        }

        private static Button CreateButton(string text, int width, Color backColor, Color foreColor)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Margin = new Padding(8, 0, 0, 0);
            UiTheme.StyleButton(button, backColor, foreColor);
            return button;
        }

        private void LoadValues(HotkeySettings settings)
        {
            txtToggleOverlays.Value = settings.ToggleOverlays;
            txtEditMode.Value = settings.EditMode;
            txtToggleClickThrough.Value = settings.ToggleClickThrough;
            txtMoveToCurrentScreen.Value = settings.MoveToCurrentScreen;
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            Result.ToggleOverlays = txtToggleOverlays.Value;
            Result.EditMode = txtEditMode.Value;
            Result.ToggleClickThrough = txtToggleClickThrough.Value;
            Result.MoveToCurrentScreen = txtMoveToCurrentScreen.Value;

            string duplicateMessage = FindDuplicateHotkeys(Result);

            if (!string.IsNullOrWhiteSpace(duplicateMessage))
            {
                MessageBox.Show(this, duplicateMessage, "단축키 중복");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static string FindDuplicateHotkeys(HotkeySettings settings)
        {
            List<(string Name, HotkeyConfig Hotkey)> hotkeys = new List<(string Name, HotkeyConfig Hotkey)>
            {
                ("전체 표시/숨김", settings.ToggleOverlays),
                ("편집 모드", settings.EditMode),
                ("클릭 무시 전환", settings.ToggleClickThrough),
                ("현재 화면으로 모으기", settings.MoveToCurrentScreen)
            };

            for (int i = 0; i < hotkeys.Count; i++)
            {
                for (int j = i + 1; j < hotkeys.Count; j++)
                {
                    if (hotkeys[i].Hotkey.SameAs(hotkeys[j].Hotkey))
                    {
                        return "중복된 단축키가 있습니다.\n\n" +
                            hotkeys[i].Name + " / " + hotkeys[j].Name + ": " + hotkeys[i].Hotkey;
                    }
                }
            }

            return "";
        }

        private class HotkeyTextBox : TextBox
        {
            private HotkeyConfig value = new HotkeyConfig { Enabled = false };

            public HotkeyConfig Value
            {
                get
                {
                    return value.Clone();
                }
                set
                {
                    this.value = value.Clone();
                    Text = this.value.ToString();
                }
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                e.SuppressKeyPress = true;

                if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete || e.KeyCode == Keys.Escape)
                {
                    Value = new HotkeyConfig { Enabled = false };
                    return;
                }

                Keys key = e.KeyCode;

                if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                    key == Keys.LWin || key == Keys.RWin)
                {
                    return;
                }

                Value = new HotkeyConfig
                {
                    Enabled = true,
                    Control = e.Control,
                    Alt = e.Alt,
                    Shift = e.Shift,
                    Win = (e.Modifiers & Keys.LWin) == Keys.LWin || (e.Modifiers & Keys.RWin) == Keys.RWin,
                    Key = key
                };
            }
        }
    }
}
