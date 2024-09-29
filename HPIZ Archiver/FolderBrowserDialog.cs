using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace HPIZArchiver
{
    internal static class ShellIIDGuid
    {
        // IID GUID strings for relevant Shell COM interfaces.
        internal const string IModalWindow = "B4DB1657-70D7-485E-8E3E-6FCB5A5C1802";
        internal const string IFileDialog = "42F85136-DB7E-439C-85F1-E4075D135FC8";
        internal const string IFileOpenDialog = "D57C7288-D4AD-4768-BE02-9D969532D960";
        internal const string IFileDialogEvents = "973510DB-7D7F-452B-8975-74A85828D354";
        internal const string IShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        internal const string IShellItem2 = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";
        internal const string IShellFolder = "000214E6-0000-0000-C000-000000000046";
        internal const string IEnumIDList = "000214F2-0000-0000-C000-000000000046";
        internal const string IShellItemArray = "B63EA76D-1F85-456F-A19C-48159EFA858B";
    }

    internal static class ShellCLSIDGuid
    {
        // CLSID GUID strings for relevant coclasses.
        internal const string FileOpenDialog = "DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7";
        internal const string FileSaveDialog = "C0B4E2F3-BA21-4773-8DBA-335EC946EB8B";
    }

        [ComImport]
    [Guid(ShellIIDGuid.IFileOpenDialog)]
    [CoClass(typeof(FileOpenDialogRCW))]
    internal interface NativeFileOpenDialog : IFileOpenDialog
    {
    }

    [ComImport]
    [ClassInterface(ClassInterfaceType.None)]
    [TypeLibType(TypeLibTypeFlags.FCanCreate)]
    [Guid(ShellCLSIDGuid.FileOpenDialog)]
    internal class FileOpenDialogRCW
    {
    }

    internal enum Win32ErrorCodes : UInt16
    {
        /// <summary><c>ERROR_SUCCESS = 0x0000 == 0</c></summary>
        Success = 0,

        /// <summary><c>ERROR_CANCELLED = 0x000004C7 == 1223</c></summary>
        ErrorCancelled = 1223,
    }

    /// <summary>Remember that HRESULT values are actually 32-bit packed structures which *encapsulate* 16-bit Win32 error codes - and other error codes. Use the methods in <see cref="HResults"/> to correctly inspect a <see cref="HResult"/> value.</summary>
    internal enum HResult : UInt32
    {
        /// <summary>S_OK</summary>    
        Ok = 0x0000,

        //		/// <summary>S_FALSE</summary> 
        //		/// <remarks>Why on earth is <c>1</c> being used to represent <c>false</c>?!?</remarks>
        //		False = 0x0001,

        /// <summary>E_INVALIDARG</summary>
        InvalidArguments = 0x80070057,

        /// <summary>E_OUTOFMEMORY</summary>
        OutOfMemory = 0x8007000E,

        /// <summary>E_NOINTERFACE</summary>
        NoInterface = 0x80004002,

        /// <summary>E_FAIL</summary>
        Fail = 0x80004005,

        /// <summary>E_ELEMENTNOTFOUND</summary>
        ElementNotFound = 0x80070490,

        /// <summary>TYPE_E_ELEMENTNOTFOUND</summary>
        TypeElementNotFound = 0x8002802B,

        /// <summary>NO_OBJECT</summary>
        NoObject = 0x800401E5,

        /// <summary>ERROR_CANCELLED</summary>
        Canceled = 0x800704C7,

        /// <summary>The requested resource is in use</summary>
        ResourceInUse = 0x800700AA,

        /// <summary>The requested resources is read-only.</summary>
        AccessDenied = 0x80030005
    }

    internal static class HResults
    {
        public static readonly HResult Cancelled = CreateWin32(code: Win32ErrorCodes.ErrorCancelled);

        /// <summary>Creates a Win32 <see cref="HResult"/> value.</summary>
        public static HResult CreateWin32(Win32ErrorCodes code)
        {
            return Create(
                isFailure: code != Win32ErrorCodes.Success,
                isCustomer: false,
                facility: HResultFacility.Win32,
                code: (UInt16)Win32ErrorCodes.ErrorCancelled
            );
        }

        // Bytes:                             333333333    222222222    111111111    000000000
        const UInt32 _codeBitMask = 0b____0000_0000____0000_0000____1111_1111____1111_1111; // Lower 16 bits
        const UInt32 _facilityBitMask = 0b____0000_0111____1111_1111____0000_0000____0000_0000; // Next 11 bits
        const UInt32 _reserveXBitMask = 0b____0000_1000____0000_0000____0000_0000____0000_0000; // Next 1 bit
        const UInt32 _ntStatusBitMask = 0b____0001_0000____0000_0000____0000_0000____0000_0000; // Next 1 bit
        const UInt32 _customerBitMask = 0b____0010_0000____0000_0000____0000_0000____0000_0000; // Next 1 bit
        const UInt32 _reservedBitMask = 0b____0100_0000____0000_0000____0000_0000____0000_0000; // Highest bit
        const UInt32 _severityBitMask = 0b____1000_0000____0000_0000____0000_0000____0000_0000; // Highest bit

        /// <summary>Creates a non-NTSTATUS <see cref="HResult"/> value.</summary>
        public static HResult Create(
            Boolean isFailure,
            Boolean isCustomer,
            HResultFacility facility,
            UInt16 code
        )
        {
            if (facility >= HResultFacility.MAX_VALUE_EXCLUSIVE)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(facility), actualValue: facility, message: "Value must be in the range 0 through 2047 (inclusive). The HRESULT Facility code is an 11-bit number.");
            }

            UInt32 value = 0;

            if (isFailure)
            {
                value |= _severityBitMask;
            }

            if (isCustomer)
            {
                value |= _customerBitMask;
            }

            //

            {
                UInt32 facilityBits = (UInt32)facility;
                facilityBits = facilityBits << 16;

                value |= facilityBits;
            }

            //

            value |= code;

            //

            return (HResult)value;
        }

        public static HResultSeverity GetSeverity(this HResult hr)
        {
            UInt32 severityBits = (UInt32)hr & _severityBitMask;
            return (severityBits == 0) ? HResultSeverity.Success : HResultSeverity.Failure;
        }

        public static HResultCustomer GetCustomer(this HResult hr)
        {
            UInt32 customerBits = (UInt32)hr & _customerBitMask;
            return (customerBits == 0) ? HResultCustomer.MicrosoftDefined : HResultCustomer.CustomerDefined;
        }

        public static HResultFacility GetFacility(this HResult hr)
        {
            UInt32 facilityBits = (UInt32)hr & _facilityBitMask;

            UInt32 facilityBitsOnly = facilityBits >> 16;

            UInt16 facilityBitsOnlyAsU16 = (UInt16)facilityBitsOnly;

            return (HResultFacility)facilityBitsOnlyAsU16;
        }

        public static UInt16 GetCode(this HResult hr)
        {
            UInt32 codeBits = (UInt32)hr & _codeBitMask;
            return (UInt16)codeBits;
        }

        /// <summary>Indicates if the required zeros for reserved bits are indeed zero - otherwise <paramref name="hr"/> may be an <c>NTSTATUS</c> value or some other 32-bit value.</summary>
        public static Boolean IsValidHResult(this HResult hr)
        {
            UInt32 ntstatusBits = (UInt32)hr & _ntStatusBitMask;
            if (ntstatusBits != 0)
            {
                // The NTSTATUS bit is set, so this is not a HRESULT.
                return false;
            }

            // If the NTSTATUS (`N`) bit is clear, then the Reserved (`R`) bit must also be clear:
            // > Reserved. If the N bit is clear, this bit MUST be set to 0. If the N bit is set, this bit is defined by the NTSTATUS numbering space (as specified in section 2.3).

            UInt32 reservedBits = (UInt32)hr & _reservedBitMask;
            if (reservedBits != 0)
            {
                // Invalid HRESULT: `R` must be 0 if `N` is 0.
                return false;
            }

            UInt32 reservedXBits = (UInt32)hr & _reserveXBitMask;
            if (reservedXBits != 0)
            {
                // The `X` bit should always be false. (Though "should" - implying it *COULD* be non-zero... but how should this function interpret that language in the spec?)
                return false;
            }

            return true;
        }

        public static Boolean TryGetWin32ErrorCode(this HResult hr, out Win32ErrorCodes win32Code)
        {
            // Set `win32Code` anyway, just in case the HRESULT's Customer and Facility codes are wrong:
            UInt16 codeBits = GetCode(hr);
            win32Code = (Win32ErrorCodes)codeBits;

            if (IsValidHResult(hr))
            {
                // But only return true or false if the flag bits are correct:
                if (GetCustomer(hr) == HResultCustomer.MicrosoftDefined)
                {
                    if (GetFacility(hr) == HResultFacility.Win32)
                    {
                        Boolean hresultSeverityMatchesWin32Code =
                            (GetSeverity(hr) == HResultSeverity.Success && win32Code == Win32ErrorCodes.Success)
                            ||
                            (GetSeverity(hr) == HResultSeverity.Failure && win32Code != Win32ErrorCodes.Success);

                        if (hresultSeverityMatchesWin32Code)
                        {
                            return true;
                        }
                    }
                    if (0 <= hr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        internal enum HResultSeverity
        {
            Success = 0,
            Failure = 1
        }

        internal enum HResultCustomer
        {
            MicrosoftDefined = 0,
            CustomerDefined = 1
        }

        internal enum HResultFacility : UInt16
        {
            /// <summary>FACILITY_NULL - The default facility code.</summary>
            Null = 0,

            /// <summary>FACILITY_RPC - The source of the error code is an RPC subsystem.</summary>
            Rpc = 1,

            /// <summary>FACILITY_WIN32 - This region is reserved to map undecorated error codes into HRESULTs.</summary>
            Win32 = 7,

            /// <summary>FACILITY_WINDOWS - The source of the error code is the Windows subsystem.</summary>
            Windows = 8,

            MAX_VALUE_INCLUSIVE = 2047,
            MAX_VALUE_EXCLUSIVE = 2048,
        }
    }

    [ComImport]
    [Guid(ShellIIDGuid.IModalWindow)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IModalWindow
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [PreserveSig]
        HResult Show([In] IntPtr parent);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct FilterSpec
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string Name;
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string Spec;

        internal FilterSpec(string name, string spec)
        {
            this.Name = name;
            this.Spec = spec;
        }
    }

    [Flags]
    internal enum ShellFolderEnumerationOptions : ushort
    {
        CheckingForChildren = 0x0010,
        Folders = 0x0020,
        NonFolders = 0x0040,
        IncludeHidden = 0x0080,
        InitializeOnFirstNext = 0x0100,
        NetPrinterSearch = 0x0200,
        Shareable = 0x0400,
        Storage = 0x0800,
        NavigationEnum = 0x1000,
        FastItems = 0x2000,
        FlatList = 0x4000,
        EnableAsync = 0x8000
    }

    [ComImport]
    [Guid(ShellIIDGuid.IEnumIDList)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumIDList
    {
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult Next(uint celt, out IntPtr rgelt, out uint pceltFetched);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult Skip([In] uint celt);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult Reset();

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult Clone([MarshalAs(UnmanagedType.Interface)] out IEnumIDList ppenum);
    }

    [ComImport]
    [Guid(ShellIIDGuid.IShellFolder)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComConversionLoss]
    internal interface IShellFolder
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void ParseDisplayName(IntPtr hwnd, [In, MarshalAs(UnmanagedType.Interface)] IBindCtx pbc, [In, MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, [In, Out] ref uint pchEaten, [Out] IntPtr ppidl, [In, Out] ref uint pdwAttributes);
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult EnumObjects([In] IntPtr hwnd, [In] ShellFolderEnumerationOptions grfFlags, [MarshalAs(UnmanagedType.Interface)] out IEnumIDList ppenumIDList);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult BindToObject([In] IntPtr pidl, /*[In, MarshalAs(UnmanagedType.Interface)] IBindCtx*/ IntPtr pbc, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IShellFolder ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void BindToStorage([In] ref IntPtr pidl, [In, MarshalAs(UnmanagedType.Interface)] IBindCtx pbc, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void CompareIDs([In] IntPtr lParam, [In] ref IntPtr pidl1, [In] ref IntPtr pidl2);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void CreateViewObject([In] IntPtr hwndOwner, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetAttributesOf([In] uint cidl, [In] IntPtr apidl, [In, Out] ref uint rgfInOut);


        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetUIObjectOf([In] IntPtr hwndOwner, [In] uint cidl, [In] IntPtr apidl, [In] ref Guid riid, [In, Out] ref uint rgfReserved, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetDisplayNameOf([In] ref IntPtr pidl, [In] uint uFlags, out IntPtr pName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetNameOf([In] IntPtr hwnd, [In] ref IntPtr pidl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszName, [In] uint uFlags, [Out] IntPtr ppidlOut);
    }
    internal enum ShellItemDesignNameOptions
    {
        Normal = 0x00000000,           // SIGDN_NORMAL
        ParentRelativeParsing = unchecked((int)0x80018001),   // SIGDN_INFOLDER | SIGDN_FORPARSING
        DesktopAbsoluteParsing = unchecked((int)0x80028000),  // SIGDN_FORPARSING
        ParentRelativeEditing = unchecked((int)0x80031001),   // SIGDN_INFOLDER | SIGDN_FOREDITING
        DesktopAbsoluteEditing = unchecked((int)0x8004c000),  // SIGDN_FORPARSING | SIGDN_FORADDRESSBAR
        FileSystemPath = unchecked((int)0x80058000),             // SIGDN_FORPARSING
        Url = unchecked((int)0x80068000),                     // SIGDN_FORPARSING
        ParentRelativeForAddressBar = unchecked((int)0x8007c001),     // SIGDN_INFOLDER | SIGDN_FORPARSING | SIGDN_FORADDRESSBAR
        ParentRelative = unchecked((int)0x80080001)           // SIGDN_INFOLDER
    }

    [Flags]
    internal enum ShellFileGetAttributesOptions
    {
        /// <summary>
        /// The specified items can be copied.
        /// </summary>
        CanCopy = 0x00000001,

        /// <summary>
        /// The specified items can be moved.
        /// </summary>
        CanMove = 0x00000002,

        /// <summary>
        /// Shortcuts can be created for the specified items. This flag has the same value as DROPEFFECT. 
        /// The normal use of this flag is to add a Create Shortcut item to the shortcut menu that is displayed 
        /// during drag-and-drop operations. However, SFGAO_CANLINK also adds a Create Shortcut item to the Microsoft 
        /// Windows Explorer's File menu and to normal shortcut menus. 
        /// If this item is selected, your application's IContextMenu::InvokeCommand is invoked with the lpVerb 
        /// member of the CMINVOKECOMMANDINFO structure set to "link." Your application is responsible for creating the link.
        /// </summary>
        CanLink = 0x00000004,

        /// <summary>
        /// The specified items can be bound to an IStorage interface through IShellFolder::BindToObject.
        /// </summary>
        Storage = 0x00000008,

        /// <summary>
        /// The specified items can be renamed.
        /// </summary>
        CanRename = 0x00000010,

        /// <summary>
        /// The specified items can be deleted.
        /// </summary>
        CanDelete = 0x00000020,

        /// <summary>
        /// The specified items have property sheets.
        /// </summary>
        HasPropertySheet = 0x00000040,

        /// <summary>
        /// The specified items are drop targets.
        /// </summary>
        DropTarget = 0x00000100,

        /// <summary>
        /// This flag is a mask for the capability flags.
        /// </summary>
        CapabilityMask = 0x00000177,

        /// <summary>
        /// Windows 7 and later. The specified items are system items.
        /// </summary>
        System = 0x00001000,

        /// <summary>
        /// The specified items are encrypted.
        /// </summary>
        Encrypted = 0x00002000,

        /// <summary>
        /// Indicates that accessing the object = through IStream or other storage interfaces, 
        /// is a slow operation. 
        /// Applications should avoid accessing items flagged with SFGAO_ISSLOW.
        /// </summary>
        IsSlow = 0x00004000,

        /// <summary>
        /// The specified items are ghosted icons.
        /// </summary>
        Ghosted = 0x00008000,

        /// <summary>
        /// The specified items are shortcuts.
        /// </summary>
        Link = 0x00010000,

        /// <summary>
        /// The specified folder objects are shared.
        /// </summary>    
        Share = 0x00020000,

        /// <summary>
        /// The specified items are read-only. In the case of folders, this means 
        /// that new items cannot be created in those folders.
        /// </summary>
        ReadOnly = 0x00040000,

        /// <summary>
        /// The item is hidden and should not be displayed unless the 
        /// Show hidden files and folders option is enabled in Folder Settings.
        /// </summary>
        Hidden = 0x00080000,

        /// <summary>
        /// This flag is a mask for the display attributes.
        /// </summary>
        DisplayAttributeMask = 0x000FC000,

        /// <summary>
        /// The specified folders contain one or more file system folders.
        /// </summary>
        FileSystemAncestor = 0x10000000,

        /// <summary>
        /// The specified items are folders.
        /// </summary>
        Folder = 0x20000000,

        /// <summary>
        /// The specified folders or file objects are part of the file system 
        /// that is, they are files, directories, or root directories).
        /// </summary>
        FileSystem = 0x40000000,

        /// <summary>
        /// The specified folders have subfolders = and are, therefore, 
        /// expandable in the left pane of Windows Explorer).
        /// </summary>
        HasSubFolder = unchecked((int)0x80000000),

        /// <summary>
        /// This flag is a mask for the contents attributes.
        /// </summary>
        ContentsMask = unchecked((int)0x80000000),

        /// <summary>
        /// When specified as input, SFGAO_VALIDATE instructs the folder to validate that the items 
        /// pointed to by the contents of apidl exist. If one or more of those items do not exist, 
        /// IShellFolder::GetAttributesOf returns a failure code. 
        /// When used with the file system folder, SFGAO_VALIDATE instructs the folder to discard cached 
        /// properties retrieved by clients of IShellFolder2::GetDetailsEx that may 
        /// have accumulated for the specified items.
        /// </summary>
        Validate = 0x01000000,

        /// <summary>
        /// The specified items are on removable media or are themselves removable devices.
        /// </summary>
        Removable = 0x02000000,

        /// <summary>
        /// The specified items are compressed.
        /// </summary>
        Compressed = 0x04000000,

        /// <summary>
        /// The specified items can be browsed in place.
        /// </summary>
        Browsable = 0x08000000,

        /// <summary>
        /// The items are nonenumerated items.
        /// </summary>
        Nonenumerated = 0x00100000,

        /// <summary>
        /// The objects contain new content.
        /// </summary>
        NewContent = 0x00200000,

        /// <summary>
        /// It is possible to create monikers for the specified file objects or folders.
        /// </summary>
        CanMoniker = 0x00400000,

        /// <summary>
        /// Not supported.
        /// </summary>
        HasStorage = 0x00400000,

        /// <summary>
        /// Indicates that the item has a stream associated with it that can be accessed 
        /// by a call to IShellFolder::BindToObject with IID_IStream in the riid parameter.
        /// </summary>
        Stream = 0x00400000,

        /// <summary>
        /// Children of this item are accessible through IStream or IStorage. 
        /// Those children are flagged with SFGAO_STORAGE or SFGAO_STREAM.
        /// </summary>
        StorageAncestor = 0x00800000,

        /// <summary>
        /// This flag is a mask for the storage capability attributes.
        /// </summary>
        StorageCapabilityMask = 0x70C50008,

        /// <summary>
        /// Mask used by PKEY_SFGAOFlags to remove certain values that are considered 
        /// to cause slow calculations or lack context. 
        /// Equal to SFGAO_VALIDATE | SFGAO_ISSLOW | SFGAO_HASSUBFOLDER.
        /// </summary>
        PkeyMask = unchecked((int)0x81044000),
    }

    internal enum SICHINTF
    {
        SICHINT_DISPLAY = 0x00000000,
        SICHINT_CANONICAL = 0x10000000,
        SICHINT_TEST_FILESYSPATH_IF_NOT_EQUAL = 0x20000000,
        SICHINT_ALLFIELDS = unchecked((int)0x80000000)
    }

    [ComImport]
    [Guid(ShellIIDGuid.IShellItem)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        // Not supported: IBindCtx.
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult BindToHandler(
            [In] IntPtr pbc,
            [In] ref Guid bhid,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IShellFolder ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetDisplayName(
            [In] ShellItemDesignNameOptions sigdnName,
            out IntPtr ppszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetAttributes([In] ShellFileGetAttributesOptions sfgaoMask, out ShellFileGetAttributesOptions psfgaoAttribs);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult Compare(
            [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
            [In] SICHINTF hint,
            out int piOrder);
    }

    internal enum FileDialogEventShareViolationResponse
    {
        Default = 0x00000000,
        Accept = 0x00000001,
        Refuse = 0x00000002
    }

    internal enum FileDialogEventOverwriteResponse
    {
        Default = 0x00000000,
        Accept = 0x00000001,
        Refuse = 0x00000002
    }

    [ComImport]
    [Guid(ShellIIDGuid.IFileDialogEvents)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileDialogEvents
    {
        // NOTE: some of these callbacks are cancelable - returning S_FALSE means that 
        // the dialog should not proceed (e.g. with closing, changing folder); to 
        // support this, we need to use the PreserveSig attribute to enable us to return
        // the proper HRESULT.

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime),
        PreserveSig]
        HResult OnFileOk([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime),
        PreserveSig]
        HResult OnFolderChanging(
            [In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd,
            [In, MarshalAs(UnmanagedType.Interface)] IShellItem psiFolder);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnFolderChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnSelectionChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnShareViolation(
            [In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd,
            [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
            out FileDialogEventShareViolationResponse pResponse);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnTypeChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OnOverwrite([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd,
            [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
            out FileDialogEventOverwriteResponse pResponse);
    }

    internal enum FileDialogAddPlacement
    {
        Bottom = 0x00000000,
        Top = 0x00000001,
    }

    [Flags]
    internal enum FileOpenOptions
    {
        None = 0,
        OverwritePrompt = 0x00000002,
        StrictFileTypes = 0x00000004,
        NoChangeDirectory = 0x00000008,
        PickFolders = 0x00000020,
        // Ensure that items returned are filesystem items.
        ForceFilesystem = 0x00000040,
        // Allow choosing items that have no storage.
        AllNonStorageItems = 0x00000080,
        NoValidate = 0x00000100,
        AllowMultiSelect = 0x00000200,
        PathMustExist = 0x00000800,
        FileMustExist = 0x00001000,
        CreatePrompt = 0x00002000,
        ShareAware = 0x00004000,
        NoReadOnlyReturn = 0x00008000,
        NoTestFileCreate = 0x00010000,
        HideMruPlaces = 0x00020000,
        HidePinnedPlaces = 0x00040000,
        NoDereferenceLinks = 0x00100000,
        DontAddToRecent = 0x02000000,
        ForceShowHidden = 0x10000000,
        DefaultNoMiniMode = 0x20000000
    }


    [ComImport]
    [Guid(ShellIIDGuid.IFileDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileDialog : IModalWindow
    {
        // Defined on IModalWindow - repeated here due to requirements of COM interop layer.
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime),
        PreserveSig]
        HResult Show([In] IntPtr parent);

        // IFileDialog-Specific interface members.

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileTypes(
            [In] uint cFileTypes,
            [In, MarshalAs(UnmanagedType.LPArray)] FilterSpec[] rgFilterSpec);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileTypeIndex([In] uint iFileType);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFileTypeIndex(out uint piFileType);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Advise(
            [In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde,
            out uint pdwCookie);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Unadvise([In] uint dwCookie);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetOptions([In] FileOpenOptions fos);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetOptions(out FileOpenOptions pfos);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, FileDialogAddPlacement fdap);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Close([MarshalAs(UnmanagedType.Error)] int hr);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetClientGuid([In] ref Guid guid);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void ClearClientData();

        // Not supported:  IShellItemFilter is not defined, converting to IntPtr.
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
    }


    internal enum ShellItemAttributeOptions
    {
        // if multiple items and the attirbutes together.
        And = 0x00000001,
        // if multiple items or the attributes together.
        Or = 0x00000002,
        // Call GetAttributes directly on the 
        // ShellFolder for multiple attributes.
        AppCompat = 0x00000003,

        // A mask for SIATTRIBFLAGS_AND, SIATTRIBFLAGS_OR, and SIATTRIBFLAGS_APPCOMPAT. Callers normally do not use this value.
        Mask = 0x00000003,

        // Windows 7 and later. Examine all items in the array to compute the attributes. 
        // Note that this can result in poor performance over large arrays and therefore it 
        // should be used only when needed. Cases in which you pass this flag should be extremely rare.
        AllItems = 0x00004000
    }


    [ComImport]
    [Guid(ShellIIDGuid.IShellItemArray)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        // Not supported: IBindCtx.
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult BindToHandler(
            [In, MarshalAs(UnmanagedType.Interface)] IntPtr pbc,
            [In] ref Guid rbhid,
            [In] ref Guid riid,
            out IntPtr ppvOut);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetPropertyStore(
            [In] int Flags,
            [In] ref Guid riid,
            out IntPtr ppv);

#if PROPERTIES
		[PreserveSig]
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		HResult GetPropertyDescriptionList(
			[In] ref PropertyKey keyType,
			[In] ref Guid riid,
			out IntPtr ppv);
#else
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetPropertyDescriptionList(
            [In] IntPtr keyType,
            [In] ref Guid riid,
            out IntPtr ppv);
#endif

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetAttributes(
            [In] ShellItemAttributeOptions dwAttribFlags,
            [In] ShellFileGetAttributesOptions sfgaoMask,
            out ShellFileGetAttributesOptions psfgaoAttribs);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetCount(out uint pdwNumItems);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetItemAt(
            [In] uint dwIndex,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        // Not supported: IEnumShellItems (will use GetCount and GetItemAt instead).
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult EnumItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenumShellItems);
    }

    /// <summary>
    /// Indicate flags that modify the property store object retrieved by methods 
    /// that create a property store, such as IShellItem2::GetPropertyStore or 
    /// IPropertyStoreFactory::GetPropertyStore.
    /// </summary>
    [Flags]
    internal enum GetPropertyStoreOptions
    {
        /// <summary>
        /// Meaning to a calling process: Return a read-only property store that contains all 
        /// properties. Slow items (offline files) are not opened. 
        /// Combination with other flags: Can be overridden by other flags.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Meaning to a calling process: Include only properties directly from the property
        /// handler, which opens the file on the disk, network, or device. Meaning to a file 
        /// folder: Only include properties directly from the handler.
        /// 
        /// Meaning to other folders: When delegating to a file folder, pass this flag on 
        /// to the file folder; do not do any multiplexing (MUX). When not delegating to a 
        /// file folder, ignore this flag instead of returning a failure code.
        /// 
        /// Combination with other flags: Cannot be combined with GPS_TEMPORARY, 
        /// GPS_FASTPROPERTIESONLY, or GPS_BESTEFFORT.
        /// </summary>
        HandlePropertiesOnly = 0x1,

        /// <summary>
        /// Meaning to a calling process: Can write properties to the item. 
        /// Note: The store may contain fewer properties than a read-only store. 
        /// 
        /// Meaning to a file folder: ReadWrite.
        /// 
        /// Meaning to other folders: ReadWrite. Note: When using default MUX, 
        /// return a single unmultiplexed store because the default MUX does not support ReadWrite.
        /// 
        /// Combination with other flags: Cannot be combined with GPS_TEMPORARY, GPS_FASTPROPERTIESONLY, 
        /// GPS_BESTEFFORT, or GPS_DELAYCREATION. Implies GPS_HANDLERPROPERTIESONLY.
        /// </summary>
        ReadWrite = 0x2,

        /// <summary>
        /// Meaning to a calling process: Provides a writable store, with no initial properties, 
        /// that exists for the lifetime of the Shell item instance; basically, a property bag 
        /// attached to the item instance. 
        /// 
        /// Meaning to a file folder: Not applicable. Handled by the Shell item.
        /// 
        /// Meaning to other folders: Not applicable. Handled by the Shell item.
        /// 
        /// Combination with other flags: Cannot be combined with any other flag. Implies GPS_READWRITE
        /// </summary>
        Temporary = 0x4,

        /// <summary>
        /// Meaning to a calling process: Provides a store that does not involve reading from the 
        /// disk or network. Note: Some values may be different, or missing, compared to a store 
        /// without this flag. 
        /// 
        /// Meaning to a file folder: Include the "innate" and "fallback" stores only. Do not load the handler.
        /// 
        /// Meaning to other folders: Include only properties that are available in memory or can 
        /// be computed very quickly (no properties from disk, network, or peripheral IO devices). 
        /// This is normally only data sources from the IDLIST. When delegating to other folders, pass this flag on to them.
        /// 
        /// Combination with other flags: Cannot be combined with GPS_TEMPORARY, GPS_READWRITE, 
        /// GPS_HANDLERPROPERTIESONLY, or GPS_DELAYCREATION.
        /// </summary>
        FastPropertiesOnly = 0x8,

        /// <summary>
        /// Meaning to a calling process: Open a slow item (offline file) if necessary. 
        /// Meaning to a file folder: Retrieve a file from offline storage, if necessary. 
        /// Note: Without this flag, the handler is not created for offline files.
        /// 
        /// Meaning to other folders: Do not return any properties that are very slow.
        /// 
        /// Combination with other flags: Cannot be combined with GPS_TEMPORARY or GPS_FASTPROPERTIESONLY.
        /// </summary>
        OpensLowItem = 0x10,

        /// <summary>
        /// Meaning to a calling process: Delay memory-intensive operations, such as file access, until 
        /// a property is requested that requires such access. 
        /// 
        /// Meaning to a file folder: Do not create the handler until needed; for example, either 
        /// GetCount/GetAt or GetValue, where the innate store does not satisfy the request. 
        /// Note: GetValue might fail due to file access problems.
        /// 
        /// Meaning to other folders: If the folder has memory-intensive properties, such as 
        /// delegating to a file folder or network access, it can optimize performance by 
        /// supporting IDelayedPropertyStoreFactory and splitting up its properties into a 
        /// fast and a slow store. It can then use delayed MUX to recombine them.
        /// 
        /// Combination with other flags: Cannot be combined with GPS_TEMPORARY or 
        /// GPS_READWRITE
        /// </summary>
        DelayCreation = 0x20,

        /// <summary>
        /// Meaning to a calling process: Succeed at getting the store, even if some 
        /// properties are not returned. Note: Some values may be different, or missing,
        /// compared to a store without this flag. 
        /// 
        /// Meaning to a file folder: Succeed and return a store, even if the handler or 
        /// innate store has an error during creation. Only fail if substores fail.
        /// 
        /// Meaning to other folders: Succeed on getting the store, even if some properties 
        /// are not returned.
        /// 
        /// Combination with other flags: Cannot be combined with GPS_TEMPORARY, 
        /// GPS_READWRITE, or GPS_HANDLERPROPERTIESONLY.
        /// </summary>
        BestEffort = 0x40,

        /// <summary>
        /// Mask for valid GETPROPERTYSTOREFLAGS values.
        /// </summary>
        MaskValid = 0xff,
    }

    /// <summary>
    /// Defines a unique key for a Shell Property
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PropertyKey : IEquatable<PropertyKey>
    {
        #region Private Fields

        private Guid formatId;
        private Int32 propertyId;

        #endregion

        #region Public Properties
        /// <summary>
        /// A unique GUID for the property
        /// </summary>
        public Guid FormatId
        {
            get
            {
                return formatId;
            }
        }

        /// <summary>
        ///  Property identifier (PID)
        /// </summary>
        public Int32 PropertyId
        {
            get
            {
                return propertyId;
            }
        }

        #endregion

        #region Public Construction

        /// <summary>
        /// PropertyKey Constructor
        /// </summary>
        /// <param name="formatId">A unique GUID for the property</param>
        /// <param name="propertyId">Property identifier (PID)</param>
        public PropertyKey(Guid formatId, Int32 propertyId)
        {
            this.formatId = formatId;
            this.propertyId = propertyId;
        }

        /// <summary>
        /// PropertyKey Constructor
        /// </summary>
        /// <param name="formatId">A string represenstion of a GUID for the property</param>
        /// <param name="propertyId">Property identifier (PID)</param>
        public PropertyKey(string formatId, Int32 propertyId)
        {
            this.formatId = new Guid(formatId);
            this.propertyId = propertyId;
        }

        #endregion

        #region IEquatable<PropertyKey> Members

        /// <summary>
        /// Returns whether this object is equal to another. This is vital for performance of value types.
        /// </summary>
        /// <param name="other">The object to compare against.</param>
        /// <returns>Equality result.</returns>
        public bool Equals(PropertyKey other)
        {
            return other.Equals((object)this);
        }

        #endregion

        #region equality and hashing

        /// <summary>
        /// Returns the hash code of the object. This is vital for performance of value types.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return formatId.GetHashCode() ^ propertyId;
        }

        /// <summary>
        /// Returns whether this object is equal to another. This is vital for performance of value types.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>Equality result.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is PropertyKey))
                return false;

            PropertyKey other = (PropertyKey)obj;
            return other.formatId.Equals(formatId) && (other.propertyId == propertyId);
        }

        /// <summary>
        /// Implements the == (equality) operator.
        /// </summary>
        /// <param name="propKey1">First property key to compare.</param>
        /// <param name="propKey2">Second property key to compare.</param>
        /// <returns>true if object a equals object b. false otherwise.</returns>        
        public static bool operator ==(PropertyKey propKey1, PropertyKey propKey2)
        {
            return propKey1.Equals(propKey2);
        }

        /// <summary>
        /// Implements the != (inequality) operator.
        /// </summary>
        /// <param name="propKey1">First property key to compare</param>
        /// <param name="propKey2">Second property key to compare.</param>
        /// <returns>true if object a does not equal object b. false otherwise.</returns>
        public static bool operator !=(PropertyKey propKey1, PropertyKey propKey2)
        {
            return !propKey1.Equals(propKey2);
        }

        /// <summary>
        /// Override ToString() to provide a user friendly string representation
        /// </summary>
        /// <returns>String representing the property key</returns>        
        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0}, {1}",
                formatId.ToString("B"), propertyId);
        }

        #endregion
    }


    [ComImport]
    [Guid(ShellIIDGuid.IShellItem2)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem2 : IShellItem
    {
        // Not supported: IBindCtx.
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult BindToHandler(
            [In] IntPtr pbc,
            [In] ref Guid bhid,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IShellFolder ppv);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetDisplayName(
            [In] ShellItemDesignNameOptions sigdnName,
            [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetAttributes([In] ShellFileGetAttributesOptions sfgaoMask, out ShellFileGetAttributesOptions psfgaoAttribs);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Compare(
            [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi,
            [In] uint hint,
            out int piOrder);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetPropertyStoreWithCreateObject([In] GetPropertyStoreOptions Flags, [In, MarshalAs(UnmanagedType.IUnknown)] object punkCreateObject, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetPropertyDescriptionList([In] ref PropertyKey keyType, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult Update([In, MarshalAs(UnmanagedType.Interface)] IBindCtx pbc);

#if PROPERTIES
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), PreserveSig]
        int GetPropertyStore(
            [In] GetPropertyStoreOptions Flags,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetProperty([In] ref PropertyKey key, [Out] PropVariant ppropvar);

		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetPropertyStoreForKeys([In] ref PropertyKey rgKeys, [In] uint cKeys, [In] GetPropertyStoreOptions Flags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out IPropertyStore ppv);
#else
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), PreserveSig]
        int GetPropertyStore(
            [In] GetPropertyStoreOptions Flags,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetProperty([In] ref PropertyKey key, [Out] IntPtr ppropvar);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetPropertyStoreForKeys([In] ref PropertyKey rgKeys, [In] uint cKeys, [In] GetPropertyStoreOptions Flags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out IntPtr ppv);
#endif

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetCLSID([In] ref PropertyKey key, out Guid pclsid);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFileTime([In] ref PropertyKey key, out System.Runtime.InteropServices.ComTypes.FILETIME pft);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetInt32([In] ref PropertyKey key, out int pi);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        HResult GetString([In] ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] out string ppsz);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetUInt32([In] ref PropertyKey key, out uint pui);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetUInt64([In] ref PropertyKey key, out ulong pull);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetBool([In] ref PropertyKey key, out int pf);
    }

    [ComImport]
    [Guid(ShellIIDGuid.IFileOpenDialog)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOpenDialog : IFileDialog
    {
        // Defined on IModalWindow - repeated here due to requirements of COM interop layer.
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [PreserveSig]
        HResult Show([In] IntPtr parent);

        // Defined on IFileDialog - repeated here due to requirements of COM interop layer.
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileTypes([In] uint cFileTypes, [In] ref FilterSpec rgFilterSpec);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileTypeIndex([In] uint iFileType);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFileTypeIndex(out uint piFileType);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Advise(
            [In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde,
            out uint pdwCookie);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Unadvise([In] uint dwCookie);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetOptions([In] FileOpenOptions fos);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetOptions(out FileOpenOptions pfos);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, FileDialogAddPlacement fdap);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Close([MarshalAs(UnmanagedType.Error)] int hr);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetClientGuid([In] ref Guid guid);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void ClearClientData();

        // Not supported:  IShellItemFilter is not defined, converting to IntPtr.
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);

        // Defined by IFileOpenDialog.
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetResults([MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppenum);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetSelectedItems([MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppsai);
    }


    public static class FolderBrowserDialog
	{
        /// <summary>Shows the folder browser dialog. Returns <see langword="null"/> if the user cancelled the dialog. Otherwise returns the selected path.</summary>
        public static String ShowDialog(IntPtr parentHWnd, String title, String initialDirectory)
		{
			NativeFileOpenDialog nfod = new NativeFileOpenDialog();
			try
			{
				return ShowDialogInner( nfod, parentHWnd, title, initialDirectory );
			}
			finally
			{
				_ = Marshal.ReleaseComObject( nfod );
			}
		}

        internal static class ShellNativeMethods
        {
            [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern HResult SHCreateItemFromParsingName(
                [MarshalAs(UnmanagedType.LPWStr)] string path,
                // The following parameter is not used - binding context.
                IntPtr pbc,
                ref Guid riid,
                [MarshalAs(UnmanagedType.Interface)] out IShellItem2 shellItem);
        }

        internal static IShellItem2 ParseShellItem2Name(String value)
        {
            Guid ishellItem2GuidCopy = new Guid(ShellIIDGuid.IShellItem2);

            HResult hresult = ShellNativeMethods.SHCreateItemFromParsingName(value, IntPtr.Zero, ref ishellItem2GuidCopy, out IShellItem2 shellItem);
            if (hresult == HResult.Ok)
            {
                return shellItem;
            }
            else
            {
                // TODO: Handle HRESULT error codes?
                return null;
            }
        }

        internal static Boolean ValidateDialogShowHResult(this HResult dialogHResult)
        {
            if (dialogHResult.TryGetWin32ErrorCode(out Win32ErrorCodes win32Code))
            {
                if (win32Code == Win32ErrorCodes.Success)
                {
                    // OK.
                    return true;
                }
                else if (win32Code == Win32ErrorCodes.ErrorCancelled)
                {
                    // Cancelled
                    return false;
                }
                else
                {
                    // Other Win32 error:

                    String msg = String.Format(CultureInfo.CurrentCulture, "Unexpected Win32 error code 0x{0:X2} in HRESULT 0x{1:X4} returned from IModalWindow.Show(...).", (Int32)win32Code, (Int32)dialogHResult);
                    throw new Win32Exception(error: (Int32)win32Code, message: msg);
                }
            }
            else if (dialogHResult.IsValidHResult())
            {
                const UInt16 RPC_E_SERVERFAULT = 0x0105;

                if (dialogHResult.GetFacility() == HResults.HResultFacility.Rpc && dialogHResult.GetCode() == RPC_E_SERVERFAULT)
                {
                    // This error happens when calling `IModalWindow.Show` instead of using the `Show` method on a different interface, like `IFileOpenDialog.Show`.
                    String msg = String.Format(CultureInfo.CurrentCulture, "Unexpected RPC HRESULT: 0x{0:X4} (RPC Error {1:X2}) returned from IModalWindow.Show(...). This particular RPC error suggests the dialog was accessed via the wrong COM interface.", (Int32)dialogHResult, RPC_E_SERVERFAULT);
                    throw new ExternalException(msg, errorCode: (Int32)dialogHResult);
                }
                else
                {
                    // Fall-through to below:
                }
            }
            else
            {
                // Fall-through to below:
            }

            {
                // Other HRESULT (non-Win32 error):
                // https://stackoverflow.com/questions/11158379/how-can-i-throw-an-exception-with-a-certain-hresult

                String msg = String.Format(CultureInfo.CurrentCulture, "Unexpected HRESULT: 0x{0:X4} returned from IModalWindow.Show(...).", (Int32)dialogHResult);
                throw new ExternalException(msg, errorCode: (Int32)dialogHResult);
            }
        }

        internal static IReadOnlyList<String> GetFileNames(IShellItemArray items)
        {
            HResult hresult = items.GetCount(out UInt32 count);
            if (hresult != HResult.Ok)
            {
                throw new Exception("IShellItemArray.GetCount failed. HResult: " + hresult); // TODO: Will this ever happen?
            }
            else
            {
                List<String> list = new List<String>(capacity: (Int32)count);

                for (int i = 0; i < count; i++)
                {
                    IShellItem shellItem = GetShellItemAt(items, i);
                    String fileName = GetFileNameFromShellItem(shellItem);
                    if (fileName != null)
                    {
                        list.Add(fileName);
                    }
                }

                return list;
            }
        }

        internal static String GetFileNameFromShellItem(IShellItem item)
        {
            if (item is null)
            {
                return null;
            }
            else
            {
                HResult hr = item.GetDisplayName(ShellItemDesignNameOptions.DesktopAbsoluteParsing, out IntPtr pszString);
                if (hr == HResult.Ok && pszString != IntPtr.Zero)
                {
#if NETCOREAPP3_1_OR_GREATER
					String fileName = Marshal.PtrToStringAuto( pszString )!; // `PtrToStringAuto` won't return `null` if its `ptr` argument is not null, which we check for.
#else
                    String fileName = Marshal.PtrToStringAuto(pszString);
#endif
                    Marshal.FreeCoTaskMem(pszString);
                    return fileName;
                }
                else
                {
                    return null;
                }
            }
        }


        internal static IShellItem GetShellItemAt(IShellItemArray array, int i)
        {
            if (array is null) throw new ArgumentNullException(nameof(array));

            HResult hr = array.GetItemAt((UInt32)i, out IShellItem result);
            if (hr == HResult.Ok)
            {
                return result;
            }
            else
            {
                return null;
            }
        }


        private static String ShowDialogInner(IFileOpenDialog dialog, IntPtr parentHWnd, String title, String initialDirectory)
		{
			//IFileDialog ifd = dialog;
			FileOpenOptions flags =
				FileOpenOptions.NoTestFileCreate |
				FileOpenOptions.PathMustExist |
				FileOpenOptions.PickFolders |
				FileOpenOptions.ForceFilesystem;

			dialog.SetOptions( flags );
			
			if( title != null )
			{
				dialog.SetTitle( title );
			}

			if( initialDirectory != null )
			{
				IShellItem2 initialDirectoryShellItem = ParseShellItem2Name( initialDirectory );
				if( initialDirectoryShellItem != null )
				{
					dialog.SetFolder( initialDirectoryShellItem );
				}
			}

			//

			HResult hr = dialog.Show( parentHWnd );
			if( hr.ValidateDialogShowHResult() )
			{
				dialog.GetResults( out IShellItemArray resultsArray );

				IReadOnlyList<String> fileNames = GetFileNames( resultsArray );

				if( fileNames.Count == 0 )
				{
					return null;
				}
				else
				{
					return fileNames[0];
				}
			}
			else
			{
				// User cancelled.
				return null;
			}
		}
	}
}
