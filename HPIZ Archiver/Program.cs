using HPIZ;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace HPIZArchiver
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            OperationTuning.Initialize();

            if (args.Length > 0)
                return RunCommandLine(args);

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
            return 0;
        }

        private static int RunCommandLine(string[] args)
        {
            try
            {
                if (args.Length < 2
                    || args.Length > 3
                    || !string.Equals(args[0], "-r", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Usage: HPIZ Archiver.exe -r <archive> [destination]");

                string source = Path.GetFullPath(args[1]);
                string destination = args.Length >= 3
                    ? Path.GetFullPath(args[2])
                    : Path.Combine(
                        Path.GetDirectoryName(source),
                        Path.GetFileNameWithoutExtension(source) + ".repacked" + Path.GetExtension(source));

                using (var archive = HpiFile.Open(source))
                {
                    var sources = new FilePathCollection();

                    foreach (var entry in archive.Entries)
                        sources.Add(entry.Key, source);

                    var cache = new Dictionary<string, HpiArchive>(StringComparer.OrdinalIgnoreCase)
                    {
                        { source, archive }
                    };
                    var duplicates = HpiFile.FindDuplicateContent(sources, cache);
                    ArchiveCreationResult result = HpiFile.CreateFromManySources(
                        sources,
                        destination,
                        CompressionMethod.ZopfliDeflate,
                        null,
                        cache,
                        duplicates);

                    return result.Errors.Count == 0 ? 0 : 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
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
