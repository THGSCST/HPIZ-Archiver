using HPIZ;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HPIZArchiver
{
    public partial class MainForm : Form
    {
        SortedSet<string> uniqueFiles = new SortedSet<string>();
        SortedSet<string> uniqueItens = new SortedSet<string>();
        List<string> duplicatedItens = new List<string>();
        public MainForm()
        {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            flavorLevelComboBox.ComboBox.DataSource = Enum.GetValues(typeof(CompressionFlavor));
            flavorLevelComboBox.ComboBox.BindingContext = this.BindingContext;
            flavorLevelComboBox.SelectedIndex = 4;
        }

        private void PopulateList(List<ListViewItem> collection)
        {
            string newGroupName = string.Empty;
            listViewFiles.BeginUpdate();
            foreach (var item in collection)
            {
                if(uniqueFiles.Add(item.Group.Name))
                {
                    newGroupName = item.Group.Name;
                    listViewFiles.Groups.Add(item.Group);
                }

                if(item.Group.Name == newGroupName)
                {
                    if (uniqueItens.Add(item.SubItems[1].Text))
                            item.Checked = true;
                    else
                        {
                            rulesStripButton.Enabled = true;
                            duplicatedItens.Add(item.SubItems[1].Text);
                        }

                    listViewFiles.Items.Add(item);
                }
            }
                listViewFiles.EndUpdate();
                listViewFiles.Enabled = true;
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

            if (duplicatedItens.Count > 0)
                secondStatusLabel.Text = "Duplicated names found: " + duplicatedItens.Count.ToString();
        }

        internal async void hPIFileToExtractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenHpi.ShowDialog() == DialogResult.OK)
            {
                SimpleLoading("Loading file list from selected HPI file(s)...");

                string[] order = { ".GP3", ".CCX", ".UFO", ".HPI" }; //File load priority order
                var orderedList = dialogOpenHpi.FileNames.OrderBy(x => {
                    var index = Array.IndexOf(order, Path.GetExtension(x).ToUpper());
                    return index < 0 ? int.MaxValue : index; }).ToList();

                foreach (var file in orderedList)
                {
                    var filesInfo = await Task.Run(() => GetListViewGroupItens(file));
                    PopulateList(filesInfo);
                }
                SimpleLoading("end");
                CalculateTotalListSizes();
                ChangeArchiverMode(true, false);
            }
        }
        private void SimpleLoading(string message)
        {
            bool finish = message.Equals("end");
            if (finish == false)
                firstStatusLabel.Text = message;
            toolStrip.Enabled = finish;
            progressBar.Visible = !finish;
            progressBar.Style = ProgressBarStyle.Marquee;
        }
        private async void directoryToCompressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenFolder.ShowDialog() == DialogResult.OK)
            {
                SimpleLoading("Loading file list from selected directory...");
                var dirInfo = await Task.Run(() => GetListViewGroupItens(dialogOpenFolder.SelectedPath));
                PopulateList(dirInfo);
                SimpleLoading("end");
                CalculateTotalListSizes();
                ChangeArchiverMode(false, true);
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
                var group = new ListViewGroup(fullPath, "Dir: " + fullPath);
                foreach (var file in fileList)
                {
                    var finfo = new FileInfo(file);
                    if (finfo.Length > Int32.MaxValue)
                        throw new Exception(finfo.Name + " is too large. File maximum size is 2GB (2 147 483 647 bytes).");

                    ListViewItem lvItem = new ListViewItem(group);
                    lvItem.Tag = fullPath;
                    var subItems = new ListViewItem.ListViewSubItem[3];
                    for (int i = 0; i < subItems.Length; i++)
                        subItems[i] = new ListViewItem.ListViewSubItem();
                    subItems[0].Text = file.Substring(fullPath.Length + 1);
                    subItems[0].Tag = fullPath;
                    subItems[1].Text = Path.GetExtension(file).ToUpper();
                    subItems[2].Text = finfo.Length.ToString("N0");
                    subItems[2].Tag = (int)finfo.Length;
                    lvItem.SubItems.AddRange(subItems);
                    listColection.Add(lvItem);
                }
            }
            else //HPI File or Files
            {

                var group = new ListViewGroup(fullPath, Path.GetExtension(fullPath).ToUpper() + " File: " + fullPath);
                using (HpiArchive hpia = HpiFile.Open(fullPath))
                    foreach (var entry in hpia.Entries)
                    {
                        ListViewItem lvItem = new ListViewItem(group);
                        lvItem.Tag = fullPath;
                        var subItems = new ListViewItem.ListViewSubItem[5];
                        for (int i = 0; i < subItems.Length; i++)
                            subItems[i] = new ListViewItem.ListViewSubItem();
                        subItems[0].Text = entry.Key;
                        subItems[0].Tag = fullPath;
                        subItems[1].Text = Path.GetExtension(entry.Key).ToUpper();
                        subItems[2].Text = entry.Value.UncompressedSize.ToString("N0");
                        subItems[2].Tag = entry.Value.UncompressedSize;
                        subItems[3].Text = entry.Value.CompressedSizeCount().ToString("N0");
                        subItems[3].Tag = entry.Value.CompressedSizeCount();
                        subItems[4].Text = entry.Value.Ratio().ToString("P1");
                        subItems[4].Tag = entry.Value.Ratio();
                        lvItem.SubItems.AddRange(subItems);
                        listColection.Add(lvItem);
                    }
            }

            return listColection;
        }


        private async void toolStripExtractButton_Click(object sender, EventArgs e)
        {


            if (listViewFiles.CheckedItems.Count > 0)
            {
                //dialogExtractToFolder.SelectedPath = Path.GetDirectoryName(toolStripPathTextBox.Text);
                if (dialogExtractToFolder.ShowDialog() == DialogResult.OK)
                {
                    toolStrip.Enabled = false;
                    listViewFiles.Enabled = false;
                    toolStripExtractButton.Enabled = false;

                    firstStatusLabel.Text = "Extracting " + listViewFiles.CheckedItems.Count.ToString() + " files...";

                    string target = (string)listViewFiles.CheckedItems[0].Tag;

                    var fileList = GetCheckedFileNames();
                    progressBar.Visible = true;
                    progressBar.Value = 0;
                    progressBar.Maximum = fileList.Values.Sum(x => x.Count) + 1;
                    progressBar.Style = ProgressBarStyle.Continuous;

                    var progress = new Progress<string>(percent =>
                    {
                        progressBar.Value++;
                    });

                    var timer = new Stopwatch();
                    timer.Start();

                    await Task.Run(() => HpiFile.DoExtraction(fileList, dialogExtractToFolder.SelectedPath, progress));

                    timer.Stop();

                    firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}m {1}s {2}ms", timer.Elapsed.Minutes,
                    timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                    progressBar.Value = progressBar.Maximum;
                    secondStatusLabel.Text = dialogExtractToFolder.SelectedPath;
                    toolStrip.Enabled = true;
                    listViewFiles.Enabled = true;
                    ChangeArchiverMode(true, true);
                }
            }
        }



        public PathCollection GetCheckedFileNames()
        {
            PathCollection fileList = new PathCollection();
            for (int i = 0; i < listViewFiles.Groups.Count; i++)
            {
                fileList.Add(listViewFiles.Groups[i].Name, new SortedSet<string>());

                for (int j = 0; j < listViewFiles.Groups[i].Items.Count; j++)
                    if (listViewFiles.Groups[i].Items[j].Checked)
                        fileList[listViewFiles.Groups[i].Name].Add(listViewFiles.Groups[i].Items[j].SubItems[1].Text);
            }
        return fileList;
        }


        public string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB" };
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }

        private async void compressCheckedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogSaveHpi.ShowDialog() == DialogResult.OK)
            {
                toolStrip.Enabled = false;
                listViewFiles.Enabled = false;

                firstStatusLabel.Text = "Compressing... Last processed:";

                //Calculate total size and number of chunks
                int chunkTotal = 0;
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                {
                    chunkTotal += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);
                }

                CompressionFlavor flavor;
                Enum.TryParse(flavorLevelComboBox.Text, out flavor);

                var fileList = GetCheckedFileNames();
                string target = fileList.First().Key;

                progressBar.Maximum = chunkTotal + 1;
                progressBar.Value = 0;
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Continuous;

                var progress = new Progress<string>(last =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.Value++;
                });

                var timer = new Stopwatch();
                timer.Start();

                await Task.Run(() => HpiFile.CreateFromFileList(fileList[target].ToArray(), target, dialogSaveHpi.FileName, progress, flavor));

                timer.Stop();
                progressBar.Value = progressBar.Maximum;

                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}h {1}m {2}s {3}ms", timer.Elapsed.Hours, timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                secondStatusLabel.Text = dialogSaveHpi.FileName;
                toolStrip.Enabled = true;
                ChangeArchiverMode(true, true);
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
                if (e.Column == 0)
                {
                    if (listViewFiles.Tag.ToString() != "C")
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemCheckComparerAsc(e.Column);
                        listViewFiles.Tag = "C";
                    }
                    else
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemCheckComparerDesc(e.Column);
                        listViewFiles.Tag = "U";
                    }
                }

                if (e.Column == 1 || e.Column == 2)
                {
                    if (listViewFiles.Tag.ToString() != "A")
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemStringComparerAsc(e.Column);
                        listViewFiles.Tag = "A";
                    }
                    else
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemStringComparerDesc(e.Column);
                        listViewFiles.Tag = "Z";
                    }
                }
                if (e.Column == 3 || e.Column == 4)
                {
                    if (listViewFiles.Tag.ToString() != "0")
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemIntComparerAsc(e.Column);
                        listViewFiles.Tag = "0";
                    }
                    else
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemIntComparerDesc(e.Column);
                        listViewFiles.Tag = "9";
                    }
                }
                if (e.Column == 5)
                {
                    if (listViewFiles.Tag.ToString() != ".0")
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemFloatComparerAsc(e.Column);
                        listViewFiles.Tag = ".0";
                    }
                    else
                    {
                        listViewFiles.ListViewItemSorter = new ListViewItemFloatComparerDesc(e.Column);
                        listViewFiles.Tag = ".9";
                    }
                }
            }
        }

        private void ChangeArchiverMode(bool fileMode, bool dirMode)
        {
            openFilesToolStripMenuItem.Enabled = !dirMode;
            openDirToolStripMenuItem.Enabled = !fileMode & !dirMode;
            toolStripCompressButton.Enabled = fileMode ^ dirMode;
            toolStripExtractButton.Enabled = fileMode & !dirMode;
            flavorLevelComboBox.Enabled = fileMode ^ dirMode;
            flavorStripLabel.Enabled = fileMode ^ dirMode;
            compressCheckedFilesToolStripMenuItem.Enabled = dirMode & !fileMode;
            mergeRepackCheckedFilesToolStripMenuItem.Enabled = fileMode & ! dirMode; 
            closeAllToolStripMenuItem.Enabled = fileMode | dirMode;
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listViewFiles.Items.Clear();
            listViewFiles.Groups.Clear();
            listViewFiles.Enabled = true;
            uniqueFiles.Clear();
            uniqueItens.Clear();
            duplicatedItens.Clear();
            ChangeArchiverMode(false, false);
            firstStatusLabel.Text = "No files or directories opened";
            secondStatusLabel.Text = string.Empty;
            progressBar.Visible = false;
        }

        private async void mergeRepackCheckedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogSaveHpi.ShowDialog() == DialogResult.OK)
            {
                toolStrip.Enabled = false;
                listViewFiles.Enabled = false;

                firstStatusLabel.Text = "Compressing... Last processed:";

                //Calculate total size and number of chunks
                int chunkTotal = 0;
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                {
                    chunkTotal += FileEntry.CalculateChunkQuantity((int)item.SubItems[3].Tag);
                }

                CompressionFlavor flavor;
                Enum.TryParse(flavorLevelComboBox.Text, out flavor);

                var fileList = GetCheckedFileNames();

                progressBar.Maximum = chunkTotal + 1;
                progressBar.Value = 0;
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Continuous;

                var progress = new Progress<string>(last =>
                {
                    secondStatusLabel.Text = last;
                    progressBar.Value++;
                });

                var timer = new Stopwatch();
                timer.Start();

                await Task.Run(() => HpiFile.Merge(fileList, dialogSaveHpi.FileName, flavor, progress));

                timer.Stop();
                progressBar.Value = progressBar.Maximum;

                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}h {1}m {2}s {3}ms", timer.Elapsed.Hours, timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                secondStatusLabel.Text = dialogSaveHpi.FileName;
                toolStrip.Enabled = true;
                ChangeArchiverMode(true, true);
            }
        }
    }
}



