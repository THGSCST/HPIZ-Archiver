using System;
using System.Windows.Forms;

namespace HPIZArchiver
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (Environment.GetCommandLineArgs().Length > 1)
                return;
            /* Command: hpiz -help
             * Usage: hpiz [options] <archive_name> [destination_folder]
             * 
             * Options:
             * -e  Extract all files from the specified archive. Do not overwrites existing file.
             *     Usage: hpiz -e <archive_name> [destination_folder]
             *
             * -x  Extract all files from the specified archive. Always overwrites existing files without prompting!
             *     Usage: hpiz -x <archive_name> [destination_folder]
             *     
             * -c  Create a new archive from a specified source folder.
             *     Usage: hpiz -c <new_archive_name> <source_folder>
             *     
             * -r  Repack an existing archive. Optionally, specify a new name for the repacked archive.
             *     Usage: hpiz -r <archive_name> [new_archive_name]
             */

            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }

    }
}
