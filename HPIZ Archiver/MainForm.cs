using HPIZ;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.WebRequestMethods;

namespace HPIZArchiver
{
    public partial class MainForm : Form
    {
        SortedSet<string> uniqueSources = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<ListViewItem>> uniqueNames = new Dictionary<string, List<ListViewItem>>(StringComparer.OrdinalIgnoreCase);

        Dictionary<int, List<ListViewItem>> toHashCandidates = new Dictionary<int, List<ListViewItem>>();
        Dictionary<string, List<ListViewItem>> sameContent = new Dictionary<string, List<ListViewItem>>();

        Stopwatch timer = new Stopwatch();

        long totalSize = 0;
        long totalCompressedSize = 0;

        DuplicateRules dRules = DuplicateRules.KeepFirst;
        enum DuplicateRules { KeepFirst, KeepLast, NoDuplicates }

        public MainForm()
        {
            InitializeComponent();
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
            openFilesToolStripMenuItem.Enabled = mode == ArchiverMode.Empty || mode == ArchiverMode.File;
            openDirToolStripMenuItem.Enabled = mode == ArchiverMode.Empty || mode == ArchiverMode.Dir;
            toolStripExtractButton.Enabled = mode == ArchiverMode.File;
            toolStripCompressButton.Enabled = mode == ArchiverMode.File || mode == ArchiverMode.Dir;
            flavorLevelComboBox.Enabled = mode == ArchiverMode.File || mode == ArchiverMode.Dir;
            flavorStripLabel.Enabled = mode == ArchiverMode.File || mode == ArchiverMode.Dir;
            compressCheckedFilesToolStripMenuItem.Enabled = mode == ArchiverMode.Dir;
            mergeRepackCheckedFilesToolStripMenuItem.Enabled = mode == ArchiverMode.File;
            closeAllToolStripMenuItem.Enabled = mode != ArchiverMode.Busy && mode != ArchiverMode.Empty;
            listViewFiles.Enabled = ! (mode == ArchiverMode.Busy || mode == ArchiverMode.Finish);
            toolStrip.Enabled = mode != ArchiverMode.Busy;
            progressBar.Visible = mode == ArchiverMode.Busy || mode == ArchiverMode.Finish;
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listViewFiles.Items.Clear();
            listViewFiles.Groups.Clear();
            uniqueSources.Clear();
            uniqueNames.Clear();
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
            foreach (var item in itemList)
            {
                if (!listViewFiles.Groups.Contains(item.Group))
                    listViewFiles.Groups.Add(item.Group);
                listViewFiles.Items.Add(item);

                if (uniqueNames.ContainsKey(item.SubItems[1].Text))
                {
                    uniqueNames[item.SubItems[1].Text].Add(item);
                    manageDuplicateNamesStripButton.Enabled = true;
                }
                else
                    uniqueNames.Add(item.SubItems[1].Text, new List<ListViewItem>() { item });
            }

            foreach (ListViewGroup group in listViewFiles.Groups)
                if (Directory.Exists(group.Name))
                {
                    foreach (var candidate in toHashCandidates.Values)
                        if (candidate.Count > 1 && candidate[0].Group == group && candidate[0].SubItems[8].Text == string.Empty)
                        {
                            candidate[0].SubItems[8].Text = Utils.CalculateSha256((string)candidate[0].SubItems[1].Tag);
                            if (sameContent.ContainsKey(candidate[0].SubItems[8].Text))
                                sameContent[candidate[0].SubItems[8].Text].Add(candidate[0]);
                            else sameContent.Add(candidate[0].SubItems[8].Text, new List<ListViewItem>() { candidate[0] });
                        }
                }
                else
                    using (HpiArchive hpia = HpiFile.Open(group.Name))
                        foreach (var candidate in toHashCandidates.Values)
                        if (candidate.Count > 1 && candidate[0].Group == group && candidate[0].SubItems[8].Text == string.Empty)
                        {
                            candidate[0].SubItems[8].Text = Utils.CalculateSha256(hpia.Entries[candidate[0].SubItems[1].Text].Uncompress());
                            if (sameContent.ContainsKey(candidate[0].SubItems[8].Text))
                                sameContent[candidate[0].SubItems[8].Text].Add(candidate[0]);
                            else sameContent.Add(candidate[0].SubItems[8].Text, new List<ListViewItem>() { candidate[0] });
                        }



            SetRule(dRules);
            SetHighliths();
            showHideColumns();
            listViewFiles.EndUpdate();
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
            if (dialogOpenHpi.ShowDialog() == DialogResult.OK)
            {
                SetMode(ArchiverMode.Busy);
                progressBar.Style = ProgressBarStyle.Marquee;
                firstStatusLabel.Text = "Loading file list from selected HPI file(s)...";

                string[] extensionOrder = { ".GP3", ".CCX", ".UFO", ".HPI" }; //File load order
                var orderedList = dialogOpenHpi.FileNames.OrderBy(x => {
                    var index = Array.IndexOf(extensionOrder, Path.GetExtension(x).ToUpper());
                    return index < 0 ? int.MaxValue : index; }).ToList();

                foreach (var file in orderedList)
                    if(!uniqueSources.Contains(file))
                    {
                        uniqueSources.Add(file);
                        var filesInfo = await Task.Run(() => GetListViewGroupItens(file));
                        AddToListViewFiles(filesInfo);
                    }
                UpdateStatusBarInfo();
                progressBar.Style = ProgressBarStyle.Continuous;
                SetMode(ArchiverMode.File);
            }
        }

