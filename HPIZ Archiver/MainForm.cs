using HPIZ;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HPIZArchiver
{
    public partial class MainForm : Form
    {
        private const string ArchiveOpenFilter =
            "All TA Files|*.hpi;*.ccx;*.ufo;*.gp?|HPI Files|*.hpi|CCX Files|*.ccx|" +
            "UFO Files|*.ufo|GP Files|*.gp?|All Files|*.*";
        private const string ArchiveSaveFilter =
            "HPI Files|*.hpi|CCX Files|*.ccx|UFO Files|*.ufo|GP3 Files|*.gp3";

        SortedSet<string> uniqueSources = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<ListViewItem>> uniqueNames = new Dictionary<string, List<ListViewItem>>(StringComparer.OrdinalIgnoreCase);

        Dictionary<int, List<ListViewItem>> toHashCandidates = new Dictionary<int, List<ListViewItem>>();
        Dictionary<string, List<ListViewItem>> sameContent = new Dictionary<string, List<ListViewItem>>();

        Dictionary<string, HpiArchive> cachedHPI = new Dictionary<string, HpiArchive>(StringComparer.OrdinalIgnoreCase);

        Stopwatch timer = new Stopwatch();

        long totalSize = 0;
        long totalCompressedSize = 0;

        DuplicateRules dRules = DuplicateRules.KeepFirst;
        enum DuplicateRules { KeepFirst, KeepLast, NoDuplicates }
        ArchiverMode currentMode = ArchiverMode.Empty;

        public MainForm()
        {
            InitializeComponent();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DisposeCachedArchives();
            base.OnFormClosed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (currentMode == ArchiverMode.Busy)
            {
                e.Cancel = true;
                MessageBox.Show(
                    this,
                    "Wait for the current operation to finish before closing the application.",
                    "Operation in progress",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            base.OnFormClosing(e);
        }

        private void DisposeCachedArchives()
        {
            foreach (var archive in cachedHPI.Values)
                archive.Dispose();

            cachedHPI.Clear();
        }

        private void ShowOperationErrors(string operation, IEnumerable<FileOperationError> errors)
        {
            var errorList = errors.ToList();
            if (errorList.Count == 0)
                return;

            var message = new StringBuilder();
            message.AppendLine(operation + " completed with " + errorList.Count + " error(s):");
            message.AppendLine();

            foreach (var error in errorList.Take(10))
                message.AppendLine(error.FilePath + ": " + error.Exception.Message);

            if (errorList.Count > 10)
                message.AppendLine("...and " + (errorList.Count - 10) + " more error(s).");

            MessageBox.Show(this, message.ToString(), operation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowOperationException(string operation, Exception exception)
        {
            MessageBox.Show(this, exception.GetBaseException().Message, operation, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void BeginBusyOperation(string status, ProgressBarStyle progressStyle)
        {
            SetMode(ArchiverMode.Busy);
            progressBar.Style = progressStyle;
            progressBar.Value = 0;
            firstStatusLabel.Text = status;
            secondStatusLabel.Text = string.Empty;
            secondStatusLabel.IsLink = false;
            TaskbarProgress.SetState(
                Handle,
                progressStyle == ProgressBarStyle.Marquee
                    ? TaskbarProgress.ProgressState.Indeterminate
                    : TaskbarProgress.ProgressState.Normal);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            flavorLevelComboBox.Items.AddRange(Enum.GetNames(typeof(CompressionMethod)));
            flavorLevelComboBox.Items.RemoveAt(1); //Remove LZ77 compression method
            flavorLevelComboBox.SelectedIndex = 4;
        }
        enum ArchiverMode { Empty, Busy, File, Dir, Finish }

        private void SetMode(ArchiverMode mode)
        {
            currentMode = mode;
            openFilesToolStripMenuItem.Enabled = mode == ArchiverMode.Empty || mode == ArchiverMode.File;
            openDirToolStripMenuItem.Enabled = mode == ArchiverMode.Empty || mode == ArchiverMode.Dir;
            toolStripExtractButton.Enabled = mode == ArchiverMode.File;
            toolStripCompressButton.Enabled = mode == ArchiverMode.File || mode == ArchiverMode.Dir;
            flavorLevelComboBox.Enabled = mode == ArchiverMode.File || mode == ArchiverMode.Dir;
            flavorStripLabel.Enabled = mode == ArchiverMode.File || mode == ArchiverMode.Dir;
            compressCheckedFilesToolStripMenuItem.Enabled = mode == ArchiverMode.Dir;
            mergeRepackCheckedFilesToolStripMenuItem.Enabled = mode == ArchiverMode.File;
            closeAllToolStripMenuItem.Enabled = mode != ArchiverMode.Busy && mode != ArchiverMode.Empty;
            listViewFiles.Enabled = !(mode == ArchiverMode.Busy || mode == ArchiverMode.Finish);
            toolStrip.Enabled = mode != ArchiverMode.Busy;
            progressBar.Visible = mode == ArchiverMode.Busy || mode == ArchiverMode.Finish;
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listViewFiles.Items.Clear();
            listViewFiles.Groups.Clear();
            uniqueSources.Clear();
            uniqueNames.Clear();
            DisposeCachedArchives();
            toHashCandidates.Clear();
            sameContent.Clear();
            totalSize = 0;
            totalCompressedSize = 0;
            dRules = DuplicateRules.KeepFirst;
            manageDuplicateNamesStripButton.Enabled = false;
            SetMode(ArchiverMode.Empty);
            firstStatusLabel.Text = "No files or directories opened";
            secondStatusLabel.Text = string.Empty;
            secondStatusLabel.IsLink = false;
            progressBar.Value = 0;
            TaskbarProgress.SetState(this.Handle, TaskbarProgress.ProgressState.None);
        }

        private void AddToListViewFiles(List<ListViewItem> itemList)
        {
            listViewFiles.BeginUpdate();
            try
            {
                foreach (var item in itemList)
                {
                    if (!listViewFiles.Groups.Contains(item.Group))
                        listViewFiles.Groups.Add(item.Group);
                    listViewFiles.Items.Add(item);

                    int size = (int)item.SubItems[3].Tag;
                    totalSize += size;
                    if (item.SubItems[4].Tag is int)
                        totalCompressedSize += (int)item.SubItems[4].Tag;

                    string hash = item.SubItems[8].Text;
                    List<ListViewItem> sizeCandidates;
                    if (!toHashCandidates.TryGetValue(size, out sizeCandidates))
                    {
                        sizeCandidates = new List<ListViewItem>();
                        toHashCandidates.Add(size, sizeCandidates);
                    }
                    sizeCandidates.Add(item);

                    if (!string.IsNullOrEmpty(hash))
                    {
                        List<ListViewItem> contentMatches;
                        if (!sameContent.TryGetValue(hash, out contentMatches))
                        {
                            contentMatches = new List<ListViewItem>();
                            sameContent.Add(hash, contentMatches);
                        }
                        contentMatches.Add(item);
                    }

                    if (uniqueNames.ContainsKey(item.SubItems[1].Text))
                    {
                        uniqueNames[item.SubItems[1].Text].Add(item);
                        manageDuplicateNamesStripButton.Enabled = true;
                    }
                    else
                        uniqueNames.Add(item.SubItems[1].Text, new List<ListViewItem>() { item });
                }

                foreach (ListViewGroup group in listViewFiles.Groups)
                    foreach (var candidate in toHashCandidates.Values)
                        if (candidate.Count > 1 && candidate[0].Group == group && candidate[0].SubItems[8].Text == string.Empty)
                        {
                            string hash = CalculateItemHash(candidate[0]);
                            candidate[0].SubItems[8].Text = hash;
                            if (!string.IsNullOrEmpty(hash))
                            {
                                if (sameContent.ContainsKey(hash))
                                    sameContent[hash].Add(candidate[0]);
                                else
                                    sameContent.Add(hash, new List<ListViewItem>() { candidate[0] });
                            }
                        }

                SetRule(dRules);
                SetHighliths();
                showHideColumns();
            }
            finally
            {
                listViewFiles.EndUpdate();
            }
        }

        private string CalculateItemHash(ListViewItem item)
        {
            try
            {
                if (Directory.Exists(item.Group.Name))
                    return HashUtility.CalculateSha256(Path.Combine(item.Group.Name, item.SubItems[1].Text));

                return HashUtility.CalculateSha256(cachedHPI[item.Group.Name].Entries[item.SubItems[1].Text].Uncompress());
            }
            catch
            {
                return string.Empty;
            }
        }

        internal void UpdateStatusBarInfo()
        {
            firstStatusLabel.Text = "Total: " + listViewFiles.Items.Count.ToString() + " file(s); " + Utils.SizeSuffix(totalSize);
            if (totalCompressedSize > 0)
                firstStatusLabel.Text += " > " + Utils.SizeSuffix(totalCompressedSize)
                            + " (" + ((float)totalCompressedSize / totalSize).ToString("P1") + ") inside "
                            + listViewFiles.Groups.Count.ToString() + " opened archive(s).";

            else
                firstStatusLabel.Text += " (uncompressed) inside " + listViewFiles.Groups.Count.ToString() + " opened directory(ies).";

            int duplicatedContent = 0;
            foreach (var duplicates in sameContent.Values)
                if (duplicates.Count > 1)
                    duplicatedContent += duplicates.Count;


            secondStatusLabel.Text = "Duplicate names: " + (listViewFiles.Items.Count - uniqueNames.Count).ToString() +
            " | Duplicate content: " + duplicatedContent.ToString();
        }

        internal async void openFilesStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] selectedFiles = ModernDialogs.OpenFiles(
                this,
                "Select one or many HPI files to open",
                ArchiveOpenFilter);

            if (selectedFiles.Length > 0)
            {
                BeginBusyOperation("Loading file list from selected HPI file(s)...", ProgressBarStyle.Marquee);
                var errors = new List<FileOperationError>();

                string[] extensionOrder = { ".GP3", ".CCX", ".UFO", ".HPI" }; //File load order
                var orderedList = selectedFiles.OrderBy(x =>
                {
                    var index = Array.IndexOf(extensionOrder, Path.GetExtension(x).ToUpper());
                    return index < 0 ? int.MaxValue : index;
                }).ToList();

                try
                {
                    bool calculateAllHashes = sha256ToolStripMenuItem.Checked;
                    foreach (var file in orderedList)
                        if (!uniqueSources.Contains(file))
                        {
                            HpiArchive openedArchive = null;
                            try
                            {
                                openedArchive = HpiFile.Open(file);
                                cachedHPI.Add(file, openedArchive);
                                uniqueSources.Add(file);

                                var knownCandidateSizes = new HashSet<int>(toHashCandidates.Keys);
                                var filesInfo = await Task.Run(
                                    () => GetListViewGroupItens(file, calculateAllHashes, knownCandidateSizes));
                                AddToListViewFiles(filesInfo);
                                openedArchive = null;
                            }
                            catch (Exception ex)
                            {
                                HpiArchive archive;
                                if (cachedHPI.TryGetValue(file, out archive))
                                {
                                    archive.Dispose();
                                    cachedHPI.Remove(file);
                                }
                                else
                                {
                                    openedArchive?.Dispose();
                                }
                                uniqueSources.Remove(file);
                                errors.Add(new FileOperationError(file, ex));
                            }
                        }

                    UpdateStatusBarInfo();
                    ShowOperationErrors("Open archives", errors);
                }
                catch (Exception ex)
                {
                    ShowOperationException("Open archives", ex);
                }
                finally
                {
                    progressBar.Style = ProgressBarStyle.Continuous;
                    TaskbarProgress.SetState(Handle, TaskbarProgress.ProgressState.None);
                    SetMode(listViewFiles.Groups.Count > 0 ? ArchiverMode.File : ArchiverMode.Empty);
                }
            }
        }

        private async void directoryToCompressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string selectedDirectory = ModernDialogs.SelectFolder(
                this,
                "Select folder containing the files to be compressed");

            if (selectedDirectory != null && !uniqueSources.Contains(selectedDirectory))
            {
                BeginBusyOperation("Loading file list from selected directory...", ProgressBarStyle.Marquee);
                try
                {
                    uniqueSources.Add(selectedDirectory);
                    bool calculateAllHashes = sha256ToolStripMenuItem.Checked;
                    var knownCandidateSizes = new HashSet<int>(toHashCandidates.Keys);
                    var dirInfo = await Task.Run(
                        () => GetListViewGroupItens(selectedDirectory, calculateAllHashes, knownCandidateSizes));
                    AddToListViewFiles(dirInfo);
                    UpdateStatusBarInfo();
                }
                catch (Exception ex)
                {
                    uniqueSources.Remove(selectedDirectory);
                    ShowOperationException("Open directory", ex);
                }
                finally
                {
                    progressBar.Style = ProgressBarStyle.Continuous;
                    TaskbarProgress.SetState(Handle, TaskbarProgress.ProgressState.None);
                    SetMode(listViewFiles.Groups.Count > 0 ? ArchiverMode.Dir : ArchiverMode.Empty);
                }
            }
        }

        public List<ListViewItem> GetListViewGroupItens(
            string fullPath,
            bool calculateAllHashes,
            ISet<int> knownCandidateSizes)
        {
            var listColection = new List<ListViewItem>();
            var currentGroupSizes = new HashSet<int>();
            //Check if path is a directory or file
            if (Directory.Exists(fullPath)) //Directory
            {
                var fileList = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
                var group = new ListViewGroup(fullPath, "Directory - " + fullPath);
                foreach (var file in fileList)
                {
                    int size = 0;
                    try
                    {
                        size = Convert.ToInt32(new FileInfo(file).Length);
                    }
                    catch (OverflowException)
                    {
                        throw new OverflowException(file + " is too large. File maximum size is 2GB (2 147 483 647 bytes).");
                    }

                    ListViewItem lvItem = new ListViewItem(new string[] {
                        String.Empty,
                        file.Substring(fullPath.Length + 1),
                        Path.GetExtension(file).ToUpper(),
                        size.ToString("N0"),
                        String.Empty,
                        String.Empty,
                        String.Empty,
                        String.Empty,
                        String.Empty
                        }, group);

                    lvItem.SubItems[1].Tag = fullPath;
                    lvItem.SubItems[3].Tag = size;
                    lvItem.Tag = fullPath;
                    listColection.Add(lvItem);

                    bool sizeAlreadySeen = knownCandidateSizes.Contains(size) || !currentGroupSizes.Add(size);
                    if (calculateAllHashes || sizeAlreadySeen)
                    {
                        var hash = CalculateHashOrEmpty(file);
                        lvItem.SubItems[8].Text = hash;
                    }
                }

            }
            else //HPI Files
            {
                var group = new ListViewGroup(fullPath, Path.GetExtension(fullPath).ToUpper() + " File - " + fullPath);
                foreach (var entry in cachedHPI[fullPath].Entries)
                {
                    ListViewItem lvItem = new ListViewItem(new string[] {
                        String.Empty,
                        entry.Key,
                        Path.GetExtension(entry.Key).ToUpper(),
                        entry.Value.UncompressedSize.ToString("N0"),
                        entry.Value.CompressedSizeCount().ToString("N0"),
                        entry.Value.Ratio().ToString("P1"),
                        entry.Value.FlagCompression.ToString(),
                        entry.Value.CompressedDataOffset.ToString("X8"),
                        String.Empty
                    }, group);

                    lvItem.SubItems[1].Tag = fullPath;
                    lvItem.SubItems[3].Tag = entry.Value.UncompressedSize;
                    lvItem.SubItems[4].Tag = entry.Value.CompressedSizeCount();
                    lvItem.SubItems[5].Tag = entry.Value.Ratio();
                    lvItem.SubItems[6].Tag = entry.Value.FlagCompression;
                    lvItem.Tag = fullPath;
                    listColection.Add(lvItem);

                    Debug.WriteLine(entry.Key + entry.Value.CompressedDataOffset.ToString());

                    bool sizeAlreadySeen = knownCandidateSizes.Contains(entry.Value.UncompressedSize)
                        || !currentGroupSizes.Add(entry.Value.UncompressedSize);
                    if (calculateAllHashes || sizeAlreadySeen)
                    {
                        var hash = CalculateHashOrEmpty(entry.Value);
                        lvItem.SubItems[8].Text = hash;
                    }
                }
            }
            return listColection;
        }

        private static string CalculateHashOrEmpty(string fileName)
        {
            try
            {
                return HashUtility.CalculateSha256(fileName);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CalculateHashOrEmpty(FileEntry entry)
        {
            try
            {
                return HashUtility.CalculateSha256(entry.Uncompress());
            }
            catch
            {
                return string.Empty;
            }
        }

        private async void toolStripExtractButton_Click(object sender, EventArgs e)
        {
            if (listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Extract: No files have been checked in the list.");

            else
            {
                string selectedDirectory = ModernDialogs.SelectFolder(
                    this,
                    "Select folder for the extracted files");
                if (selectedDirectory == null)
                    return;

                BeginBusyOperation(
                    "Extracting " + listViewFiles.CheckedItems.Count.ToString() + " files...",
                    ProgressBarStyle.Continuous);
                progressBar.Maximum = listViewFiles.CheckedItems.Count;

                var progress = new CoalescingProgress<string>((filePath, completedCount) =>
                {
                    progressBar.Value = Math.Min(
                        progressBar.Maximum,
                        progressBar.Value + completedCount);
                    secondStatusLabel.Text = filePath;
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                var sources = GetCheckedFileNames();

                timer.Restart();
                try
                {
                    ExtractionResult result = await Task.Run(
                        () => HpiFile.DoExtraction(sources, selectedDirectory, progress, cachedHPI));

                    timer.Stop();
                    firstStatusLabel.Text = String.Format(
                        "Done! Extracted {0} file(s). Elapsed time: {1}m {2}s {3}ms",
                        result.ExtractedFileCount,
                        timer.Elapsed.Minutes,
                        timer.Elapsed.Seconds,
                        timer.Elapsed.Milliseconds);
                    progressBar.Value = progressBar.Maximum;
                    secondStatusLabel.Text = selectedDirectory;
                    secondStatusLabel.IsLink = true;
                    TaskbarProgress.FlashWindow(this.Handle, true);
                    TaskbarProgress.SetState(
                        Handle,
                        result.Errors.Count == 0
                            ? TaskbarProgress.ProgressState.Normal
                            : TaskbarProgress.ProgressState.Error);
                    SetMode(ArchiverMode.Finish);
                    ShowOperationErrors("Extraction", result.Errors);
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    TaskbarProgress.SetState(Handle, TaskbarProgress.ProgressState.Error);
                    SetMode(ArchiverMode.File);
                    ShowOperationException("Extraction", ex);
                }
            }
        }

        public FilePathCollection GetCheckedFileNames()
        {
            FilePathCollection fileList = new FilePathCollection();

            foreach (ListViewItem item in listViewFiles.CheckedItems)
                fileList.AddOrReplace(item.SubItems[1].Text, item.Group.Name);

            return fileList;
        }

        public async Task CompressOrRepackCheckedFilesAsync()
        {
            string destinationFile = ModernDialogs.SaveFile(
                this,
                "Create HPI archive",
                ArchiveSaveFilter,
                "hpi");
            if (destinationFile == null)
                return;

            BeginBusyOperation("Compressing... Last processed:", ProgressBarStyle.Continuous);
            try
            {
                CompressionMethod flavor;
                if (!Enum.TryParse(flavorLevelComboBox.Text, out flavor))
                    throw new InvalidOperationException("Select a valid compression method.");

                var sources = GetCheckedFileNames();
                var duplicateResults = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var sames in sameContent.Values)
                {
                    string first = string.Empty;
                    var orderedSames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in sames)
                        if (item.Checked)
                            orderedSames.Add(item.SubItems[1].Text);

                    foreach (var item in orderedSames)
                        if (first == string.Empty)
                            first = item;
                        else if (!duplicateResults.ContainsKey(item))
                            duplicateResults.Add(item, first);
                }

                long totalChunks = 0;
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                    if (!duplicateResults.ContainsKey(item.SubItems[1].Text))
                        totalChunks += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);

                if (totalChunks > int.MaxValue)
                    throw new InvalidOperationException("The selected files require more progress steps than the interface supports.");

                progressBar.Maximum = Math.Max(1, (int)totalChunks);

                var progress = new CoalescingProgress<string>((last, completedCount) =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.Value = Math.Min(
                        progressBar.Maximum,
                        progressBar.Value + completedCount);
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                timer.Restart();
                ArchiveCreationResult result = await Task.Run(
                    () => HpiFile.CreateFromManySources(
                        sources,
                        destinationFile,
                        flavor,
                        progress,
                        cachedHPI,
                        duplicateResults));

                timer.Stop();
                firstStatusLabel.Text = String.Format(
                    "Done! Added {0} file(s). Elapsed time: {1}h {2}m {3}s {4}ms",
                    result.AddedFileCount,
                    timer.Elapsed.Hours,
                    timer.Elapsed.Minutes,
                    timer.Elapsed.Seconds,
                    timer.Elapsed.Milliseconds);
                progressBar.Value = progressBar.Maximum;
                secondStatusLabel.Text = destinationFile;
                secondStatusLabel.IsLink = true;
                TaskbarProgress.FlashWindow(this.Handle, true);
                TaskbarProgress.SetState(
                    Handle,
                    result.Errors.Count == 0
                        ? TaskbarProgress.ProgressState.Normal
                        : TaskbarProgress.ProgressState.Error);
                SetMode(ArchiverMode.Finish);

                if (result.ExceedsRecommendedSize)
                {
                    MessageBox.Show(
                        this,
                        "The HPI file was created, but its size exceeds 2GB (2,147,483,647 bytes). A fatal error may occur when loading the game.",
                        "Oversize Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                ShowOperationErrors("Compression", result.Errors);
            }
            catch (Exception ex)
            {
                timer.Stop();
                TaskbarProgress.SetState(Handle, TaskbarProgress.ProgressState.Error);
                SetMode(listViewFiles.Groups.Count > 0 && Directory.Exists(listViewFiles.Groups[0].Name)
                    ? ArchiverMode.Dir
                    : ArchiverMode.File);
                ShowOperationException("Compression", ex);
            }
        }

        private async void compressCheckedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Compress: No files have been checked in the list.");
            else
                await CompressOrRepackCheckedFilesAsync();
        }

        private async void mergeRepackCheckedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Merge/Repack: No files have been checked in the list.");
            else
                await CompressOrRepackCheckedFilesAsync();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listViewFiles.Items.Count; i++)
                listViewFiles.Items[i].Checked = true;
        }

        private void unselectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listViewFiles.Items.Count; i++)
                listViewFiles.Items[i].Checked = false;
        }

        private void invertSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listViewFiles.Items.Count; i++)
                listViewFiles.Items[i].Checked = !listViewFiles.Items[i].Checked;
        }

        private void listViewFiles_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (listViewFiles.Items.Count > 0 && listViewFiles.Items[0].SubItems.Count > e.Column)
            {
                if (listViewFiles.Tag.Equals("D"))
                {
                    if (e.Column == 0) listViewFiles.ListViewItemSorter = new ListViewItemCheckComparerAsc(e.Column);
                    if (e.Column == 1 || e.Column == 2 || e.Column == 6 || e.Column == 7 || e.Column == 8) listViewFiles.ListViewItemSorter = new ListViewItemStringComparerAsc(e.Column);
                    if (e.Column == 3 || e.Column == 4) listViewFiles.ListViewItemSorter = new ListViewItemIntComparerAsc(e.Column);
                    if (e.Column == 5) listViewFiles.ListViewItemSorter = new ListViewItemFloatComparerAsc(e.Column);
                    listViewFiles.Tag = "A";
                }
                else
                {
                    if (e.Column == 0) listViewFiles.ListViewItemSorter = new ListViewItemCheckComparerDesc(e.Column);
                    if (e.Column == 1 || e.Column == 2 || e.Column == 6 || e.Column == 7 || e.Column == 8) listViewFiles.ListViewItemSorter = new ListViewItemStringComparerDesc(e.Column);
                    if (e.Column == 3 || e.Column == 4) listViewFiles.ListViewItemSorter = new ListViewItemIntComparerDesc(e.Column);
                    if (e.Column == 5) listViewFiles.ListViewItemSorter = new ListViewItemFloatComparerDesc(e.Column);
                    listViewFiles.Tag = "D";
                }
            }
        }

        private void SetRule(DuplicateRules rule)
        {
            dRules = rule;
            keepFirstNameToolStripMenuItem.Checked = rule == DuplicateRules.KeepFirst;
            keepLastNameToolStripMenuItem.Checked = rule == DuplicateRules.KeepLast;
            uncheckAllDuplicateNamesToolStripMenuItem.Checked = rule == DuplicateRules.NoDuplicates;

            listViewFiles.BeginUpdate();

            switch (rule)
            {
                case DuplicateRules.KeepFirst:
                    foreach (var item in uniqueNames)
                    {
                        item.Value.First().Checked = true;
                        for (int i = 1; i < item.Value.Count; i++)
                            item.Value[i].Checked = false;
                    }
                    break;
                case DuplicateRules.KeepLast:
                    foreach (var item in uniqueNames)
                    {
                        item.Value.Last().Checked = true;
                        for (int i = 0; i < item.Value.Count - 1; i++)
                            item.Value[i].Checked = false;
                    }
                    break;
                case DuplicateRules.NoDuplicates:
                    foreach (var item in uniqueNames)
                    {
                        if (item.Value.Count == 1)
                            item.Value.First().Checked = true;
                        else
                            for (int i = 0; i < item.Value.Count; i++)
                                item.Value[i].Checked = false;
                    }
                    break;
            }

            listViewFiles.EndUpdate();
        }

        private void keepFirstToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dRules != DuplicateRules.KeepFirst)
                SetRule(DuplicateRules.KeepFirst);
        }

        private void keepLastToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dRules != DuplicateRules.KeepLast)
                SetRule(DuplicateRules.KeepLast);
        }

        private void uncheckAllDuplicatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dRules != DuplicateRules.NoDuplicates)
                SetRule(DuplicateRules.NoDuplicates);
        }

        private void secondStatusLabel_Click(object sender, EventArgs e)
        {
            if (secondStatusLabel.IsLink)
                Process.Start("explorer.exe", "/select,\"" + secondStatusLabel.Text + "\"");
        }

        private void showHideColumns()
        {
            listViewFiles.BeginUpdate();
            listViewFiles.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            listViewFiles.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            if (!extensionToolStripMenuItem.Checked) listViewFiles.Columns[2].Width = 0;
            if (!sizeToolStripMenuItem.Checked) listViewFiles.Columns[3].Width = 0;
            if (!compressionToolStripMenuItem.Checked) listViewFiles.Columns[4].Width = 0;
            if (!ratioToolStripMenuItem.Checked) listViewFiles.Columns[5].Width = 0;
            if (!methodToolStripMenuItem.Checked) listViewFiles.Columns[6].Width = 0;
            if (!offsetToolStripMenuItem.Checked) listViewFiles.Columns[7].Width = 0;
            if (!sha256ToolStripMenuItem.Checked) listViewFiles.Columns[8].Width = 0;

            listViewFiles.EndUpdate();
        }

        private void showHideToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            showHideColumns();
        }

        private void changeHighLightsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetHighliths();
        }

        void SetHighliths()
        {
            foreach (ListViewItem item in listViewFiles.Items)
                item.BackColor = default(Color);

            foreach (var ocurrence in uniqueNames.Values)
                if (ocurrence.Count > 1)
                    foreach (var item in ocurrence)
                        if (duplicateNamesinYellowStripMenuItem.Checked)
                            item.BackColor = duplicateNamesinYellowStripMenuItem.BackColor;

            foreach (var ocurrence in sameContent.Values)
                if (ocurrence.Count > 1)
                    foreach (var item in ocurrence)
                        if (item.BackColor == duplicateNamesinYellowStripMenuItem.BackColor
                            && duplicateNameContentToolStripMenuItem.Checked)
                            item.BackColor = duplicateNameContentToolStripMenuItem.BackColor;
                        else if (duplicateContentsToolStripMenuItem.Checked)
                            item.BackColor = duplicateContentsToolStripMenuItem.BackColor;


            foreach (ListViewItem item in listViewFiles.Items)
                if (!DirectoryExtensionPair.IsDirectoryExtensionKnow(item.SubItems[1].Text))
                    if (unknowFoldersExtensionToolStripMenuItem.Checked)
                        item.BackColor = unknowFoldersExtensionToolStripMenuItem.BackColor;
        }
    }
}



