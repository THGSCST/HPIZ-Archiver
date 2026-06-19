using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HPIZArchiver
{
    internal static class ModernDialogs
    {
        private const int CancelledHResult = unchecked((int)0x800704C7);
        private static string _lastOpenDirectory;
        private static string _lastSaveDirectory;
        private static string _lastFolder;

        public static string[] OpenFiles(IWin32Window owner, string title, string filter)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.AutoUpgradeEnabled = true;
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.Filter = filter;
                dialog.Multiselect = true;
                dialog.RestoreDirectory = true;
                dialog.Title = title;
                SetInitialDirectory(dialog, _lastOpenDirectory);

                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return Array.Empty<string>();

                _lastOpenDirectory = Path.GetDirectoryName(dialog.FileNames[0]);
                return dialog.FileNames;
            }
        }

        public static string SaveFile(
            IWin32Window owner,
            string title,
            string filter,
            string defaultExtension)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.AddExtension = true;
                dialog.AutoUpgradeEnabled = true;
                dialog.CheckPathExists = true;
                dialog.DefaultExt = defaultExtension;
                dialog.Filter = filter;
                dialog.OverwritePrompt = true;
                dialog.RestoreDirectory = true;
                dialog.SupportMultiDottedExtensions = true;
                dialog.Title = title;
                SetInitialDirectory(dialog, _lastSaveDirectory);

                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return null;

                _lastSaveDirectory = Path.GetDirectoryName(dialog.FileName);
                return dialog.FileName;
            }
        }

        public static string SelectFolder(IWin32Window owner, string title)
        {
            IFileDialog dialog = null;
            IShellItem initialFolder = null;
            IShellItem result = null;

            try
            {
                dialog = (IFileDialog)new FileOpenDialogCom();
                dialog.SetOptions(
                    FileOpenOptions.PickFolders |
                    FileOpenOptions.ForceFileSystem |
                    FileOpenOptions.PathMustExist |
                    FileOpenOptions.NoChangeDirectory);
                dialog.SetTitle(title);

                string initialPath = ExistingDirectoryOrNull(_lastFolder);
                if (initialPath != null)
                {
                    Guid shellItemId = typeof(IShellItem).GUID;
                    int createResult = SHCreateItemFromParsingName(
                        initialPath,
                        IntPtr.Zero,
                        ref shellItemId,
                        out initialFolder);
                    if (createResult >= 0)
                        dialog.SetFolder(initialFolder);
                }

                int showResult = dialog.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (showResult == CancelledHResult)
                    return null;
                if (showResult < 0)
                    Marshal.ThrowExceptionForHR(showResult);

                dialog.GetResult(out result);
                string selectedPath = GetFileSystemPath(result);
                _lastFolder = selectedPath;
                return selectedPath;
            }
            finally
            {
                ReleaseComObject(result);
                ReleaseComObject(initialFolder);
                ReleaseComObject(dialog);
            }
        }

        private static void SetInitialDirectory(FileDialog dialog, string directory)
        {
            string existingDirectory = ExistingDirectoryOrNull(directory);
            if (existingDirectory != null)
                dialog.InitialDirectory = existingDirectory;
        }

        private static string ExistingDirectoryOrNull(string directory)
        {
            return !string.IsNullOrEmpty(directory) && Directory.Exists(directory)
                ? directory
                : null;
        }

        private static string GetFileSystemPath(IShellItem item)
        {
            IntPtr pathPointer;
            item.GetDisplayName(ShellDisplayName.FileSystemPath, out pathPointer);
            try
            {
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bindingContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [Flags]
        private enum FileOpenOptions : uint
        {
            PickFolders = 0x00000020,
            ForceFileSystem = 0x00000040,
            NoChangeDirectory = 0x00000008,
            PathMustExist = 0x00000800
        }

        private enum ShellDisplayName : uint
        {
            FileSystemPath = 0x80058000
        }

        private enum FileDialogAddPlacement
        {
            Bottom = 0,
            Top = 1
        }

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        [ClassInterface(ClassInterfaceType.None)]
        private class FileOpenDialogCom
        {
        }

        [ComImport]
        [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);

            void SetFileTypes(uint count, IntPtr filterSpecifications);
            void SetFileTypeIndex(uint fileTypeIndex);
            void GetFileTypeIndex(out uint fileTypeIndex);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(FileOpenOptions options);
            void GetOptions(out FileOpenOptions options);
            void SetDefaultFolder(IShellItem shellItem);
            void SetFolder(IShellItem shellItem);
            void GetFolder(out IShellItem shellItem);
            void GetCurrentSelection(out IShellItem shellItem);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem shellItem);
            void AddPlace(IShellItem shellItem, FileDialogAddPlacement placement);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid clientGuid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(
                IntPtr bindingContext,
                ref Guid handlerId,
                ref Guid interfaceId,
                out IntPtr result);

            void GetParent(out IShellItem parent);
            void GetDisplayName(ShellDisplayName displayName, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
