using System;

using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HPIZArchiver
{
    public class CollapsibleListView : ListView
    {
        private const int LVGF_STATE = 0x00000004;
        private const int LVGS_COLLAPSIBLE = 0x00000008;
        private const int LVM_FIRST = 0x1000;
        private const int LVM_SETGROUPINFO = (LVM_FIRST + 147);
        private const int LVM_INSERTGROUP = (LVM_FIRST + 145);
        private const int WM_LBUTTONUP = 0x0202;

        /// <summary>
        /// Intercepts <see cref="ListView.WndProc(ref Message)"/> calls
        /// </summary>
        /// <param name="message">Message</param>
        protected override void WndProc(ref Message message)
        {
            switch (message.Msg)
            {
                case WM_LBUTTONUP:
                    base.DefWndProc(ref message);
                    break;
                case LVM_INSERTGROUP:
                    base.WndProc(ref message);
                    var group = (LvGroup)Marshal.PtrToStructure(message.LParam, typeof(LvGroup));
                    SetGroupCollapsible(group.iGroupId);
                    return;
            }

            base.WndProc(ref message);
        }

        /// <summary>
        /// Sends the specified message to a window or windows.
        /// <seealso cref="https://docs.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-sendmessage"/>
        /// </summary>
        /// <param name="handle">A handle to the window 
        /// whose window procedure will receive the message.</param>
        /// <param name="message">The message to be sent.</param>
        /// <param name="wParam">Additional message-specific information.</param>
        /// <param name="lParam">Additional message-specific information.</param>
        /// <returns>The return value specifies the result of the message processing; 
        /// it depends on the message sent.</returns>
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr handle, int message, int wParam, IntPtr lParam);

        /// <summary>
        /// Represents native version of the <see cref="ListViewGroup"/> class
        /// <seealso cref="https://docs.microsoft.com/en-us/windows/desktop/api/commctrl/ns-commctrl-taglvgroup"/>
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

        /// <summary>
        /// Updates a <see cref="ListViewGroup"/> item's state within current instance as collapsible
        /// </summary>
        /// <param name="groupItemIndex">Group item index</param>
        private void SetGroupCollapsible(int groupItemIndex)
        {
            LvGroup group = new LvGroup();
            group.cbSize = Marshal.SizeOf(group);
            group.state = LVGS_COLLAPSIBLE;
            group.mask = LVGF_STATE;
            group.iGroupId = groupItemIndex;

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
    }
}
