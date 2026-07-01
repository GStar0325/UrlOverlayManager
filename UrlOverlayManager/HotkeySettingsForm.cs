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

            Text = "단축키 설정";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(500, 276);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            InitControls();
            LoadValues(Result);
        }

        private void InitControls()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16);
            layout.ColumnCount = 2;
            layout.RowCount = 7;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(layout, 0, "전체 표시/숨김", txtToggleOverlays);
            AddRow(layout, 1, "편집 모드", txtEditMode);
            AddRow(layout, 2, "클릭 무시 토글", txtToggleClickThrough);
            AddRow(layout, 3, "현재 화면으로 모으기", txtMoveToCurrentScreen);

            Label hintLabel = new Label();
            hintLabel.Text = "입력칸 선택 후 원하는 조합을 누르세요. Backspace, Delete, Esc는 사용 안 함입니다.";
            hintLabel.AutoSize = false;
            hintLabel.Height = 34;
            hintLabel.TextAlign = ContentAlignment.MiddleLeft;
            hintLabel.ForeColor = Color.FromArgb(210, 210, 210);
            layout.Controls.Add(hintLabel, 0, 4);
            layout.SetColumnSpan(hintLabel, 2);

            Button recommendedButton = CreateButton("추천값 채우기", 116);
            recommendedButton.Click += (s, e) => LoadValues(HotkeySettings.CreateRecommended());
            layout.Controls.Add(recommendedButton, 1, 5);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;

            Button okButton = CreateButton("확인", 86);
            okButton.Click += OkButton_Click;

            Button cancelButton = CreateButton("취소", 86);
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttons.Controls.Add(okButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 6);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
        }

        private static void AddRow(TableLayoutPanel layout, int row, string labelText, HotkeyTextBox textBox)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;

            textBox.Dock = DockStyle.Fill;
            textBox.ReadOnly = true;
            textBox.BackColor = Color.FromArgb(45, 45, 48);
            textBox.ForeColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(textBox, 1, row);
        }

        private static Button CreateButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(58, 122, 254);
            button.ForeColor = Color.White;
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
                MessageBox.Show(duplicateMessage);
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
                ("클릭 무시 토글", settings.ToggleClickThrough),
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
