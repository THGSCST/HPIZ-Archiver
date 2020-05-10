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
        public MainForm()
        {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Disable, Not Implemented yet
            compressionLevelComboBox.SelectedIndex = 1;
            compressionLevelComboBox.Enabled = false;
        }


        private async void toolStripCompressButton_Click(object sender, EventArgs e)
        {
            if (dialogSaveHpi.ShowDialog() == DialogResult.OK)
            {
                toolStrip.Enabled = false;
                listViewFiles.Enabled = false;

                firstStatusLabel.Text = "Compressing... Last processed:";

                //Calculate chunk total
                var fileList = new SortedSet<string>();
                int chunkTotal = 0;
                foreach (ListViewItem item in listViewFiles.CheckedItems)
                {
                    fileList.Add(item.Text);
                    int size = Int32.Parse(item.SubItems[1].Text, NumberStyles.AllowThousands);
                    chunkTotal += (size / 65536) + (size % 65536 == 0 ? 0 : 1);
                }
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

                await Task.Run(() => HpiFile.CreateFromFileList( fileList, toolStripPathTextBox.Text, dialogSaveHpi.FileName, progress));

                timer.Stop();

                firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}m {1}s {2}ms", timer.Elapsed.Minutes,
                timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                secondStatusLabel.Text = dialogSaveHpi.FileName;
                toolStrip.Enabled = true;
            }
        }
        

        private void PopulateList(List<ListViewItem> collection)
        {
            listViewFiles.Items.Clear();
            listViewFiles.BeginUpdate();
            listViewFiles.Items.AddRange(collection.ToArray());
            listViewFiles.EndUpdate();
            listViewFiles.Enabled = true;
        }

        private void hPIFileToExtractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenHpi.ShowDialog() == DialogResult.OK)
            {
                toolStripPathLabel.Text = "File:";
                firstStatusLabel.Text = "Loading file list from selected HPI file...";
                toolStrip.Enabled = false;
                toolStripCompressButton.Enabled = false;
                toolStripPathTextBox.Text = dialogOpenHpi.FileName;
                progressBar.Style = ProgressBarStyle.Marquee;
                progressBar.Visible = true;

                OpenHPIandLoad(dialogOpenHpi.FileName);
            }

        }
        public async void OpenHPIandLoad(string fullPath)
        {
            
            var collection = await Task.Run(() =>
            {
                //System.Threading.Thread.Sleep(10000);
                using (HpiArchive hpia = new HpiArchive( File.Open(fullPath, FileMode.Open)))
                {
                    long totalSize = 0;
                    long totalCompressedSize = 0;
                    var listColection = new List<ListViewItem>(hpia.Entries.Count);


                    foreach (var item in hpia.Entries)
                    {
                        ListViewItem lvi = new ListViewItem(item.Key);
                        lvi.SubItems.Add(item.Value.UncompressedSize.ToString("N0"));
                        totalSize += item.Value.UncompressedSize;
                        lvi.SubItems.Add(item.Value.CompressedSizeCount().ToString("N0"));
                        totalCompressedSize += item.Value.CompressedSizeCount();
                        lvi.SubItems.Add(item.Value.Ratio().ToString("P1"));
                        lvi.Checked = true;
                        listColection.Add(lvi);
                    }


                    string totalsText =
                        hpia.Entries.Count.ToString() + " file(s), " + SizeSuffix(totalSize)
                        + " > " + SizeSuffix(totalCompressedSize)
                        + " (" + ((float)totalCompressedSize / totalSize).ToString("P1", CultureInfo.InvariantCulture) + ")";
                    return new Tuple<List<ListViewItem>, string>(listColection, totalsText);
                }
            });

            PopulateList(collection.Item1);
            firstStatusLabel.Text = collection.Item2;
            progressBar.Visible = false;
            toolStripExtractButton.Enabled = true;
            toolStrip.Enabled = true;
        }

        private async void directoryToCompressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dialogOpenFolder.ShowDialog() == DialogResult.OK)
            {
                toolStripPathLabel.Text = "Dir:";
                firstStatusLabel.Text = "Loading file list from selected directory...";
                toolStripExtractButton.Enabled = false;
                toolStrip.Enabled = false;
                toolStripPathTextBox.Text = dialogOpenFolder.SelectedPath;
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Marquee;

                var dirInfo = await Task.Run(() => OpenDirectoryandLoadInfo(dialogOpenFolder.SelectedPath));

                PopulateList(dirInfo.Item1);
                toolStripCompressButton.Enabled = true;
                firstStatusLabel.Text = dirInfo.Item2;
                progressBar.Visible = false;
                toolStrip.Enabled = true;
            }
        }

        public async Task<(List<ListViewItem>, string)> OpenDirectoryandLoadInfo(string fullPath)
        {
                long totalSize = 0;
                var fileList = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);

                var listColection = new List<ListViewItem>(fileList.Length);
                foreach (var file in fileList)
                {
                    ListViewItem lvi = new ListViewItem(file.Substring(fullPath.Length + 1) );
                    var finfo = new FileInfo(file);
                    totalSize += finfo.Length;
                    lvi.SubItems.Add(finfo.Length.ToString("N0"));
                    listColection.Add(lvi);
                    lvi.Checked = true;
                }

                string totalsText =
                    fileList.Length.ToString() + " file(s), " + SizeSuffix(totalSize) + " (uncompressed)";

            return (listColection, totalsText);
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

                    var fileList = GetCheckedFileNames();
                    progressBar.Visible = true;
                    progressBar.Value = 0;
                    progressBar.Maximum = fileList.Count;
                    progressBar.Style = ProgressBarStyle.Continuous;

                    var progress = new Progress<int>(percent =>
                    {
                        progressBar.Value = percent;
                    });

                    var timer = new Stopwatch();
                    timer.Start();

                    await Task.Run(() => DoExtraction(progress, fileList, dialogExtractToFolder.SelectedPath, toolStripPathTextBox.Text));
                    
                    timer.Stop();

                    firstStatusLabel.Text = String.Format("Done! Elapsed time: {0}m {1}s {2}ms", timer.Elapsed.Minutes,
                    timer.Elapsed.Seconds, timer.Elapsed.Milliseconds);
                    progressBar.Value = progressBar.Maximum;
                    secondStatusLabel.Text = dialogExtractToFolder.SelectedPath;
                    toolStrip.Enabled = true;

                }

            } }

        public void DoExtraction(IProgress<int> progress, SortedSet<string> fileList, string path, string archivepath)
        {
            using (var hpiaaa = new HpiArchive(File.OpenRead(archivepath)))
            {
                var fList = fileList.ToArray();
                int minProgress = (fileList.Count - 1) / 100 + 1;
                for (int i = 0; i != fileList.Count; ++i)
                {
                    var hfile = hpiaaa.Entries[fList[i]];
                    string fullName = path + "\\" + fList[i];
                    Directory.CreateDirectory(Path.GetDirectoryName(fullName));
                    using (FileStream diskFile = File.Create(fullName, hfile.UncompressedSize + 1)) //+1 because file size can be zero
                    {
                        var buffer = hpiaaa.Extract(hfile);
                        diskFile.Write(buffer, 0, buffer.Length);
                    }
                    
                    if (progress != null && i % minProgress == 0)
                        progress.Report(i);
                }
            }
        }

        public SortedSet<string> GetCheckedFileNames()
        {
            var fileList = new SortedSet<string>();
            foreach (ListViewItem item in listViewFiles.CheckedItems)
            {
                fileList.Add(item.Text);
            }
            return fileList;
        }


        public string SizeSuffix(Int64 value, int decimalPlaces = 1)
            {
                string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
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

    }

    } 