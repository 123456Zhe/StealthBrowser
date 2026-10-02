using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using WinForms = System.Windows.Forms;

namespace StealthBrowser
{
    public partial class MainWindow : Window
    {
        // ============ 隐身：Windows 官方 API，本进程直接调自己窗口 ============
        const uint WDA_NONE = 0;
        const uint WDA_MONITOR = 1;               // Win7/8：截屏里黑块
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Win10 2004+：截屏里透出后面的内容

        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OSVERSIONINFOEX
        {
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
        static extern int RtlGetVersion(ref OSVERSIONINFOEX versionInfo);

        static uint BestAffinity()
        {
            try
            {
                var info = new OSVERSIONINFOEX();
                info.dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                if (RtlGetVersion(ref info) == 0)
                {
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

        // ============ 老板键：Ctrl+Alt+H ============
        const int HOTKEY_ID = 0xB055;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_ALT = 0x0001;
        const uint VK_H = 0x48;
        const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const string HOME_URL = "https://www.bing.com";
        const string SEARCH_URL = "https://www.bing.com/search?q=";

        public class TabInfo
        {
            public Border Header;
            public TextBlock Title;
            public BrowserTab Tab;
        }

        readonly List<TabInfo> tabList = new List<TabInfo>();
        TabInfo current;
        CoreWebView2Environment env;
        bool stealthOn = true;
        uint stealthAffinity = WDA_EXCLUDEFROMCAPTURE;
        WinForms.NotifyIcon tray;

        public MainWindow()
        {
            InitializeComponent();

            BtnBack.Click += (s, e) => current?.Tab.GoBack();
            BtnForward.Click += (s, e) => current?.Tab.GoForward();
            BtnReload.Click += (s, e) => current?.Tab.Reload();
            BtnHome.Click += (s, e) => current?.Tab.Navigate(HOME_URL);
            BtnNewTab.Click += (s, e) => OpenTab(HOME_URL);
            BtnStealth.Click += (s, e) => { stealthOn = !stealthOn; ApplyStealth(); };
            AddressBar.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) NavigateAddressBar();
            };

            tray = new WinForms.NotifyIcon();
            tray.Icon = System.Drawing.SystemIcons.Shield;
            tray.Text = "StealthBrowser（双击还原）";
            tray.Visible = false;
            tray.DoubleClick += (s, e) => ShowFromTray();

            Loaded += async (s, e) =>
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dataDir = Path.Combine(baseDir, "data");
                string fixedRuntime = FindFixedRuntime(baseDir); // 内嵌内核优先
                try
                {
                    env = await CoreWebView2Environment.CreateAsync(fixedRuntime, dataDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "WebView2 运行环境初始化失败：\n" + ex.Message +
                        "\n\nWin10/11 一般自带；Win7 需要手动安装 WebView2 Runtime：\n" +
                        "https://developer.microsoft.com/microsoft-edge/webview2/",
                        "StealthBrowser", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                OpenTab(HOME_URL);
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hs = (HwndSource)PresentationSource.FromVisual(this);
            hs.AddHook(WndProcHook);
            RegisterHotKey(hs.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_H);

            stealthAffinity = BestAffinity();
            ApplyStealth();
        }

        IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleBossKey();
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            var hs = (HwndSource)PresentationSource.FromVisual(this);
            if (hs != null) UnregisterHotKey(hs.Handle, HOTKEY_ID);
            tray.Visible = false;
            tray.Dispose();
            base.OnClosed(e);
        }

        // ---------------- 内嵌内核 ----------------
        // 程序目录下有 webview2-runtime/（Fixed Version Runtime）就用它，
        // 没有则回退到系统自带的 Evergreen Runtime。
        // 兼容两种布局：webview2-runtime/msedgewebview2.exe，
        // 或 webview2-runtime/Microsoft.WebView2.FixedVersionRuntime.<版本>.x64/msedgewebview2.exe
        static string FindFixedRuntime(string baseDir)
        {
            try
            {
                string rt = Path.Combine(baseDir, "webview2-runtime");
                if (File.Exists(Path.Combine(rt, "msedgewebview2.exe")))
                    return rt;
                if (Directory.Exists(rt))
                {
                    foreach (var sub in Directory.GetDirectories(rt, "Microsoft.WebView2.FixedVersionRuntime.*"))
                    {
                        if (File.Exists(Path.Combine(sub, "msedgewebview2.exe")))
                            return sub;
                    }
                }
            }
            catch { }
            return null;
        }

        // ---------------- 隐身 ----------------
        void ApplyStealth()
        {
            var hs = (HwndSource)PresentationSource.FromVisual(this);
            if (hs != null)
                SetWindowDisplayAffinity(hs.Handle, stealthOn ? stealthAffinity : WDA_NONE);
            string mode = stealthAffinity == WDA_EXCLUDEFROMCAPTURE ? "透明" : "黑块";
            BtnStealth.Content = stealthOn ? "● 隐身:开·" + mode : "○ 隐身:关";
            SetStatus(stealthOn ? "防截屏隐身已开启（" + mode + "模式）" : "防截屏隐身已关闭");
        }

        // ---------------- 老板键 ----------------
        void ToggleBossKey()
        {
            if (IsVisible)
            {
                Hide();
                tray.Visible = true;
                tray.ShowBalloonTip(1500, "StealthBrowser", "已隐藏到托盘，再按 Ctrl+Alt+H 恢复",
                    WinForms.ToolTipIcon.Info);
            }
            else
            {
                ShowFromTray();
            }
        }

        void ShowFromTray()
        {
            tray.Visible = false;
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // ---------------- 标签页 ----------------
        public void OpenTab(string url)
        {
            if (env == null) return;
            var tab = new BrowserTab(this);

            var title = new TextBlock
            {
                Text = "新标签页",
                MaxWidth = 170,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextBrush"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var close = new Button
            {
                Content = "×",
                Style = (Style)FindResource("TabCloseButton"),
                ToolTip = "关闭标签页"
            };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(title);
            panel.Children.Add(close);
            var header = new Border
            {
                Child = panel,
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(12, 7, 6, 7),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };

            var info = new TabInfo { Header = header, Title = title, Tab = tab };
            tab.SetTabInfo(info);
            header.MouseLeftButtonDown += (s, e) => SelectTab(info);
            close.PreviewMouseLeftButtonDown += (s, e) => e.Handled = true; // 别冒泡成选中
            close.Click += (s, e) => CloseTab(info);

            TabStrip.Children.Add(header);
            tabList.Add(info);
            tab.Init(env, url);
            SelectTab(info);
        }

        public void CloseTab(TabInfo info)
        {
            if (info == null) return;
            tabList.Remove(info);
            TabStrip.Children.Remove(info.Header);
            try { info.Tab.Web.Dispose(); } catch { }
            if (current == info) current = null;
            if (tabList.Count == 0)
                OpenTab(HOME_URL);
            else if (current == null)
                SelectTab(tabList[tabList.Count - 1]);
        }

        void SelectTab(TabInfo info)
        {
            current = info;
            var active = (Brush)FindResource("TabActiveBrush");
            foreach (var t in tabList)
                t.Header.Background = (t == info) ? active : Brushes.Transparent;
            TabContent.Content = info.Tab;
            SyncAddressBar(info);
        }

        public void SyncTabTitle(TabInfo info)
        {
            if (info == null) return;
            try
            {
                string title = info.Tab.Web.CoreWebView2.DocumentTitle;
                if (string.IsNullOrEmpty(title)) title = "新标签页";
                info.Title.Text = title;
            }
            catch { }
        }

        public void SyncAddressBar(TabInfo info)
        {
            if (info == null || info != current) return;
            try
            {
                var src = info.Tab.Web.Source;
                if (src != null) AddressBar.Text = src.ToString();
            }
            catch { }
        }

        public void SetStatus(string s)
        {
            StatusText.Text = s;
        }

        // ---------------- 地址栏 ----------------
        void NavigateAddressBar()
        {
            if (current == null) return;
            string url = SmartUrl(AddressBar.Text);
            current.Tab.Navigate(url);
            SetStatus("正在打开：" + url);
        }

        static string SmartUrl(string input)
        {
            input = (input ?? "").Trim();
            if (input.StartsWith("http://") || input.StartsWith("https://"))
                return input;
            if (input.StartsWith("localhost") ||
                (input.Contains(".") && !input.Contains(" ")) ||
                input.StartsWith("about:") || input.StartsWith("edge://"))
                return input.Contains("://") ? input : "https://" + input;
            return SEARCH_URL + Uri.EscapeDataString(input);
        }
    }
}
