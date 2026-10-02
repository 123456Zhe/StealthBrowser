using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StealthBrowser.Managers {

	/// <summary>
	/// 防截屏隐身：直接对自己进程的窗口调用 Windows 官方 API，
	/// 无跨进程注入，Defender 无感。
	/// Win10 2004+ 上截屏里透出后面的内容；更早的系统上只能是黑块（系统限制）。
	/// </summary>
	public static class StealthManager {

		public const uint WDA_NONE = 0;
		public const uint WDA_MONITOR = 1;
		public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

		public static bool StealthOn = true;
		public static uint StealthAffinity = WDA_EXCLUDEFROMCAPTURE;

		[DllImport("user32.dll")]
		private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct OSVERSIONINFOEX {
			public uint dwOSVersionInfoSize;
			public uint dwMajorVersion;
			public uint dwMinorVersion;
			public uint dwBuildNumber;
			public uint dwPlatformId;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string szCSDVersion;
			public ushort wServicePackMajor;
			public ushort wServicePackMinor;
			public ushort wSuiteMask;
			public byte wProductType;
			public byte wReserved;
		}
		[DllImport("ntdll.dll")]
		private static extern int RtlGetVersion(ref OSVERSIONINFOEX versionInfo);

		private static uint BestAffinity() {
			try {
				var info = new OSVERSIONINFOEX();
				info.dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(OSVERSIONINFOEX));
				if (RtlGetVersion(ref info) == 0) {
					if (info.dwMajorVersion > 10 ||
						(info.dwMajorVersion == 10 && info.dwBuildNumber >= 19041))
						return WDA_EXCLUDEFROMCAPTURE;
					if (info.dwMajorVersion >= 6)
						return WDA_MONITOR;
				}
			}
			catch { }
			return WDA_EXCLUDEFROMCAPTURE;
		}

		public static void Init() {
			StealthAffinity = BestAffinity();
		}

		public static void Apply(IntPtr hwnd) {
			if (hwnd == IntPtr.Zero) return;
			SetWindowDisplayAffinity(hwnd, StealthOn ? StealthAffinity : WDA_NONE);
		}

		public static string ModeName =>
			StealthAffinity == WDA_EXCLUDEFROMCAPTURE ? "透明" : "黑块";

		public static string ButtonText =>
			StealthOn ? ("隐身开·" + ModeName) : "隐身关";
	}
}
