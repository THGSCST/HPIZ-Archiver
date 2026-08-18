using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HPIZArchiver
{
    /// <summary>
    /// ListView with collapsible native groups and optionally read-only check boxes.
    /// Compatible with .NET Framework 4.8.
    /// </summary>
    public class CollapsibleListView : ListView
    {
        // ListView native messages.
        private const int LVM_FIRST = 0x1000;
        private const int LVM_INSERTGROUP = LVM_FIRST + 145;
        private const int LVM_SETGROUPINFO = LVM_FIRST + 147;
        private const int WM_LBUTTONUP = 0x0202;

        // LVGROUP flags.
        private const uint LVGF_STATE = 0x00000004;
        private const uint LVGS_COLLAPSIBLE = 0x00000008;

        private bool _checkBoxesLocked;
        private bool _allowCheckBoxChange;

        /// <summary>
        /// Gets or sets whether item check boxes are locked.
        /// The ListView itself remains enabled, so scrolling, selection,
        /// keyboard navigation and group collapsing continue to work.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Prevents item check box states from being changed while leaving the ListView enabled and scrollable.")]
        public bool CheckBoxesLocked
        {
            get { return _checkBoxesLocked; }
            set { _checkBoxesLocked = value; }
        }

        /// <summary>
        /// Changes an item's Checked state even when CheckBoxesLocked is true.
        /// Use this for application-controlled changes while the user interface is locked.
        /// </summary>
        public void SetItemChecked(int itemIndex, bool isChecked)
        {
            if (itemIndex < 0 || itemIndex >= Items.Count)
                throw new ArgumentOutOfRangeException("itemIndex");

            bool previous = _allowCheckBoxChange;
            _allowCheckBoxChange = true;

            try
            {
                Items[itemIndex].Checked = isChecked;
            }
            finally
            {
                _allowCheckBoxChange = previous;
            }
        }

        /// <summary>
        /// Prevents check state changes while CheckBoxesLocked is enabled.
        /// The control itself is never disabled, so its scroll bars keep working.
        /// </summary>
        protected override void OnItemCheck(ItemCheckEventArgs e)
        {
            bool lockChange = _checkBoxesLocked && !_allowCheckBoxChange;

            // Set before raising ItemCheck so subscribers see the effective state.
            if (lockChange)
                e.NewValue = e.CurrentValue;

            base.OnItemCheck(e);

            // Enforce the lock even if an ItemCheck subscriber changes NewValue.
            if (lockChange)
                e.NewValue = e.CurrentValue;
        }

        /// <summary>
        /// Intercepts native group insertion so every WinForms ListViewGroup
        /// becomes collapsible on the underlying Windows common control.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == LVM_INSERTGROUP && m.LParam != IntPtr.Zero)
            {
                // Read the group ID before forwarding the insertion message.
                // Only the initial LVGROUP fields are needed here.
                LvGroup nativeGroup = (LvGroup)Marshal.PtrToStructure(
                    m.LParam, typeof(LvGroup));

                base.WndProc(ref m);

                // LVM_INSERTGROUP returns -1 on failure.
                if (m.Result != new IntPtr(-1))
                    SetGroupCollapsible(nativeGroup.iGroupId);

                return;
            }

            // WinForms ListView does not by itself give the native group
            // expander everything it needs on mouse-up. The native default
            // window procedure must see WM_LBUTTONUP so the right-side
            // collapse/expand chevron can toggle the group.
            //
            // Do not replace this with only base.WndProc(ref m).
            if (m.Msg == WM_LBUTTONUP)
                base.DefWndProc(ref m);

            base.WndProc(ref m);
        }

        /// <summary>
        /// Adds LVGS_COLLAPSIBLE to an existing native ListView group.
        /// </summary>
        private void SetGroupCollapsible(int groupId)
        {
            LvGroup nativeGroup = new LvGroup
            {
                cbSize = (uint)Marshal.SizeOf(typeof(LvGroup)),
                mask = LVGF_STATE,
                stateMask = LVGS_COLLAPSIBLE,
                state = LVGS_COLLAPSIBLE
            };

            IntPtr buffer = IntPtr.Zero;

            try
            {
                buffer = Marshal.AllocHGlobal((int)nativeGroup.cbSize);
                Marshal.StructureToPtr(nativeGroup, buffer, false);

                // For LVM_SETGROUPINFO, wParam is the group ID.
                SendMessage(Handle, LVM_SETGROUPINFO, new IntPtr(groupId), buffer);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Native SendMessage signature using pointer-sized WPARAM/LRESULT,
        /// which is correct for both 32-bit and 64-bit processes.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);

        /// <summary>
        /// Initial portion of the native LVGROUP structure.
        /// Pointer fields are IntPtr because their contents are not needed;
        /// this avoids unnecessary string marshalling and remains x86/x64 safe.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LvGroup
        {
            public uint cbSize;
            public uint mask;
            public IntPtr pszHeader;
            public int cchHeader;
            public IntPtr pszFooter;
            public int cchFooter;
            public int iGroupId;
            public uint stateMask;
            public uint state;
            public uint uAlign;
        }
    }
}
