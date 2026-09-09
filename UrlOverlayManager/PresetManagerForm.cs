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

            InitializeForm();
            BuildLayout();
            UiChrome.Apply(this, "프리셋 관리", false);
            RefreshList();
        }

        private void InitializeForm()
        {
            Text = "프리셋 관리";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 390);
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
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            Label title = new Label();
            title.Text = "프리셋 관리";
            title.Dock = DockStyle.Fill;
            title.Font = UiTheme.BoldFont(17F);
            title.TextAlign = ContentAlignment.MiddleLeft;

            TableLayoutPanel nameRow = new TableLayoutPanel();
            nameRow.Dock = DockStyle.Fill;
            nameRow.ColumnCount = 2;
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label nameLabel = new Label();
            nameLabel.Text = "프리셋 이름";
            nameLabel.Dock = DockStyle.Fill;
            nameLabel.TextAlign = ContentAlignment.MiddleLeft;

            txtName.Dock = DockStyle.Fill;
            UiTheme.StyleTextBox(txtName);

            nameRow.Controls.Add(nameLabel, 0, 0);
            nameRow.Controls.Add(txtName, 1, 0);

            lstPresets.Dock = DockStyle.Fill;
            lstPresets.BackColor = UiTheme.PanelBackColor;
            lstPresets.ForeColor = UiTheme.TextColor;
            lstPresets.BorderStyle = BorderStyle.FixedSingle;
            lstPresets.Font = UiTheme.RegularFont(10F);
            lstPresets.SelectedIndexChanged += (s, e) =>
            {
                if (lstPresets.SelectedItem is PresetConfig preset)
                {
                    txtName.Text = preset.Name;
                }
            };

            FlowLayoutPanel actionRow = new FlowLayoutPanel();
            actionRow.Dock = DockStyle.Fill;
            actionRow.WrapContents = false;

            Button saveButton = CreateButton("현재 설정 저장", 132, UiTheme.AccentColor, Color.White);
            saveButton.Click += SaveButton_Click;

            Button applyButton = CreateButton("적용", 86, UiTheme.PanelBackColor, UiTheme.TextColor);
            applyButton.Click += ApplyButton_Click;

            Button deleteButton = CreateButton("삭제", 86, UiTheme.DangerColor, Color.White);
            deleteButton.Click += DeleteButton_Click;

            actionRow.Controls.Add(saveButton);
            actionRow.Controls.Add(applyButton);
            actionRow.Controls.Add(deleteButton);

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.WrapContents = false;

            Button closeButton = CreateButton("닫기", 86, UiTheme.AccentColor, Color.White);
            closeButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            Label hintLabel = new Label();
            hintLabel.Text = "같은 이름으로 저장하면 기존 프리셋을 덮어씁니다.";
            hintLabel.AutoSize = false;
            hintLabel.Size = new Size(360, 32);
            hintLabel.TextAlign = ContentAlignment.MiddleLeft;
            hintLabel.ForeColor = UiTheme.MutedTextColor;

            bottom.Controls.Add(closeButton);
            bottom.Controls.Add(hintLabel);

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(nameRow, 0, 1);
            root.Controls.Add(lstPresets, 0, 2);
            root.Controls.Add(actionRow, 0, 3);
            root.Controls.Add(bottom, 0, 4);

            Controls.Add(root);
        }

        private static Button CreateButton(string text, int width, Color backColor, Color foreColor)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Margin = new Padding(0, 0, 8, 8);
            UiTheme.StyleButton(button, backColor, foreColor);
            return button;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            string name = txtName.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "프리셋 이름을 입력하세요.", "프리셋 저장");
                return;
            }

            PresetConfig preset = PresetConfig.Create(name, currentItems);
            int existingIndex = ResultPresets.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                DialogResult result = MessageBox.Show(
                    this,
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
                MessageBox.Show(this, "적용할 프리셋을 선택하세요.", "프리셋 적용");
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
                MessageBox.Show(this, "삭제할 프리셋을 선택하세요.", "프리셋 삭제");
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
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
