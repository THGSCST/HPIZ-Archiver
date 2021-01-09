using System;
using System.Runtime.InteropServices;
namespace HPIZArchiver
{
	public static class TaskbarProgress
	{
		public enum ProgressState
		{
			None,
			Indeterminate,
			Normal,
			Error = 4,
			Paused = 8
		}

		[ComImportAttribute()]
		[GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
		[InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
		private interface ITaskbarList3
		{
			// ITaskbarList
			[PreserveSig]
			void HrInit();
			[PreserveSig]
			void AddTab(IntPtr hwnd);
			[PreserveSig]
			void DeleteTab(IntPtr hwnd);
			[PreserveSig]
			void ActivateTab(IntPtr hwnd);
			[PreserveSig]
			void SetActiveAlt(IntPtr hwnd);

			// ITaskbarList2
			[PreserveSig]
			void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

			// ITaskbarList3
			[PreserveSig]
			void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
			[PreserveSig]
			void SetProgressState(IntPtr hwnd, ProgressState state);
		}

		[GuidAttribute("56FDF344-FD6D-11d0-958A-006097C9A090")]
		[ClassInterfaceAttribute(ClassInterfaceType.None)]
		[ComImportAttribute()]
		private class TaskbarList { }

		private static ITaskbarList3 taskbarInstance = (ITaskbarList3) new TaskbarList();

		public static void SetState(IntPtr windowHandle, ProgressState taskbarState)
		{
			taskbarInstance.SetProgressState(windowHandle, taskbarState);
		}

		public static void SetValue(IntPtr windowHandle, int progressValue, int progressMax)
		{
			taskbarInstance.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
		}
	}
}
