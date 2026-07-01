using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class PresetManagerForm : Form
    {
        private readonly ListBox lstPresets = new ListBox();
        private readonly TextBox txtName = new TextBox();
        private readonly List<OverlayItemConfig> currentItems;

        public List<PresetConfig> ResultPresets { get; }
        public PresetConfig? PresetToApply { get; private set; }
        public bool PresetsChanged { get; private set; }

        public PresetManagerForm(List<PresetConfig> presets, List<OverlayItemConfig> currentItems)
        {
            ResultPresets = ClonePresets(presets);
            this.currentItems = currentItems;

            Text = "프리셋 관리";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 310);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            InitControls();
            RefreshList();
        }

        private void InitControls()
        {
            Label nameLabel = CreateLabel("프리셋 이름");
            nameLabel.Location = new Point(16, 17);
            nameLabel.Size = new Size(90, 24);

            txtName.Location = new Point(112, 16);
            txtName.Size = new Size(316, 23);
            txtName.BackColor = Color.FromArgb(45, 45, 48);
            txtName.ForeColor = Color.White;
            txtName.BorderStyle = BorderStyle.FixedSingle;

            lstPresets.Location = new Point(16, 52);
            lstPresets.Size = new Size(412, 160);
            lstPresets.BackColor = Color.FromArgb(37, 37, 38);
            lstPresets.ForeColor = Color.White;
            lstPresets.BorderStyle = BorderStyle.FixedSingle;
            lstPresets.SelectedIndexChanged += (s, e) =>
            {
                if (lstPresets.SelectedItem is PresetConfig preset)
                {
                    txtName.Text = preset.Name;
                }
            };

            Button saveButton = CreateButton("현재 설정 저장", 16, 224, 128);
            saveButton.Click += SaveButton_Click;

            Button applyButton = CreateButton("적용", 150, 224, 82);
            applyButton.Click += ApplyButton_Click;

            Button deleteButton = CreateButton("삭제", 238, 224, 82);
            deleteButton.BackColor = Color.FromArgb(185, 74, 72);
            deleteButton.Click += DeleteButton_Click;

            Button closeButton = CreateButton("닫기", 346, 260, 82);
            closeButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            Label hintLabel = CreateLabel("같은 이름으로 저장하면 기존 프리셋을 덮어씁니다.");
            hintLabel.Location = new Point(16, 260);
            hintLabel.Size = new Size(300, 30);
            hintLabel.ForeColor = Color.FromArgb(210, 210, 210);

            Controls.Add(nameLabel);
            Controls.Add(txtName);
            Controls.Add(lstPresets);
            Controls.Add(saveButton);
            Controls.Add(applyButton);
            Controls.Add(deleteButton);
            Controls.Add(hintLabel);
            Controls.Add(closeButton);
        }

        private static Label CreateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(230, 230, 230);
            label.Font = new Font("맑은 고딕", 9F);
            return label;
        }

        private static Button CreateButton(string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 30);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(58, 122, 254);
            button.ForeColor = Color.White;
            button.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            return button;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            string name = txtName.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("프리셋 이름을 입력하세요.");
                return;
            }

            PresetConfig preset = PresetConfig.Create(name, currentItems);
            int existingIndex = ResultPresets.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                DialogResult result = MessageBox.Show(
                    "같은 이름의 프리셋이 있습니다. 덮어쓰시겠습니까?",
                    "프리셋 저장",
                    MessageBoxButtons.YesNo
                );

                if (result != DialogResult.Yes)
                    return;

                ResultPresets[existingIndex] = preset;
            }
            else
            {
                ResultPresets.Add(preset);
            }

            PresetsChanged = true;
            RefreshList();
            SelectPresetByName(name);
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            if (lstPresets.SelectedItem is not PresetConfig preset)
            {
                MessageBox.Show("적용할 프리셋을 선택하세요.");
                return;
            }

            PresetToApply = PresetConfig.Create(preset.Name, preset.Items);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (lstPresets.SelectedItem is not PresetConfig preset)
            {
                MessageBox.Show("삭제할 프리셋을 선택하세요.");
                return;
            }

            DialogResult result = MessageBox.Show(
                "선택한 프리셋을 삭제하시겠습니까?",
                "프리셋 삭제",
                MessageBoxButtons.YesNo
            );

            if (result != DialogResult.Yes)
                return;

            ResultPresets.Remove(preset);
            PresetsChanged = true;
            RefreshList();
        }

        private void RefreshList()
        {
            lstPresets.DisplayMember = nameof(PresetConfig.Name);
            lstPresets.DataSource = null;
            lstPresets.DataSource = ResultPresets;
        }

        private void SelectPresetByName(string name)
        {
            for (int index = 0; index < lstPresets.Items.Count; index++)
            {
                if (lstPresets.Items[index] is PresetConfig preset &&
                    string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    lstPresets.SelectedIndex = index;
                    break;
                }
            }
        }

        private static List<PresetConfig> ClonePresets(IEnumerable<PresetConfig> presets)
        {
            List<PresetConfig> result = new List<PresetConfig>();

            foreach (PresetConfig preset in presets)
            {
                result.Add(PresetConfig.Create(preset.Name, preset.Items));
            }

            return result;
        }
    }
}
