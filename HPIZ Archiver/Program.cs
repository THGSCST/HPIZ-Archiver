using System;
using System.Windows.Forms;

namespace HPIZArchiver
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Set up global exception handlers
            Application.ThreadException += (sender, e) => ShowException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ShowException(e.ExceptionObject as Exception);
                Environment.Exit(1); // Optionally exit the application
            };

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }

        // Method to display exception messages
        static void ShowException(Exception ex)
        {
            string message = ex?.Message ?? "An unknown error occurred.";
            MessageBox.Show($"An error occurred: {message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}

/* Sugestions for future command line args
 * Command: hpiz -help
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
