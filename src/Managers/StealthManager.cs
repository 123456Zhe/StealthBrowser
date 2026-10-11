using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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

		[DllImport("user32.dll")]
		private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
		private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		/// <summary>
		/// 找到本进程的 CEF 开发者工具顶层窗口并补上隐身，找到返回 true。
		/// （F12 开发者工具是 CEF 独立创建的顶层窗口，不在主窗口隐身覆盖范围内。）
		/// </summary>
		public static bool ApplyToDevTools() {
			bool found = false;
			uint selfPid = (uint)Process.GetCurrentProcess().Id;
			EnumWindows((hWnd, lParam) => {
				GetWindowThreadProcessId(hWnd, out uint pid);
				if (pid != selfPid) return true;
				var sb = new StringBuilder(256);
				GetWindowText(hWnd, sb, sb.Capacity);
				var title = sb.ToString();
				if (title.StartsWith("DevTools", StringComparison.OrdinalIgnoreCase) ||
					title.IndexOf("Developer Tools", StringComparison.OrdinalIgnoreCase) >= 0) {
					Apply(hWnd);
					found = true;
				}
				return true;
			}, IntPtr.Zero);
			return found;
		}

		/// <summary>
		/// 打开开发者工具后调用：窗口创建有延迟，轮询找到后补上隐身。
		/// </summary>
		public static void WatchDevTools() {
			var timer = new Timer { Interval = 400 };
			int tries = 0;
			timer.Tick += (s, e) => {
				tries++;
				if (ApplyToDevTools() || tries >= 15) {
					timer.Stop();
					timer.Dispose();
				}
			};
			timer.Start();
		}

		public static string ModeName =>
			StealthAffinity == WDA_EXCLUDEFROMCAPTURE ? "透明" : "黑块";

		public static string ButtonText =>
			StealthOn ? ("隐身开·" + ModeName) : "隐身关";
	}
}
