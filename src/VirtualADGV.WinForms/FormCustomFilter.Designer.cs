
namespace Zuby.ADGV
{
    partial class FormCustomFilter
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        
        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.button_ok = new System.Windows.Forms.Button();
            this.button_cancel = new System.Windows.Forms.Button();
            this.label_columnName = new System.Windows.Forms.Label();
            this.comboBox_filterType = new System.Windows.Forms.ComboBox();
            this.label_and = new System.Windows.Forms.Label();
            this.comboBox_filterType2 = new System.Windows.Forms.ComboBox();
            this.radioButton_and = new System.Windows.Forms.RadioButton();
            this.radioButton_or = new System.Windows.Forms.RadioButton();
            this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // button_ok
            // 
            this.button_ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button_ok.Location = new System.Drawing.Point(40, 139);
            this.button_ok.Name = "button_ok";
            this.button_ok.Size = new System.Drawing.Size(75, 23);
            this.button_ok.TabIndex = 0;
            this.button_ok.Text = "OK";
            this.button_ok.UseVisualStyleBackColor = true;
            this.button_ok.Click += new System.EventHandler(this.button_ok_Click);
            // 
            // button_cancel
            // 
            this.button_cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button_cancel.Location = new System.Drawing.Point(121, 139);
            this.button_cancel.Name = "button_cancel";
            this.button_cancel.Size = new System.Drawing.Size(75, 23);
            this.button_cancel.TabIndex = 1;
            this.button_cancel.Text = "Cancel";
            this.button_cancel.UseVisualStyleBackColor = true;
            this.button_cancel.Click += new System.EventHandler(this.button_cancel_Click);
            // 
            // label_columnName
            // 
            this.label_columnName.AutoSize = true;
            this.label_columnName.Location = new System.Drawing.Point(4, 9);
            this.label_columnName.Name = "label_columnName";
            this.label_columnName.Size = new System.Drawing.Size(120, 13);
            this.label_columnName.TabIndex = 2;
            this.label_columnName.Text = "Show rows where value";
            // 
            // comboBox_filterType
            // 
            this.comboBox_filterType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_filterType.FormattingEnabled = true;
            this.comboBox_filterType.Location = new System.Drawing.Point(7, 25);
            this.comboBox_filterType.Name = "comboBox_filterType";
            this.comboBox_filterType.Size = new System.Drawing.Size(180, 21);
            this.comboBox_filterType.TabIndex = 3;
            this.comboBox_filterType.SelectedIndexChanged += new System.EventHandler(this.comboBox_filterType_SelectedIndexChanged);
            // 
            // label_and
            // 
            this.label_and.AutoSize = true;
            this.label_and.Location = new System.Drawing.Point(7, 58);
            this.label_and.Name = "label_and";
            this.label_and.Size = new System.Drawing.Size(26, 13);
            this.label_and.TabIndex = 6;
            this.label_and.Text = "And";
            this.label_and.Visible = false;
            // 
            // comboBox_filterType2
            // 
            this.comboBox_filterType2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_filterType2.FormattingEnabled = true;
            this.comboBox_filterType2.Location = new System.Drawing.Point(7, 85);
            this.comboBox_filterType2.Name = "comboBox_filterType2";
            this.comboBox_filterType2.Size = new System.Drawing.Size(180, 21);
            this.comboBox_filterType2.TabIndex = 7;
            this.comboBox_filterType2.SelectedIndexChanged += new System.EventHandler(this.comboBox_filterType_SelectedIndexChanged);
            // 
            // radioButton_and
            // 
            this.radioButton_and.AutoSize = true;
            this.radioButton_and.Checked = true;
            this.radioButton_and.Location = new System.Drawing.Point(40, 58);
            this.radioButton_and.Name = "radioButton_and";
            this.radioButton_and.Size = new System.Drawing.Size(38, 17);
            this.radioButton_and.TabIndex = 4;
            this.radioButton_and.TabStop = true;
            this.radioButton_and.Text = "Ve";
            this.radioButton_and.UseVisualStyleBackColor = true;
            // 
            // radioButton_or
            // 
            this.radioButton_or.AutoSize = true;
            this.radioButton_or.Location = new System.Drawing.Point(100, 58);
            this.radioButton_or.Name = "radioButton_or";
            this.radioButton_or.Size = new System.Drawing.Size(50, 17);
            this.radioButton_or.TabIndex = 5;
            this.radioButton_or.Text = "Veya";
            this.radioButton_or.UseVisualStyleBackColor = true;
            // 
            // errorProvider
            // 
            this.errorProvider.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;
            this.errorProvider.ContainerControl = this;
            // 
            // FormCustomFilter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.button_cancel;
            this.ClientSize = new System.Drawing.Size(400, 240);
            this.Controls.Add(this.radioButton_or);
            this.Controls.Add(this.radioButton_and);
            this.Controls.Add(this.comboBox_filterType2);
            this.Controls.Add(this.label_and);
            this.Controls.Add(this.label_columnName);
            this.Controls.Add(this.comboBox_filterType);
            this.Controls.Add(this.button_cancel);
            this.Controls.Add(this.button_ok);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormCustomFilter";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Custom Filter";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.FormCustomFilter_Load);
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button_ok;
        private System.Windows.Forms.Button button_cancel;
        private System.Windows.Forms.Label label_columnName;
        private System.Windows.Forms.ComboBox comboBox_filterType;
        private System.Windows.Forms.Label label_and;
        private System.Windows.Forms.ComboBox comboBox_filterType2;
        private System.Windows.Forms.RadioButton radioButton_and;
        private System.Windows.Forms.RadioButton radioButton_or;
        private System.Windows.Forms.ErrorProvider errorProvider;
    }
}
