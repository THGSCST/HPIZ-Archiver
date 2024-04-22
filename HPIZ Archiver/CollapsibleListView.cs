using System;

using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HPIZArchiver
{
    public class CollapsibleListView : ListView
    {
        // Constants representing specific messages and group state flags
        private const int LVM_FIRST = 0x1000;
        private const int LVM_INSERTGROUP = (LVM_FIRST + 145);
        private const int LVM_SETGROUPINFO = (LVM_FIRST + 147);
        private const int LVGF_STATE = 0x00000004;
        private const int LVGS_COLLAPSIBLE = 0x00000008;
        private const int WM_LBUTTONUP = 0x0202;

        /// <summary>
        /// Intercepts Windows messages to handle group collapsibility and mouse events.
        /// </summary>
        /// <param name="m">The Windows Message to process.</param>
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_LBUTTONUP:
                    // Delegates the handling of mouse button up events to the default procedure
                    base.DefWndProc(ref m);
                    break;
                case LVM_INSERTGROUP:
                    // Handles insertion of a new group by setting its collapsibility before continuing with the default processing
                    base.WndProc(ref m);
                    var group = (LvGroup)Marshal.PtrToStructure(m.LParam, typeof(LvGroup));
                    SetGroupCollapsible(group.iGroupId);
                    return;
            }

            // Default message processing
            base.WndProc(ref m);
        }

        /// <summary>
        /// Makes a ListViewGroup collapsible by updating its state in the ListView.
        /// </summary>
        /// <param name="groupItemIndex">Index of the ListViewGroup.</param>
        private void SetGroupCollapsible(int groupItemIndex)
        {
            LvGroup group = new LvGroup
            {
                cbSize = Marshal.SizeOf(typeof(LvGroup)),
                state = LVGS_COLLAPSIBLE,
                mask = LVGF_STATE,
                iGroupId = groupItemIndex
            };

            IntPtr ip = IntPtr.Zero;
            try
            {
                ip = Marshal.AllocHGlobal(group.cbSize);
                Marshal.StructureToPtr(group, ip, false);
                SendMessage(Handle, LVM_SETGROUPINFO, groupItemIndex, ip);
            }
            finally
            {
                if (ip != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ip);
                }
            }
        }

        /// <summary>
        /// Sends a message to a window or windows, typically to modify their appearance or behavior.
        /// </summary>
        /// <param name="handle">Handle to the window receiving the message.</param>
        /// <param name="message">Message identifier.</param>
        /// <param name="wParam">Additional message-specific information.</param>
        /// <param name="lParam">Additional message-specific information.</param>
        /// <returns>Result of the message processing.</returns>
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr handle, int message, int wParam, IntPtr lParam);

        /// <summary>
        /// Struct representing a native ListView group.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LvGroup
        {
            public int cbSize;
            public int mask;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszHeader;
            public int cchHeader;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszFooter;
            public int cchFooter;
            public int iGroupId;
            public int stateMask;
            public int state;
            public int uAlign;
        }
    }

}
