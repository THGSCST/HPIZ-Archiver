namespace HPIZArchiver
{
    partial class MainForm
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique 
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripExtractButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripCompressButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripPathTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripPathLabel = new System.Windows.Forms.ToolStripLabel();
            this.toolStripOpenButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.hPIFileToExtractToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hPIFilesToMergeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.directoryToCompressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compressionLevelComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.firstStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.secondStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.listViewFiles = new System.Windows.Forms.ListView();
            this.columnHeaderName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnCompressed = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnRatio = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.dialogOpenFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.dialogSaveHpi = new System.Windows.Forms.SaveFileDialog();
            this.dialogOpenHpi = new System.Windows.Forms.OpenFileDialog();
            this.dialogExtractToFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.dialogExtractFile = new System.Windows.Forms.SaveFileDialog();
            this.toolStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripExtractButton,
            this.toolStripSeparator1,
            this.toolStripCompressButton,
            this.toolStripPathTextBox,
            this.toolStripPathLabel,
            this.toolStripOpenButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(782, 75);
            this.toolStrip.TabIndex = 2;
            // 
            // toolStripExtractButton
            // 
            this.toolStripExtractButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripExtractButton.Enabled = false;
            this.toolStripExtractButton.Image = global::HPIZArchiver.Properties.Resources.Extract_48x;
            this.toolStripExtractButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toolStripExtractButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripExtractButton.Name = "toolStripExtractButton";
            this.toolStripExtractButton.Size = new System.Drawing.Size(58, 72);
            this.toolStripExtractButton.Text = "Extract";
            this.toolStripExtractButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.toolStripExtractButton.ToolTipText = "Extract checked files";
            this.toolStripExtractButton.Click += new System.EventHandler(this.toolStripExtractButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 75);
            // 
            // toolStripCompressButton
            // 
            this.toolStripCompressButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripCompressButton.Enabled = false;
            this.toolStripCompressButton.Image = global::HPIZArchiver.Properties.Resources.Compress_48x;
            this.toolStripCompressButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toolStripCompressButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripCompressButton.Name = "toolStripCompressButton";
            this.toolStripCompressButton.Size = new System.Drawing.Size(78, 72);
            this.toolStripCompressButton.Text = "Compress";
            this.toolStripCompressButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.toolStripCompressButton.ToolTipText = "Compress checked files";
            this.toolStripCompressButton.Click += new System.EventHandler(this.toolStripCompressButton_Click);
            // 
            // toolStripPathTextBox
            // 
            this.toolStripPathTextBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripPathTextBox.Margin = new System.Windows.Forms.Padding(1, 0, 20, 0);
            this.toolStripPathTextBox.Name = "toolStripPathTextBox";
            this.toolStripPathTextBox.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.toolStripPathTextBox.ReadOnly = true;
            this.toolStripPathTextBox.Size = new System.Drawing.Size(480, 75);
            // 
            // toolStripPathLabel
            // 
            this.toolStripPathLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripPathLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripPathLabel.Name = "toolStripPathLabel";
            this.toolStripPathLabel.Size = new System.Drawing.Size(40, 72);
            this.toolStripPathLabel.Text = "Path:";
            // 
            // toolStripOpenButton
            // 
            this.toolStripOpenButton.AutoToolTip = false;
            this.toolStripOpenButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hPIFileToExtractToolStripMenuItem,
            this.hPIFilesToMergeToolStripMenuItem,
            this.toolStripSeparator2,
            this.directoryToCompressToolStripMenuItem,
            this.compressionLevelComboBox});
            this.toolStripOpenButton.Image = global::HPIZArchiver.Properties.Resources.SearchFolderOpened_48x;
            this.toolStripOpenButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toolStripOpenButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripOpenButton.Name = "toolStripOpenButton";
            this.toolStripOpenButton.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.toolStripOpenButton.Size = new System.Drawing.Size(62, 72);
            this.toolStripOpenButton.Text = "Open";
            this.toolStripOpenButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            // 
            // hPIFileToExtractToolStripMenuItem
            // 
            this.hPIFileToExtractToolStripMenuItem.Name = "hPIFileToExtractToolStripMenuItem";
            this.hPIFileToExtractToolStripMenuItem.Size = new System.Drawing.Size(247, 26);
            this.hPIFileToExtractToolStripMenuItem.Text = "HPI File to extract...";
            this.hPIFileToExtractToolStripMenuItem.Click += new System.EventHandler(this.hPIFileToExtractToolStripMenuItem_Click);
            // 
            // hPIFilesToMergeToolStripMenuItem
            // 
            this.hPIFilesToMergeToolStripMenuItem.Enabled = false;
            this.hPIFilesToMergeToolStripMenuItem.Name = "hPIFilesToMergeToolStripMenuItem";
            this.hPIFilesToMergeToolStripMenuItem.Size = new System.Drawing.Size(247, 26);
            this.hPIFilesToMergeToolStripMenuItem.Text = "HPI Files to merge...";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(244, 6);
            // 
            // directoryToCompressToolStripMenuItem
            // 
            this.directoryToCompressToolStripMenuItem.Name = "directoryToCompressToolStripMenuItem";
            this.directoryToCompressToolStripMenuItem.Size = new System.Drawing.Size(247, 26);
            this.directoryToCompressToolStripMenuItem.Text = "Directory to compress...";
            this.directoryToCompressToolStripMenuItem.Click += new System.EventHandler(this.directoryToCompressToolStripMenuItem_Click);
            // 
            // compressionLevelComboBox
            // 
            this.compressionLevelComboBox.AutoToolTip = true;
            this.compressionLevelComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.compressionLevelComboBox.Items.AddRange(new object[] {
            "Zopfli i15",
            "Zopfli i10",
            "Zopfli i5",
            "Zopfli i1",
            "ZLib Deflate"});
            this.compressionLevelComboBox.Name = "compressionLevelComboBox";
            this.compressionLevelComboBox.Size = new System.Drawing.Size(160, 28);
            this.compressionLevelComboBox.ToolTipText = "Select compression level";
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressBar,
            this.firstStatusLabel,
            this.secondStatusLabel});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 427);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(782, 26);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 3;
            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(120, 25);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar.Visible = false;
            // 
            // firstStatusLabel
            // 
            this.firstStatusLabel.Name = "firstStatusLabel";
            this.firstStatusLabel.Size = new System.Drawing.Size(48, 20);
            this.firstStatusLabel.Text = "0 files";
            // 
            // secondStatusLabel
            // 
            this.secondStatusLabel.Name = "secondStatusLabel";
            this.secondStatusLabel.Size = new System.Drawing.Size(13, 20);
            this.secondStatusLabel.Text = " ";
            // 
            // listViewFiles
            // 
            this.listViewFiles.Activation = System.Windows.Forms.ItemActivation.OneClick;
            this.listViewFiles.CheckBoxes = true;
            this.listViewFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderSize,
            this.columnCompressed,
            this.columnRatio});
            this.listViewFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewFiles.FullRowSelect = true;
            this.listViewFiles.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewFiles.HideSelection = false;
            this.listViewFiles.HotTracking = true;
            this.listViewFiles.HoverSelection = true;
            this.listViewFiles.Location = new System.Drawing.Point(0, 75);
            this.listViewFiles.MultiSelect = false;
            this.listViewFiles.Name = "listViewFiles";
            this.listViewFiles.ShowGroups = false;
            this.listViewFiles.Size = new System.Drawing.Size(782, 352);
            this.listViewFiles.TabIndex = 4;
            this.listViewFiles.UseCompatibleStateImageBehavior = false;
            this.listViewFiles.View = System.Windows.Forms.View.Details;
            // 
            // columnHeaderName
            // 
            this.columnHeaderName.Text = "Full Name";
            this.columnHeaderName.Width = 496;
            // 
            // columnHeaderSize
            // 
            this.columnHeaderSize.Text = "Size";
            this.columnHeaderSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnHeaderSize.Width = 100;
            // 
            // columnCompressed
            // 
            this.columnCompressed.Text = "Compressed";
            this.columnCompressed.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnCompressed.Width = 100;
            // 
            // columnRatio
            // 
            this.columnRatio.Tag = "";
            this.columnRatio.Text = "Ratio";
            this.columnRatio.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnRatio.Width = 55;
            // 
            // dialogOpenFolder
            // 
            this.dialogOpenFolder.Description = "Select folder containing the files to be compressed";
            this.dialogOpenFolder.RootFolder = System.Environment.SpecialFolder.MyComputer;
            this.dialogOpenFolder.ShowNewFolderButton = false;
            // 
            // dialogSaveHpi
            // 
            this.dialogSaveHpi.Filter = "All TA Files|*.hpi;*.ccx;*.ufo;*.gp?|HPI Files|*.hpi|CCX Files|*.ccx|UFO Files|*." +
    "ufo|GP Files|*.gp?|Any File|*.*";
            this.dialogSaveHpi.SupportMultiDottedExtensions = true;
            // 
            // dialogOpenHpi
            // 
            this.dialogOpenHpi.Filter = "All TA Files|*.hpi;*.ccx;*.ufo;*.gp?|HPI Files|*.hpi|CCX Files|*.ccx|UFO Files|*." +
    "ufo|GP Files|*.gp?|All Files|*.*";
            this.dialogOpenHpi.ReadOnlyChecked = true;
            // 
            // dialogExtractToFolder
            // 
            this.dialogExtractToFolder.Description = "Folder to extract all selected files";
            this.dialogExtractToFolder.RootFolder = System.Environment.SpecialFolder.MyComputer;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(782, 453);
            this.Controls.Add(this.listViewFiles);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.toolStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "HPIZ Archiver";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripLabel toolStripPathLabel;
        private System.Windows.Forms.ToolStripTextBox toolStripPathTextBox;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel firstStatusLabel;
        private System.Windows.Forms.ListView listViewFiles;
        private System.Windows.Forms.ColumnHeader columnHeaderName;
        private System.Windows.Forms.ColumnHeader columnHeaderSize;
        private System.Windows.Forms.FolderBrowserDialog dialogOpenFolder;
        private System.Windows.Forms.SaveFileDialog dialogSaveHpi;
        private System.Windows.Forms.ToolStripButton toolStripExtractButton;
        private System.Windows.Forms.ToolStripDropDownButton toolStripOpenButton;
        private System.Windows.Forms.ToolStripMenuItem hPIFileToExtractToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem directoryToCompressToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.OpenFileDialog dialogOpenHpi;
        private System.Windows.Forms.FolderBrowserDialog dialogExtractToFolder;
        private System.Windows.Forms.SaveFileDialog dialogExtractFile;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ColumnHeader columnCompressed;
        private System.Windows.Forms.ColumnHeader columnRatio;
        private System.Windows.Forms.ToolStripMenuItem hPIFilesToMergeToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel secondStatusLabel;
        private System.Windows.Forms.ToolStripButton toolStripCompressButton;
        private System.Windows.Forms.ToolStripComboBox compressionLevelComboBox;
    }
}

