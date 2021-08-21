using HPIZ;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HPIZArchiver
{
    public partial class MainForm : Form
    {
        SortedSet<string> uniqueFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<ListViewItem>> uniqueItens = new Dictionary<string, List<ListViewItem>>(StringComparer.OrdinalIgnoreCase);

        Stopwatch timer = new Stopwatch();

        DuplicateRules dRules = DuplicateRules.KeepFirst;
        enum DuplicateRules { KeepFirst, KeepLast, NoDuplicates }

        public MainForm()
        {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            flavorLevelComboBox.Items.AddRange(Enum.GetNames(typeof(CompressionFlavor)));
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
            listViewFiles.Enabled = true;
            uniqueFiles.Clear();
            uniqueItens.Clear();
            dRules = DuplicateRules.KeepFirst;
            rulesStripButton.Enabled = false;
            SetMode(ArchiverMode.Empty);
            firstStatusLabel.Text = "No files or directories opened";
            secondStatusLabel.Text = string.Empty;
            progressBar.Value = 0;
            TaskbarProgress.SetState(this.Handle, TaskbarProgress.ProgressState.None);
        }

        private void PopulateList(List<ListViewItem> itemList)
        {
            string newGroupName = string.Empty;
            listViewFiles.BeginUpdate();
            foreach (var item in itemList)
            {
                if(uniqueFiles.Add(item.Group.Name))
                {
                    newGroupName = item.Group.Name;
                    listViewFiles.Groups.Add(item.Group);
                }

                if(item.Group.Name == newGroupName)
                {
                    if (uniqueItens.ContainsKey(item.SubItems[1].Text))
                    {
                        uniqueItens[item.SubItems[1].Text].Add(item);
                        rulesStripButton.Enabled = true;
                        item.SubItems[1].Font = new System.Drawing.Font(item.Font, System.Drawing.FontStyle.Bold);
                        uniqueItens[item.SubItems[1].Text][0].SubItems[1].Font = item.SubItems[1].Font;
                    }
                    else
                    {
                    uniqueItens.Add(item.SubItems[1].Text, new List<ListViewItem>() { item });
                    }

                    listViewFiles.Items.Add(item);
                }
            }
            SetRule(dRules);
            listViewFiles.EndUpdate();
        }
        
        internal void CalculateTotalListSizes()
        {
            long totalSize = 0;
            long totalCompressedSize = 0;
            for (int i = 0; i < listViewFiles.Items.Count; i++)
            {
                totalSize += Convert.ToInt64(listViewFiles.Items[i].SubItems[3].Tag);
                if (listViewFiles.Items[i].SubItems.Count > 4)
                    totalCompressedSize += Convert.ToInt32(listViewFiles.Items[i].SubItems[4].Tag);
            }

            firstStatusLabel.Text = "Total: " + listViewFiles.Items.Count.ToString() + " file(s), " + SizeSuffix(totalSize);
            if (totalCompressedSize > 0)
                firstStatusLabel.Text += " > " + SizeSuffix(totalCompressedSize)
                            + " (" + ((float)totalCompressedSize / totalSize).ToString("P1", CultureInfo.InvariantCulture) + ") inside "
                            + listViewFiles.Groups.Count.ToString() + " opened archive(s).";

            else
                firstStatusLabel.Text += " (uncompressed) inside " + listViewFiles.Groups.Count.ToString() + " opened directory(ies).";

                secondStatusLabel.Text = "Duplicated names found: " + (listViewFiles.Items.Count - uniqueItens.Count).ToString();
        }

        internal async void openFilesStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenHpi.ShowDialog() == DialogResult.OK)
            {
                SetMode(ArchiverMode.Busy);
                progressBar.Style = ProgressBarStyle.Marquee;
                firstStatusLabel.Text = "Loading file list from selected HPI file(s)...";

                string[] extensionOrder = { ".GP3", ".CCX", ".UFO", ".HPI" }; //File load priority order
                var orderedList = dialogOpenHpi.FileNames.OrderBy(x => {
                    var index = Array.IndexOf(extensionOrder, Path.GetExtension(x).ToUpper());
                    return index < 0 ? int.MaxValue : index; }).ToList();

                foreach (var file in orderedList)
                {
                    var filesInfo = await Task.Run(() => GetListViewGroupItens(file));
                    PopulateList(filesInfo);
                }
                CalculateTotalListSizes();
                progressBar.Style = ProgressBarStyle.Continuous;
                SetMode(ArchiverMode.File);
            }
        }

        private async void directoryToCompressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenFolder.ShowDialog() == DialogResult.OK)
            {
                SetMode(ArchiverMode.Busy);
                progressBar.Style = ProgressBarStyle.Marquee;
                firstStatusLabel.Text = "Loading file list from selected directory...";
                var dirInfo = await Task.Run(() => GetListViewGroupItens(dialogOpenFolder.SelectedPath));
                PopulateList(dirInfo);
                CalculateTotalListSizes();
                progressBar.Style = ProgressBarStyle.Continuous;
                SetMode(ArchiverMode.Dir);
            }
        }

        public List<ListViewItem> GetListViewGroupItens(string fullPath)
        {
            var listColection = new List<ListViewItem>();
            //Check if path is a directory or file
            FileAttributes fileAtt = File.GetAttributes(fullPath);
            if (fileAtt.HasFlag(FileAttributes.Directory)) //Directory
            {
                var fileList = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
                var group = new ListViewGroup(fullPath, "Directory " + fullPath);
                foreach (var file in fileList)
                {
                    var finfo = new FileInfo(file);
                    if (finfo.Length > Int32.MaxValue)
                        throw new Exception(finfo.Name + " is too large. File maximum size is 2GB (2 147 483 647 bytes).");

                    ListViewItem lvItem = new ListViewItem( new string[] { 
                        String.Empty,
                        file.Substring(fullPath.Length + 1),
                        Path.GetExtension(file).ToUpper(),
                        finfo.Length.ToString("N0"),
                        }, group);
                    lvItem.SubItems[1].Tag = fullPath;
                    lvItem.SubItems[3].Tag = (int)finfo.Length;
                    lvItem.Tag = fullPath;
                    lvItem.UseItemStyleForSubItems = false;
                    listColection.Add(lvItem);
                }
            }
            else //HPI File or Files
            {
                var group = new ListViewGroup(fullPath, Path.GetExtension(fullPath).ToUpper() + " File - " + fullPath);
                using (HpiArchive hpia = HpiFile.Open(fullPath))
                    foreach (var entry in hpia.Entries)
                    {
                        ListViewItem lvItem = new ListViewItem(new string[] {
                            String.Empty,
                            entry.Key,
                            Path.GetExtension(entry.Key).ToUpper(),
                            entry.Value.UncompressedSize.ToString("N0"),
                            entry.Value.CompressedSizeCount().ToString("N0"),
                            entry.Value.Ratio().ToString("P1")
                        }, group);
                        lvItem.SubItems[1].Tag = fullPath;
                        lvItem.SubItems[3].Tag = entry.Value.UncompressedSize;
                        lvItem.SubItems[4].Tag = entry.Value.CompressedSizeCount();
                        lvItem.SubItems[5].Tag = entry.Value.Ratio();
                        lvItem.Tag = fullPath;
                        lvItem.UseItemStyleForSubItems = false;
                        listColection.Add(lvItem);
                    }
            }
            return listColection;
        }

        private async void toolStripExtractButton_Click(object sender, EventArgs e)
        {
            if (listViewFiles.CheckedItems.Count == 0)
                MessageBox.Show("Can't Extract: No files have been checked in the list.");

            else if (listViewFiles.CheckedItems.Count > 0)
            {
                if (dialogExtractToFolder.ShowDialog() == DialogResult.OK)
                {
                    SetMode(ArchiverMode.Busy);
                    firstStatusLabel.Text = "Extracting " + listViewFiles.CheckedItems.Count.ToString() + " files...";

                    var fileList = GetCheckedFileNames();
                    progressBar.Maximum = fileList.Values.Sum(x => x.Count);

                    var progress = new Progress<string>(percent =>
                    {
                        progressBar.PerformStep();
                        TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                    });

                    timer.Restart();

                    await Task.Run(() => HpiFile.DoExtraction(fileList, dialogExtractToFolder.SelectedPath, progress));

                    timer.Stop();

                    firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}m {1}s {2}ms", timer.Elapsed.Minutes,
                    timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                    progressBar.Value = progressBar.Maximum;
                    secondStatusLabel.Text = dialogExtractToFolder.SelectedPath;
                    TaskbarProgress.FlashWindow(this.Handle, true);
                    SetMode(ArchiverMode.Finish);
                }
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
                    fileList.Add(item.Group.Name, new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { item.SubItems[1].Text });
            }

            return fileList;
        }

        public string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB" };
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value == 0) { return "0 bytes"; }
            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }
            return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
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
                int chunkTotal = 0;
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                    chunkTotal += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);

                progressBar.Maximum = chunkTotal;

                CompressionFlavor flavor;
                Enum.TryParse(flavorLevelComboBox.Text, out flavor);

                var fileList = GetCheckedFileNames();
                string target = fileList.First().Key;

                var progress = new Progress<string>(last =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.PerformStep();
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                timer.Restart();

                await Task.Run(() => HpiFile.CreateFromFileList(fileList[target].ToArray(), target, dialogSaveHpi.FileName, progress, flavor));

                timer.Stop();

                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}h {1}m {2}s {3}ms", timer.Elapsed.Hours, timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                progressBar.Value = progressBar.Maximum;
                secondStatusLabel.Text = dialogSaveHpi.FileName;
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
                    if (e.Column == 1 || e.Column == 2) listViewFiles.ListViewItemSorter = new ListViewItemStringComparerAsc(e.Column);
                    if (e.Column == 3 || e.Column == 4) listViewFiles.ListViewItemSorter = new ListViewItemIntComparerAsc(e.Column);
                    if (e.Column == 5) listViewFiles.ListViewItemSorter = new ListViewItemFloatComparerAsc(e.Column);
                    listViewFiles.Tag = "A";
                }
                else
                {
                    if (e.Column == 0) listViewFiles.ListViewItemSorter = new ListViewItemCheckComparerDesc(e.Column);
                    if (e.Column == 1 || e.Column == 2) listViewFiles.ListViewItemSorter = new ListViewItemStringComparerDesc(e.Column);
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

                var fileList = GetCheckedFileNames();

                CompressionFlavor flavor;
                Enum.TryParse(flavorLevelComboBox.Text, out flavor);

                //Calculate total size and number of chunks
                int chunkTotal = 0;
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                    chunkTotal += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);

                progressBar.Maximum = chunkTotal;

                var progress = new Progress<string>(last =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.PerformStep();
                    TaskbarProgress.SetValue(this.Handle, progressBar.Value, progressBar.Maximum);
                });

                timer.Restart();

                await Task.Run(() => HpiFile.Merge(fileList, dialogSaveHpi.FileName, flavor, progress));

                timer.Stop();
                
                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}h {1}m {2}s {3}ms", timer.Elapsed.Hours, timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                secondStatusLabel.Text = dialogSaveHpi.FileName;
                TaskbarProgress.FlashWindow(this.Handle, true);
                SetMode(ArchiverMode.Finish);
            }
        }

        private void SetRule(DuplicateRules rule)
        {
            dRules = rule;
            keepFirstToolStripMenuItem.Checked = rule == DuplicateRules.KeepFirst;
            keepLastToolStripMenuItem.Checked = rule == DuplicateRules.KeepLast;
            uncheckAllDuplicatesToolStripMenuItem.Checked = rule == DuplicateRules.NoDuplicates;

            listViewFiles.BeginUpdate();

            switch (rule)
            {
                case DuplicateRules.KeepFirst:
                    foreach (var item in uniqueItens)
                    {
                        item.Value.First().Checked = true;
                        for (int i = 1; i < item.Value.Count; i++)
                            item.Value[i].Checked = false;
                    }
                    break;
                case DuplicateRules.KeepLast:
                    foreach (var item in uniqueItens)
                    {
                        item.Value.Last().Checked = true;
                        for (int i = 0; i < item.Value.Count - 1; i++)
                            item.Value[i].Checked = false;
                    }
                    break;
                case DuplicateRules.NoDuplicates:
                    foreach (var item in uniqueItens)
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
    }
}