        private async void directoryToCompressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenFolder.ShowDialog() == DialogResult.OK && !uniqueSources.Contains(dialogOpenFolder.SelectedPath))
            {
                uniqueSources.Add(dialogOpenFolder.SelectedPath);
                SetMode(ArchiverMode.Busy);
                progressBar.Style = ProgressBarStyle.Marquee;
                firstStatusLabel.Text = "Loading file list from selected directory...";
                var dirInfo = await Task.Run(() => GetListViewGroupItens(dialogOpenFolder.SelectedPath));
                AddToListViewFiles(dirInfo);
                UpdateStatusBarInfo();
                progressBar.Style = ProgressBarStyle.Continuous;
                SetMode(ArchiverMode.Dir);
            }
        }

        public List<ListViewItem> GetListViewGroupItens(string fullPath)
        {
            var listColection = new List<ListViewItem>();
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

                    totalSize += size;

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

                    if (sha256ToolStripMenuItem.Checked || toHashCandidates.ContainsKey(size))
                    {
                        var hash = Utils.CalculateSha256(file);
                        lvItem.SubItems[8].Text = hash;

                        if(!sha256ToolStripMenuItem.Checked)
                            toHashCandidates[size].Add(lvItem);

                        if (sameContent.ContainsKey(hash))
                            sameContent[hash].Add(lvItem);
                        else sameContent.Add(hash, new List<ListViewItem>() { lvItem } ) ;
                    }
                    else
                        toHashCandidates.Add(size, new List<ListViewItem>() { lvItem });
                }   
                            
            }
            else //HPI Files
            {
                var group = new ListViewGroup(fullPath, Path.GetExtension(fullPath).ToUpper() + " File - " + fullPath);
                using (HpiArchive hpia = HpiFile.Open(fullPath))
                {
                    foreach (var entry in hpia.Entries)
                    {
                        totalSize += entry.Value.UncompressedSize;
                        totalCompressedSize += entry.Value.CompressedSizeCount();

                        ListViewItem lvItem = new ListViewItem(new string[] {
                            String.Empty,
                            entry.Key,
                            Path.GetExtension(entry.Key).ToUpper(),
                            entry.Value.UncompressedSize.ToString("N0"),
                            entry.Value.CompressedSizeCount().ToString("N0"),
                            entry.Value.Ratio().ToString("P1"),
                            entry.Value.FlagCompression.ToString(),
                            entry.Value.OffsetOfCompressedData.ToString("X8"),
                            String.Empty
                        }, group);

                        lvItem.SubItems[1].Tag = fullPath;
                        lvItem.SubItems[3].Tag = entry.Value.UncompressedSize;
                        lvItem.SubItems[4].Tag = entry.Value.CompressedSizeCount();
                        lvItem.SubItems[5].Tag = entry.Value.Ratio();
                        lvItem.SubItems[6].Tag = entry.Value.FlagCompression;
                        lvItem.Tag = fullPath;
                        listColection.Add(lvItem);

                        if (sha256ToolStripMenuItem.Checked || toHashCandidates.ContainsKey(entry.Value.UncompressedSize))
                        {
                            var hash = Utils.CalculateSha256(entry.Value.Uncompress());
                            lvItem.SubItems[8].Text = hash;

                            if(!sha256ToolStripMenuItem.Checked)
                                toHashCandidates[entry.Value.UncompressedSize].Add(lvItem);

                            if (sameContent.ContainsKey(hash))
                                sameContent[hash].Add(lvItem);
                            else sameContent.Add(hash, new List<ListViewItem>() { lvItem });
                        }
                        else
                            toHashCandidates.Add(entry.Value.UncompressedSize, new List<ListViewItem>() { lvItem });
                    }
                }
            }
            return listColection;
        }

        private async void toolStripExtractButton_Click(object sender, EventArgs e)
        {
            if (listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Extract: No files have been checked in the list.");

            else if (dialogExtractToFolder.ShowDialog() == DialogResult.OK)
            {
                SetMode(ArchiverMode.Busy);
                firstStatusLabel.Text = "Extracting " + listViewFiles.CheckedItems.Count.ToString() + " files...";

                progressBar.Maximum = listViewFiles.CheckedItems.Count;

                var progress = new Progress<string>(percent =>
                {
                    progressBar.PerformStep();
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                var sources = GetCheckedFileNames();

                timer.Restart();

                await Task.Run(() => HpiFile.DoExtraction(sources, dialogExtractToFolder.SelectedPath, progress));

                timer.Stop();

                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}m {1}s {2}ms", timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                progressBar.Value = progressBar.Maximum;
                secondStatusLabel.Text = dialogExtractToFolder.SelectedPath;
                secondStatusLabel.IsLink = true;
                TaskbarProgress.FlashWindow(this.Handle, true);
                SetMode(ArchiverMode.Finish);
            }
        }

        public PathCollection GetCheckedFileNames()
        {
            PathCollection fileList = new PathCollection();

            foreach (ListViewItem item in listViewFiles.CheckedItems)
            {
                if (fileList.ContainsKey(item.Group.Name))
                    fileList[item.Group.Name].Add(item.SubItems[1].Text);
                else
                    fileList.Add(item.Group.Name, new SortedSet<string>() { item.SubItems[1].Text });
            }

            return fileList;
        }

        private async void compressCheckedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Compress: No files have been checked in the list.");

            else if (dialogSaveHpi.ShowDialog() == DialogResult.OK)
            {
                SetMode(ArchiverMode.Busy);

                firstStatusLabel.Text = "Compressing... Last processed:";

                //Calculate total size and number of chunks
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                    progressBar.Maximum += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);

                CompressionMethod flavor;
                Enum.TryParse(flavorLevelComboBox.Text, out flavor);

                var progress = new Progress<string>(last =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.PerformStep();
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                var sources = GetCheckedFileNames();

                timer.Restart();

                await Task.Run(() => HpiFile.CreateFromManySources(sources, dialogSaveHpi.FileName, flavor, progress));

                timer.Stop();

                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}h {1}m {2}s {3}ms", timer.Elapsed.Hours, timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                progressBar.Value = progressBar.Maximum;
                secondStatusLabel.Text = dialogSaveHpi.FileName;
                secondStatusLabel.IsLink = true;
                TaskbarProgress.FlashWindow(this.Handle, true);
                SetMode(ArchiverMode.Finish);
            }
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
                if(listViewFiles.Tag.Equals("D"))
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

        private async void mergeRepackCheckedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Merge/Repack: No files have been checked in the list.");

            else if (dialogSaveHpi.ShowDialog() == DialogResult.OK)
            {
                SetMode(ArchiverMode.Busy);

                firstStatusLabel.Text = "Compressing... Last processed:";

                CompressionMethod flavor;
                Enum.TryParse(flavorLevelComboBox.Text, out flavor);

                //Calculate total size and number of chunks to max progressbar
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                    progressBar.Maximum += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);

                var progress = new Progress<string>(last =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.PerformStep();
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                var sources = GetCheckedFileNames();

                timer.Restart();

                await Task.Run(() => HpiFile.CreateFromManySources(sources, dialogSaveHpi.FileName, flavor, progress));

                timer.Stop();
                
                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}h {1}m {2}s {3}ms", timer.Elapsed.Hours, timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                progressBar.Value = progressBar.Maximum;
                secondStatusLabel.Text = dialogSaveHpi.FileName;
                secondStatusLabel.IsLink = true;
                TaskbarProgress.FlashWindow(this.Handle, true);
                SetMode(ArchiverMode.Finish);
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
                        if(item.Value.Count == 1)
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
            if(secondStatusLabel.IsLink)
                Process.Start("explorer.exe", "/select," + secondStatusLabel.Text);
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
            foreach (var ocurrence in uniqueNames.Values)
                if (ocurrence.Count > 1)
                    foreach (var item in ocurrence)
                        if (duplicateNamesinYellowStripMenuItem.Checked)
                            item.BackColor = duplicateNamesinYellowStripMenuItem.BackColor;
                        else item.BackColor = default(Color);

                foreach (var ocurrence in sameContent.Values)
                    if (ocurrence.Count > 1)
                        foreach (var item in ocurrence)
                            if (duplicateNameContentToolStripMenuItem.Checked)
                                if (item.BackColor == duplicateNamesinYellowStripMenuItem.BackColor)
                                    item.BackColor = duplicateNameContentToolStripMenuItem.BackColor;
                                else if(duplicateContentsToolStripMenuItem.Checked)
                                    item.BackColor = duplicateContentsToolStripMenuItem.BackColor;
                else if(item.BackColor == duplicateContentsToolStripMenuItem.BackColor)
                                item.BackColor = default(Color);
                            else if(item.BackColor == duplicateNameContentToolStripMenuItem.BackColor)
                                item.BackColor = default(Color);


            foreach (ListViewItem item in listViewFiles.Items)
                    if (!FolderExtension.CheckKnow(item.SubItems[1].Text))
                    if (unknowFoldersExtensionToolStripMenuItem.Checked)
                        item.BackColor = unknowFoldersExtensionToolStripMenuItem.BackColor;
                else if (item.BackColor == unknowFoldersExtensionToolStripMenuItem.BackColor)
                        item.BackColor = default(Color);
        }
    }
}



