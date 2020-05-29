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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripOpenButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.openFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openDirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.closeAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripExtractButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripCompressButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.compressCheckedFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeRepackCheckedFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.flavorLevelComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.flavorStripLabel = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.rulesStripButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.keepFirstToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.firstStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.secondStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.listViewFiles = new CollapsibleListView();
            this.columnChecked = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnFullName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnExt = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnCompressed = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnRatio = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.checkListContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.selectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.unselectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.invertSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dialogOpenFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.dialogSaveHpi = new System.Windows.Forms.SaveFileDialog();
            this.dialogOpenHpi = new System.Windows.Forms.OpenFileDialog();
            this.dialogExtractToFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.dialogExtractFile = new System.Windows.Forms.SaveFileDialog();
            this.toolStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.checkListContextMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip
            // 
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripOpenButton,
            this.toolStripExtractButton,
            this.toolStripSeparator1,
            this.toolStripCompressButton,
            this.flavorLevelComboBox,
            this.flavorStripLabel,
            this.toolStripSeparator3,
            this.rulesStripButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(822, 59);
            this.toolStrip.TabIndex = 2;
            // 
            // toolStripOpenButton
            // 
            this.toolStripOpenButton.AutoToolTip = false;
            this.toolStripOpenButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openFilesToolStripMenuItem,
            this.openDirToolStripMenuItem,
            this.toolStripSeparator4,
            this.closeAllToolStripMenuItem});
            this.toolStripOpenButton.Image = global::HPIZArchiver.Properties.Resources.Folder_32x;
            this.toolStripOpenButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toolStripOpenButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripOpenButton.Name = "toolStripOpenButton";
            this.toolStripOpenButton.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.toolStripOpenButton.Size = new System.Drawing.Size(59, 56);
            this.toolStripOpenButton.Text = "Open";
            this.toolStripOpenButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            // 
            // openFilesToolStripMenuItem
            // 
            this.openFilesToolStripMenuItem.Name = "openFilesToolStripMenuItem";
            this.openFilesToolStripMenuItem.Size = new System.Drawing.Size(249, 26);
            this.openFilesToolStripMenuItem.Text = "HPI File or Files...";
            this.openFilesToolStripMenuItem.Click += new System.EventHandler(this.hPIFileToExtractToolStripMenuItem_Click);
            // 
            // openDirToolStripMenuItem
            // 
            this.openDirToolStripMenuItem.Name = "openDirToolStripMenuItem";
            this.openDirToolStripMenuItem.Size = new System.Drawing.Size(249, 26);
            this.openDirToolStripMenuItem.Text = "Directory to Compress...";
            this.openDirToolStripMenuItem.Click += new System.EventHandler(this.directoryToCompressToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(246, 6);
            // 
            // closeAllToolStripMenuItem
            // 
            this.closeAllToolStripMenuItem.Enabled = false;
            this.closeAllToolStripMenuItem.Name = "closeAllToolStripMenuItem";
            this.closeAllToolStripMenuItem.Size = new System.Drawing.Size(249, 26);
            this.closeAllToolStripMenuItem.Text = "Close All";
            this.closeAllToolStripMenuItem.Click += new System.EventHandler(this.closeAllToolStripMenuItem_Click);
            // 
            // toolStripExtractButton
            // 
            this.toolStripExtractButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripExtractButton.Enabled = false;
            this.toolStripExtractButton.Image = global::HPIZArchiver.Properties.Resources.Extract_32x;
            this.toolStripExtractButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toolStripExtractButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripExtractButton.Name = "toolStripExtractButton";
            this.toolStripExtractButton.Size = new System.Drawing.Size(58, 56);
            this.toolStripExtractButton.Text = "Extract";
            this.toolStripExtractButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.toolStripExtractButton.ToolTipText = "Extract checked files";
            this.toolStripExtractButton.Click += new System.EventHandler(this.toolStripExtractButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 59);
            // 
            // toolStripCompressButton
            // 
            this.toolStripCompressButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripCompressButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.compressCheckedFilesToolStripMenuItem,
            this.mergeRepackCheckedFilesToolStripMenuItem});
            this.toolStripCompressButton.Enabled = false;
            this.toolStripCompressButton.Image = global::HPIZArchiver.Properties.Resources.Compress_32x;
            this.toolStripCompressButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.toolStripCompressButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripCompressButton.Name = "toolStripCompressButton";
            this.toolStripCompressButton.Size = new System.Drawing.Size(88, 56);
            this.toolStripCompressButton.Text = "Compress";
            this.toolStripCompressButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.toolStripCompressButton.ToolTipText = "Compress checked files";
            // 
            // compressCheckedFilesToolStripMenuItem
            // 
            this.compressCheckedFilesToolStripMenuItem.Name = "compressCheckedFilesToolStripMenuItem";
            this.compressCheckedFilesToolStripMenuItem.Size = new System.Drawing.Size(307, 26);
            this.compressCheckedFilesToolStripMenuItem.Text = "Compress Checked Files...";
            this.compressCheckedFilesToolStripMenuItem.Click += new System.EventHandler(this.compressCheckedFilesToolStripMenuItem_Click);
            // 
            // mergeRepackCheckedFilesToolStripMenuItem
            // 
            this.mergeRepackCheckedFilesToolStripMenuItem.Name = "mergeRepackCheckedFilesToolStripMenuItem";
            this.mergeRepackCheckedFilesToolStripMenuItem.Size = new System.Drawing.Size(307, 26);
            this.mergeRepackCheckedFilesToolStripMenuItem.Text = "Merge or Repack Checked Files...";
            this.mergeRepackCheckedFilesToolStripMenuItem.Click += new System.EventHandler(this.mergeRepackCheckedFilesToolStripMenuItem_Click);
            // 
            // flavorLevelComboBox
            // 
            this.flavorLevelComboBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.flavorLevelComboBox.AutoToolTip = true;
            this.flavorLevelComboBox.CausesValidation = false;
            this.flavorLevelComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.flavorLevelComboBox.Enabled = false;
            this.flavorLevelComboBox.Name = "flavorLevelComboBox";
            this.flavorLevelComboBox.Size = new System.Drawing.Size(180, 59);
            this.flavorLevelComboBox.ToolTipText = "Select compression flavor or level. Defaul is i15 Zopfli Deflate.";
            // 
            // flavorStripLabel
            // 
            this.flavorStripLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.flavorStripLabel.Enabled = false;
            this.flavorStripLabel.Name = "flavorStripLabel";
            this.flavorStripLabel.Size = new System.Drawing.Size(52, 56);
            this.flavorStripLabel.Text = "Flavor:";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 59);
            // 
            // rulesStripButton
            // 
            this.rulesStripButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.keepFirstToolStripMenuItem});
            this.rulesStripButton.Enabled = false;
            this.rulesStripButton.Image = global::HPIZArchiver.Properties.Resources.Rules_32x;
            this.rulesStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.rulesStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.rulesStripButton.Name = "rulesStripButton";
            this.rulesStripButton.Size = new System.Drawing.Size(126, 56);
            this.rulesStripButton.Text = "Duplicate Rules";
            this.rulesStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            // 
            // keepFirstToolStripMenuItem
            // 
            this.keepFirstToolStripMenuItem.Checked = true;
            this.keepFirstToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.keepFirstToolStripMenuItem.Name = "keepFirstToolStripMenuItem";
            this.keepFirstToolStripMenuItem.Size = new System.Drawing.Size(157, 26);
            this.keepFirstToolStripMenuItem.Text = "Keep First";
            this.keepFirstToolStripMenuItem.ToolTipText = "Uncheck all duplicates except the first one";
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressBar,
            this.firstStatusLabel,
            this.secondStatusLabel});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 447);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 13, 0);
            this.statusStrip.Size = new System.Drawing.Size(822, 26);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 3;
            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(120, 18);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar.Visible = false;
            // 
            // firstStatusLabel
            // 
            this.firstStatusLabel.Name = "firstStatusLabel";
            this.firstStatusLabel.Size = new System.Drawing.Size(152, 20);
            this.firstStatusLabel.Text = "No files or directories";
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
            this.listViewFiles.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listViewFiles.CheckBoxes = true;
            this.listViewFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnChecked,
            this.columnFullName,
            this.columnExt,
            this.columnSize,
            this.columnCompressed,
            this.columnRatio});
            this.listViewFiles.ContextMenuStrip = this.checkListContextMenu;
            this.listViewFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewFiles.FullRowSelect = true;
            this.listViewFiles.HideSelection = false;
            this.listViewFiles.HotTracking = true;
            this.listViewFiles.HoverSelection = true;
            this.listViewFiles.Location = new System.Drawing.Point(0, 59);
            this.listViewFiles.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.listViewFiles.MultiSelect = false;
            this.listViewFiles.Name = "listViewFiles";
            this.listViewFiles.Size = new System.Drawing.Size(822, 388);
            this.listViewFiles.TabIndex = 4;
            this.listViewFiles.Tag = "A";
            this.listViewFiles.UseCompatibleStateImageBehavior = false;
            this.listViewFiles.View = System.Windows.Forms.View.Details;
            this.listViewFiles.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listViewFiles_ColumnClick);
            // 
            // columnChecked
            // 
            this.columnChecked.Text = "...";
            this.columnChecked.Width = 22;
            // 
            // columnFullName
            // 
            this.columnFullName.Text = "Full Name";
            this.columnFullName.Width = 327;
            // 
            // columnExt
            // 
            this.columnExt.Text = "Ext";
            this.columnExt.Width = 40;
            // 
            // columnSize
            // 
            this.columnSize.Text = "Size";
            this.columnSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnSize.Width = 75;
            // 
            // columnCompressed
            // 
            this.columnCompressed.Text = "Compressed";
            this.columnCompressed.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnCompressed.Width = 75;
            // 
            // columnRatio
            // 
            this.columnRatio.Tag = "";
            this.columnRatio.Text = "Ratio";
            this.columnRatio.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnRatio.Width = 48;
            // 
            // checkListContextMenu
            // 
            this.checkListContextMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.checkListContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectAllToolStripMenuItem,
            this.unselectAllToolStripMenuItem,
            this.invertSelectedToolStripMenuItem});
            this.checkListContextMenu.Name = "checkListContextMenu";
            this.checkListContextMenu.Size = new System.Drawing.Size(192, 76);
            // 
            // selectAllToolStripMenuItem
            // 
            this.selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
            this.selectAllToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
            this.selectAllToolStripMenuItem.Size = new System.Drawing.Size(191, 24);
            this.selectAllToolStripMenuItem.Text = "Check All";
            this.selectAllToolStripMenuItem.Click += new System.EventHandler(this.selectAllToolStripMenuItem_Click);
            // 
            // unselectAllToolStripMenuItem
            // 
            this.unselectAllToolStripMenuItem.Name = "unselectAllToolStripMenuItem";
            this.unselectAllToolStripMenuItem.Size = new System.Drawing.Size(191, 24);
            this.unselectAllToolStripMenuItem.Text = "Uncheck All";
            this.unselectAllToolStripMenuItem.Click += new System.EventHandler(this.unselectAllToolStripMenuItem_Click);
            // 
            // invertSelectedToolStripMenuItem
            // 
            this.invertSelectedToolStripMenuItem.Name = "invertSelectedToolStripMenuItem";
            this.invertSelectedToolStripMenuItem.Size = new System.Drawing.Size(191, 24);
            this.invertSelectedToolStripMenuItem.Text = "Invert Checked";
            this.invertSelectedToolStripMenuItem.Click += new System.EventHandler(this.invertSelectedToolStripMenuItem_Click);
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
            this.dialogOpenHpi.Multiselect = true;
            this.dialogOpenHpi.Title = "Select one or many HPI Files to Open";
            // 
            // dialogExtractToFolder
            // 
            this.dialogExtractToFolder.Description = "Folder to extract all checked files";
            this.dialogExtractToFolder.RootFolder = System.Environment.SpecialFolder.MyComputer;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(822, 473);
            this.Controls.Add(this.listViewFiles);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.toolStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "MainForm";
            this.Text = "HPIZ Archiver";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.checkListContextMenu.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel firstStatusLabel;
        private System.Windows.Forms.ListView listViewFiles;
        private System.Windows.Forms.ColumnHeader columnFullName;
        private System.Windows.Forms.ColumnHeader columnSize;
        private System.Windows.Forms.FolderBrowserDialog dialogOpenFolder;
        private System.Windows.Forms.SaveFileDialog dialogSaveHpi;
        private System.Windows.Forms.ToolStripButton toolStripExtractButton;
        private System.Windows.Forms.ToolStripDropDownButton toolStripOpenButton;
        private System.Windows.Forms.ToolStripMenuItem openFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDirToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog dialogOpenHpi;
        private System.Windows.Forms.FolderBrowserDialog dialogExtractToFolder;
        private System.Windows.Forms.SaveFileDialog dialogExtractFile;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ColumnHeader columnCompressed;
        private System.Windows.Forms.ColumnHeader columnRatio;
        private System.Windows.Forms.ToolStripStatusLabel secondStatusLabel;
        private System.Windows.Forms.ToolStripDropDownButton toolStripCompressButton;
        private System.Windows.Forms.ToolStripMenuItem compressCheckedFilesToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip checkListContextMenu;
        private System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem unselectAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem invertSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox flavorLevelComboBox;
        private System.Windows.Forms.ToolStripLabel flavorStripLabel;
        private System.Windows.Forms.ColumnHeader columnExt;
        private System.Windows.Forms.ColumnHeader columnChecked;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripDropDownButton rulesStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem closeAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem keepFirstToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mergeRepackCheckedFilesToolStripMenuItem;
    }
}

