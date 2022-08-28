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
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.firstStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.secondStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.checkListContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.selectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.unselectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.invertSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.fullNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extensionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.compressionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ratioToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.methodToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.offsetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sha256ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dialogOpenFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.dialogSaveHpi = new System.Windows.Forms.SaveFileDialog();
            this.dialogOpenHpi = new System.Windows.Forms.OpenFileDialog();
            this.dialogExtractToFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.dialogExtractFile = new System.Windows.Forms.SaveFileDialog();
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
            this.manageDuplicateNamesStripButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.keepFirstNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.keepLastNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.uncheckAllDuplicateNamesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.HighlightsToolStripDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.unknowFoldersExtensionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.duplicateNamesinYellowStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicateContentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicateNameContentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.listViewFiles = new HPIZArchiver.CollapsibleListView();
            this.columnChecked = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnFullName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnExt = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnCompressed = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnRatio = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnMethod = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnOffset = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnSha256 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.statusStrip.SuspendLayout();
            this.checkListContextMenu.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.progressBar,
            this.firstStatusLabel,
            this.secondStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 522);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 13, 0);
            this.statusStrip.Size = new System.Drawing.Size(942, 26);
            this.statusStrip.TabIndex = 3;
            // 
            // progressBar
            // 
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(120, 18);
            this.progressBar.Step = 1;
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar.Visible = false;
            // 
            // firstStatusLabel
            // 
            this.firstStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.firstStatusLabel.Name = "firstStatusLabel";
            this.firstStatusLabel.Size = new System.Drawing.Size(152, 20);
            this.firstStatusLabel.Text = "No files or directories";
            // 
            // secondStatusLabel
            // 
            this.secondStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.secondStatusLabel.Margin = new System.Windows.Forms.Padding(0, 4, 4, 2);
            this.secondStatusLabel.Name = "secondStatusLabel";
            this.secondStatusLabel.Size = new System.Drawing.Size(772, 20);
            this.secondStatusLabel.Spring = true;
            this.secondStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.secondStatusLabel.Click += new System.EventHandler(this.secondStatusLabel_Click);
            // 
            // checkListContextMenu
            // 
            this.checkListContextMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.checkListContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.selectAllToolStripMenuItem,
            this.unselectAllToolStripMenuItem,
            this.invertSelectedToolStripMenuItem,
            this.toolStripSeparator2,
            this.fullNameToolStripMenuItem,
            this.extensionToolStripMenuItem,
            this.sizeToolStripMenuItem,
            this.compressionToolStripMenuItem,
            this.ratioToolStripMenuItem,
            this.methodToolStripMenuItem,
            this.offsetToolStripMenuItem,
            this.sha256ToolStripMenuItem});
            this.checkListContextMenu.Name = "checkListContextMenu";
            this.checkListContextMenu.Size = new System.Drawing.Size(192, 296);
            // 
            // selectAllToolStripMenuItem
            // 
            this.selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
            this.selectAllToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A)));
            this.selectAllToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.selectAllToolStripMenuItem.Text = "Check All";
            this.selectAllToolStripMenuItem.Click += new System.EventHandler(this.selectAllToolStripMenuItem_Click);
            // 
            // unselectAllToolStripMenuItem
            // 
            this.unselectAllToolStripMenuItem.Name = "unselectAllToolStripMenuItem";
            this.unselectAllToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.unselectAllToolStripMenuItem.Text = "Uncheck All";
            this.unselectAllToolStripMenuItem.Click += new System.EventHandler(this.unselectAllToolStripMenuItem_Click);
            // 
            // invertSelectedToolStripMenuItem
            // 
            this.invertSelectedToolStripMenuItem.Name = "invertSelectedToolStripMenuItem";
            this.invertSelectedToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.invertSelectedToolStripMenuItem.Text = "Invert Checked";
            this.invertSelectedToolStripMenuItem.Click += new System.EventHandler(this.invertSelectedToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(188, 6);
            // 
            // fullNameToolStripMenuItem
            // 
            this.fullNameToolStripMenuItem.Checked = true;
            this.fullNameToolStripMenuItem.CheckOnClick = true;
            this.fullNameToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.fullNameToolStripMenuItem.Enabled = false;
            this.fullNameToolStripMenuItem.Name = "fullNameToolStripMenuItem";
            this.fullNameToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.fullNameToolStripMenuItem.Text = "Full Name";
            this.fullNameToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // extensionToolStripMenuItem
            // 
            this.extensionToolStripMenuItem.Checked = true;
            this.extensionToolStripMenuItem.CheckOnClick = true;
            this.extensionToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.extensionToolStripMenuItem.Name = "extensionToolStripMenuItem";
            this.extensionToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.extensionToolStripMenuItem.Text = "Extension";
            this.extensionToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // sizeToolStripMenuItem
            // 
            this.sizeToolStripMenuItem.Checked = true;
            this.sizeToolStripMenuItem.CheckOnClick = true;
            this.sizeToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.sizeToolStripMenuItem.Name = "sizeToolStripMenuItem";
            this.sizeToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.sizeToolStripMenuItem.Text = "Size";
            this.sizeToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // compressionToolStripMenuItem
            // 
            this.compressionToolStripMenuItem.Checked = true;
            this.compressionToolStripMenuItem.CheckOnClick = true;
            this.compressionToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.compressionToolStripMenuItem.Name = "compressionToolStripMenuItem";
            this.compressionToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.compressionToolStripMenuItem.Text = "Compression";
            this.compressionToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // ratioToolStripMenuItem
            // 
            this.ratioToolStripMenuItem.Checked = true;
            this.ratioToolStripMenuItem.CheckOnClick = true;
            this.ratioToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ratioToolStripMenuItem.Name = "ratioToolStripMenuItem";
            this.ratioToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.ratioToolStripMenuItem.Text = "Ratio";
            this.ratioToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // methodToolStripMenuItem
            // 
            this.methodToolStripMenuItem.CheckOnClick = true;
            this.methodToolStripMenuItem.Name = "methodToolStripMenuItem";
            this.methodToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.methodToolStripMenuItem.Text = "Method";
            this.methodToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // offsetToolStripMenuItem
            // 
            this.offsetToolStripMenuItem.CheckOnClick = true;
            this.offsetToolStripMenuItem.Name = "offsetToolStripMenuItem";
            this.offsetToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.offsetToolStripMenuItem.Text = "Offset";
            this.offsetToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // sha256ToolStripMenuItem
            // 
            this.sha256ToolStripMenuItem.CheckOnClick = true;
            this.sha256ToolStripMenuItem.Name = "sha256ToolStripMenuItem";
            this.sha256ToolStripMenuItem.Size = new System.Drawing.Size(191, 26);
            this.sha256ToolStripMenuItem.Text = "SHA-256";
            this.sha256ToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showHideToolStripMenuItem_CheckedChanged);
            // 
            // dialogOpenFolder
            // 
            this.dialogOpenFolder.Description = "Select folder containing the files to be compressed";
            this.dialogOpenFolder.RootFolder = System.Environment.SpecialFolder.MyComputer;
            this.dialogOpenFolder.ShowNewFolderButton = false;
            // 
            // dialogSaveHpi
            // 
            this.dialogSaveHpi.Filter = "HPI Files|*.hpi|CCX Files|*.ccx|UFO Files|*.ufo|GP3 Files|*.gp3";
            this.dialogSaveHpi.SupportMultiDottedExtensions = true;
            // 
            // dialogOpenHpi
            // 
            this.dialogOpenHpi.Filter = "All TA Files|*.hpi;*.ccx;*.ufo;*.gp?|HPI Files|*.hpi|CCX Files|*.ccx|UFO Files|*." +
    "ufo|GP Files|*.gp?|All Files|*.*";
            this.dialogOpenHpi.Multiselect = true;
            this.dialogOpenHpi.Title = "Select one or many HPI files to open";
            // 
            // dialogExtractToFolder
            // 
            this.dialogExtractToFolder.Description = "Folder to extract all checked files";
            this.dialogExtractToFolder.RootFolder = System.Environment.SpecialFolder.MyComputer;
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
            this.openFilesToolStripMenuItem.Click += new System.EventHandler(this.openFilesStripMenuItem_Click);
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
            this.toolStripCompressButton.ToolTipText = "Compress or Merge";
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
            this.flavorLevelComboBox.Size = new System.Drawing.Size(162, 59);
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
            // manageDuplicateNamesStripButton
            // 
            this.manageDuplicateNamesStripButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.keepFirstNameToolStripMenuItem,
            this.keepLastNameToolStripMenuItem,
            this.uncheckAllDuplicateNamesToolStripMenuItem});
            this.manageDuplicateNamesStripButton.Enabled = false;
            this.manageDuplicateNamesStripButton.Image = global::HPIZArchiver.Properties.Resources.Rules_32x;
            this.manageDuplicateNamesStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.manageDuplicateNamesStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.manageDuplicateNamesStripButton.Name = "manageDuplicateNamesStripButton";
            this.manageDuplicateNamesStripButton.Size = new System.Drawing.Size(151, 56);
            this.manageDuplicateNamesStripButton.Text = "Manage Duplicates";
            this.manageDuplicateNamesStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.manageDuplicateNamesStripButton.ToolTipText = "This menu is enabled if there are two or more files with the same name.";
            // 
            // keepFirstNameToolStripMenuItem
            // 
            this.keepFirstNameToolStripMenuItem.Checked = true;
            this.keepFirstNameToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.keepFirstNameToolStripMenuItem.Name = "keepFirstNameToolStripMenuItem";
            this.keepFirstNameToolStripMenuItem.ShowShortcutKeys = false;
            this.keepFirstNameToolStripMenuItem.Size = new System.Drawing.Size(278, 26);
            this.keepFirstNameToolStripMenuItem.Text = "Keep First Name";
            this.keepFirstNameToolStripMenuItem.ToolTipText = "Uncheck all duplicates except the first one";
            this.keepFirstNameToolStripMenuItem.Click += new System.EventHandler(this.keepFirstToolStripMenuItem_Click);
            // 
            // keepLastNameToolStripMenuItem
            // 
            this.keepLastNameToolStripMenuItem.Name = "keepLastNameToolStripMenuItem";
            this.keepLastNameToolStripMenuItem.ShowShortcutKeys = false;
            this.keepLastNameToolStripMenuItem.Size = new System.Drawing.Size(278, 26);
            this.keepLastNameToolStripMenuItem.Text = "Keep Last Name";
            this.keepLastNameToolStripMenuItem.Click += new System.EventHandler(this.keepLastToolStripMenuItem_Click);
            // 
            // uncheckAllDuplicateNamesToolStripMenuItem
            // 
            this.uncheckAllDuplicateNamesToolStripMenuItem.Name = "uncheckAllDuplicateNamesToolStripMenuItem";
            this.uncheckAllDuplicateNamesToolStripMenuItem.ShowShortcutKeys = false;
            this.uncheckAllDuplicateNamesToolStripMenuItem.Size = new System.Drawing.Size(278, 26);
            this.uncheckAllDuplicateNamesToolStripMenuItem.Text = "Uncheck All Duplicate Names";
            this.uncheckAllDuplicateNamesToolStripMenuItem.Click += new System.EventHandler(this.uncheckAllDuplicatesToolStripMenuItem_Click);
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
            this.HighlightsToolStripDropDownButton,
            this.toolStripSeparator5,
            this.manageDuplicateNamesStripButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(942, 59);
            this.toolStrip.TabIndex = 2;
            // 
            // HighlightsToolStripDropDownButton
            // 
            this.HighlightsToolStripDropDownButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.unknowFoldersExtensionToolStripMenuItem,
            this.toolStripSeparator6,
            this.duplicateNamesinYellowStripMenuItem,
            this.duplicateContentsToolStripMenuItem,
            this.duplicateNameContentToolStripMenuItem});
            this.HighlightsToolStripDropDownButton.Image = global::HPIZArchiver.Properties.Resources.Highlighter_32x;
            this.HighlightsToolStripDropDownButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.HighlightsToolStripDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.HighlightsToolStripDropDownButton.Name = "HighlightsToolStripDropDownButton";
            this.HighlightsToolStripDropDownButton.Size = new System.Drawing.Size(91, 56);
            this.HighlightsToolStripDropDownButton.Text = "Highlights";
            this.HighlightsToolStripDropDownButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            // 
            // unknowFoldersExtensionToolStripMenuItem
            // 
            this.unknowFoldersExtensionToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(192)))), ((int)(((byte)(192)))));
            this.unknowFoldersExtensionToolStripMenuItem.Checked = true;
            this.unknowFoldersExtensionToolStripMenuItem.CheckOnClick = true;
            this.unknowFoldersExtensionToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.unknowFoldersExtensionToolStripMenuItem.Name = "unknowFoldersExtensionToolStripMenuItem";
            this.unknowFoldersExtensionToolStripMenuItem.ShowShortcutKeys = false;
            this.unknowFoldersExtensionToolStripMenuItem.Size = new System.Drawing.Size(325, 26);
            this.unknowFoldersExtensionToolStripMenuItem.Text = "Unknow Folders or Extensions in Red";
            this.unknowFoldersExtensionToolStripMenuItem.CheckedChanged += new System.EventHandler(this.changeHighLightsToolStripMenuItem_Click);
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(322, 6);
            // 
            // duplicateNamesinYellowStripMenuItem
            // 
            this.duplicateNamesinYellowStripMenuItem.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.duplicateNamesinYellowStripMenuItem.Checked = true;
            this.duplicateNamesinYellowStripMenuItem.CheckOnClick = true;
            this.duplicateNamesinYellowStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.duplicateNamesinYellowStripMenuItem.Name = "duplicateNamesinYellowStripMenuItem";
            this.duplicateNamesinYellowStripMenuItem.ShowShortcutKeys = false;
            this.duplicateNamesinYellowStripMenuItem.Size = new System.Drawing.Size(325, 26);
            this.duplicateNamesinYellowStripMenuItem.Text = "Duplicate Names in Yellow";
            this.duplicateNamesinYellowStripMenuItem.CheckedChanged += new System.EventHandler(this.changeHighLightsToolStripMenuItem_Click);
            // 
            // duplicateContentsToolStripMenuItem
            // 
            this.duplicateContentsToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.duplicateContentsToolStripMenuItem.Checked = true;
            this.duplicateContentsToolStripMenuItem.CheckOnClick = true;
            this.duplicateContentsToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.duplicateContentsToolStripMenuItem.Name = "duplicateContentsToolStripMenuItem";
            this.duplicateContentsToolStripMenuItem.ShowShortcutKeys = false;
            this.duplicateContentsToolStripMenuItem.Size = new System.Drawing.Size(325, 26);
            this.duplicateContentsToolStripMenuItem.Text = "Duplicate Contents in Blue";
            this.duplicateContentsToolStripMenuItem.CheckedChanged += new System.EventHandler(this.changeHighLightsToolStripMenuItem_Click);
            // 
            // duplicateNameContentToolStripMenuItem
            // 
            this.duplicateNameContentToolStripMenuItem.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.duplicateNameContentToolStripMenuItem.Checked = true;
            this.duplicateNameContentToolStripMenuItem.CheckOnClick = true;
            this.duplicateNameContentToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.duplicateNameContentToolStripMenuItem.Name = "duplicateNameContentToolStripMenuItem";
            this.duplicateNameContentToolStripMenuItem.ShowShortcutKeys = false;
            this.duplicateNameContentToolStripMenuItem.Size = new System.Drawing.Size(325, 26);
            this.duplicateNameContentToolStripMenuItem.Text = "Duplicate Name+Content in Green";
            this.duplicateNameContentToolStripMenuItem.CheckedChanged += new System.EventHandler(this.changeHighLightsToolStripMenuItem_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(6, 59);
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
            this.columnRatio,
            this.columnMethod,
            this.columnOffset,
            this.columnSha256});
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
            this.listViewFiles.Size = new System.Drawing.Size(942, 463);
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
            this.columnCompressed.Width = 102;
            // 
            // columnRatio
            // 
            this.columnRatio.Tag = "";
            this.columnRatio.Text = "Ratio";
            this.columnRatio.Width = 48;
            // 
            // columnMethod
            // 
            this.columnMethod.Text = "Method";
            this.columnMethod.Width = 0;
            // 
            // columnOffset
            // 
            this.columnOffset.Text = "Offset";
            this.columnOffset.Width = 0;
            // 
            // columnSha256
            // 
            this.columnSha256.Text = "SHA-256";
            this.columnSha256.Width = 0;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(942, 548);
            this.Controls.Add(this.listViewFiles);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.toolStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "MainForm";
            this.Text = "HPIZ Archiver v1.3b";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.checkListContextMenu.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel firstStatusLabel;
        private System.Windows.Forms.ColumnHeader columnFullName;
        private System.Windows.Forms.ColumnHeader columnSize;
        private System.Windows.Forms.FolderBrowserDialog dialogOpenFolder;
        private System.Windows.Forms.SaveFileDialog dialogSaveHpi;
        private System.Windows.Forms.OpenFileDialog dialogOpenHpi;
        private System.Windows.Forms.FolderBrowserDialog dialogExtractToFolder;
        private System.Windows.Forms.SaveFileDialog dialogExtractFile;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ColumnHeader columnCompressed;
        private System.Windows.Forms.ColumnHeader columnRatio;
        private System.Windows.Forms.ToolStripStatusLabel secondStatusLabel;
        private System.Windows.Forms.ContextMenuStrip checkListContextMenu;
        private System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem unselectAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem invertSelectedToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnExt;
        private System.Windows.Forms.ColumnHeader columnChecked;
        private CollapsibleListView listViewFiles;
        private System.Windows.Forms.ToolStripDropDownButton toolStripOpenButton;
        private System.Windows.Forms.ToolStripMenuItem openFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openDirToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem closeAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton toolStripExtractButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripDropDownButton toolStripCompressButton;
        private System.Windows.Forms.ToolStripMenuItem compressCheckedFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mergeRepackCheckedFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripComboBox flavorLevelComboBox;
        private System.Windows.Forms.ToolStripLabel flavorStripLabel;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripDropDownButton manageDuplicateNamesStripButton;
        private System.Windows.Forms.ToolStripMenuItem keepFirstNameToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripMenuItem keepLastNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uncheckAllDuplicateNamesToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnMethod;
        private System.Windows.Forms.ColumnHeader columnSha256;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem fullNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extensionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ratioToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem methodToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sha256ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem compressionToolStripMenuItem;
        private System.Windows.Forms.ToolStripDropDownButton HighlightsToolStripDropDownButton;
        private System.Windows.Forms.ToolStripMenuItem duplicateNamesinYellowStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicateNameContentToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripMenuItem unknowFoldersExtensionToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ColumnHeader columnOffset;
        private System.Windows.Forms.ToolStripMenuItem offsetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicateContentsToolStripMenuItem;
    }
}

