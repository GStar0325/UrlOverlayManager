namespace UrlOverlayManager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            dgvItems = new DataGridView();
            btnAdd = new Button();
            btnDelete = new Button();
            txtName = new TextBox();
            txtUrl = new TextBox();
            btnApply = new Button();
            numOpacity = new NumericUpDown();
            chkEnabled = new CheckBox();
            chkClickThrough = new CheckBox();
            name = new Label();
            url = new Label();
            opacity = new Label();
            ((System.ComponentModel.ISupportInitialize)dgvItems).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numOpacity).BeginInit();
            SuspendLayout();
            // 
            // dgvItems
            // 
            dgvItems.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvItems.Location = new Point(12, 12);
            dgvItems.Name = "dgvItems";
            dgvItems.Size = new Size(508, 176);
            dgvItems.TabIndex = 0;
            dgvItems.SelectionChanged += dgvItems_SelectionChanged;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(364, 194);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(75, 23);
            btnAdd.TabIndex = 1;
            btnAdd.Text = "추가";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(445, 194);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(75, 23);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "삭제";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // txtName
            // 
            txtName.Location = new Point(92, 236);
            txtName.Name = "txtName";
            txtName.Size = new Size(203, 23);
            txtName.TabIndex = 3;
            // 
            // txtUrl
            // 
            txtUrl.Location = new Point(92, 265);
            txtUrl.Name = "txtUrl";
            txtUrl.Size = new Size(203, 23);
            txtUrl.TabIndex = 4;
            txtUrl.KeyDown += txtUrl_KeyDown;
            // 
            // btnApply
            // 
            btnApply.Location = new Point(445, 415);
            btnApply.Name = "btnApply";
            btnApply.Size = new Size(75, 23);
            btnApply.TabIndex = 5;
            btnApply.Text = "적용";
            btnApply.UseVisualStyleBackColor = true;
            btnApply.Visible = false;
            btnApply.Click += btnApply_Click;
            // 
            // numOpacity
            // 
            numOpacity.Location = new Point(92, 294);
            numOpacity.Name = "numOpacity";
            numOpacity.Size = new Size(120, 23);
            numOpacity.TabIndex = 6;
            // 
            // chkEnabled
            // 
            chkEnabled.AutoSize = true;
            chkEnabled.Location = new Point(337, 238);
            chkEnabled.Name = "chkEnabled";
            chkEnabled.Size = new Size(102, 19);
            chkEnabled.TabIndex = 7;
            chkEnabled.Text = "오버레이 표시";
            chkEnabled.UseVisualStyleBackColor = true;
            // 
            // chkClickThrough
            // 
            chkClickThrough.AutoSize = true;
            chkClickThrough.Location = new Point(337, 269);
            chkClickThrough.Name = "chkClickThrough";
            chkClickThrough.Size = new Size(78, 19);
            chkClickThrough.TabIndex = 8;
            chkClickThrough.Text = "클릭 무시";
            chkClickThrough.UseVisualStyleBackColor = true;
            // 
            // name
            // 
            name.AutoSize = true;
            name.Location = new Point(31, 240);
            name.Name = "name";
            name.Size = new Size(31, 15);
            name.TabIndex = 9;
            name.Text = "이름";
            // 
            // url
            // 
            url.AutoSize = true;
            url.Location = new Point(34, 271);
            url.Name = "url";
            url.Size = new Size(28, 15);
            url.TabIndex = 10;
            url.Text = "URL";
            // 
            // opacity
            // 
            opacity.AutoSize = true;
            opacity.Location = new Point(31, 298);
            opacity.Name = "opacity";
            opacity.Size = new Size(43, 15);
            opacity.TabIndex = 11;
            opacity.Text = "투명도";
            // 
            // Form1
            // 
            AutoScaleMode = AutoScaleMode.None;
            BackColor = SystemColors.ActiveCaption;
            ClientSize = new Size(534, 226);
            Controls.Add(opacity);
            Controls.Add(url);
            Controls.Add(name);
            Controls.Add(chkClickThrough);
            Controls.Add(chkEnabled);
            Controls.Add(numOpacity);
            Controls.Add(btnApply);
            Controls.Add(txtUrl);
            Controls.Add(txtName);
            Controls.Add(btnDelete);
            Controls.Add(btnAdd);
            Controls.Add(dgvItems);
            MaximizeBox = false;
            Name = "Form1";
            ShowIcon = false;
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            ((System.ComponentModel.ISupportInitialize)dgvItems).EndInit();
            ((System.ComponentModel.ISupportInitialize)numOpacity).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dgvItems;
        private Button btnAdd;
        private Button btnDelete;
        private TextBox txtName;
        private TextBox txtUrl;
        private Button btnApply;
        private NumericUpDown numOpacity;
        private CheckBox chkEnabled;
        private CheckBox chkClickThrough;
        private Label name;
        private Label url;
        private Label opacity;
    }
}
