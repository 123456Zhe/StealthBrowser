using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace StealthBrowser
{
    public class MainForm : Form
    {
        // ============ 隐身：Windows 官方 API ============
        // 本进程直接对自己的窗口调用，无跨进程注入，Defender 无感。
        const uint WDA_NONE = 0;
        const uint WDA_MONITOR = 1;              // Win7/8：截屏里黑块
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11; // Win10 2004+：截屏里透出后面的内容

        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        // RtlGetVersion 不说谎（Environment.OSVersion 在无 manifest 时会谎报 6.2）
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
                        return WDA_EXCLUDEFROMCAPTURE; // Win10 2004+：透明隐身
                    if (info.dwMajorVersion >= 6)
                        return WDA_MONITOR;            // Vista/7/8：黑块降级
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

        ToolStrip toolbar;
        ToolStripButton btnBack, btnForward, btnReload, btnHome, btnGo, btnNewTab, btnStealth;
        ToolStripTextBox addressBar;
        TabControl tabs;
        StatusStrip statusStrip;
        ToolStripStatusLabel statusLabel;
        NotifyIcon tray;

        CoreWebView2Environment env;
        bool stealthOn = true;
        uint stealthAffinity = WDA_EXCLUDEFROMCAPTURE;

        public MainForm()
        {
            Text = "StealthBrowser";
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            toolbar = new ToolStrip();
            toolbar.GripStyle = ToolStripGripStyle.Hidden;
            toolbar.Dock = DockStyle.Top;

            btnBack = new ToolStripButton("←") { ToolTipText = "后退" };
            btnBack.Click += (s, e) => CurrentTab()?.GoBack();
            btnForward = new ToolStripButton("→") { ToolTipText = "前进" };
            btnForward.Click += (s, e) => CurrentTab()?.GoForward();
            btnReload = new ToolStripButton("↻") { ToolTipText = "刷新" };
            btnReload.Click += (s, e) => CurrentTab()?.Reload();
            btnHome = new ToolStripButton("⌂") { ToolTipText = "主页" };
            btnHome.Click += (s, e) => CurrentTab()?.Navigate(HOME_URL);

            addressBar = new ToolStripTextBox();
            addressBar.AutoSize = false;
            addressBar.Height = 26;
            addressBar.KeyDown += AddressBar_KeyDown;

            btnGo = new ToolStripButton("前往") { ToolTipText = "打开地址栏中的网址" };
            btnGo.Click += (s, e) => NavigateAddressBar();
            btnNewTab = new ToolStripButton("＋标签页") { ToolTipText = "新标签页" };
            btnNewTab.Click += (s, e) => OpenTab(HOME_URL);
            btnStealth = new ToolStripButton() { ToolTipText = "点击开关防截屏隐身" };
            btnStealth.Click += (s, e) => { stealthOn = !stealthOn; ApplyStealth(); };

            toolbar.Items.AddRange(new ToolStripItem[]
                { btnBack, btnForward, btnReload, btnHome, addressBar, btnGo, btnNewTab, btnStealth });

            // 地址栏自适应宽度
            toolbar.Layout += (s, e) =>
            {
                int used = 16;
                foreach (ToolStripItem it in toolbar.Items)
                    if (it != addressBar) used += it.Width + 6;
                addressBar.Width = Math.Max(150, toolbar.Width - used);
            };
            Controls.Add(toolbar);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.SelectedIndexChanged += (s, e) => SyncAddressBar(CurrentTab());
            tabs.MouseDoubleClick += (s, e) =>
            {
                for (int i = 0; i < tabs.TabCount; i++)
                {
                    if (tabs.GetTabRect(i).Contains(e.Location))
                    {
                        CloseTabPage(tabs.TabPages[i]);
                        break;
                    }
                }
            };
            Controls.Add(tabs);

            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("就绪");
            statusStrip.Items.Add(statusLabel);
            Controls.Add(statusStrip);

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Shield;
            tray.Text = "StealthBrowser（双击还原）";
            tray.Visible = false;
            tray.DoubleClick += (s, e) => ShowFromTray();
            tray.BalloonTipTitle = "StealthBrowser";

            FormClosing += (s, e) =>
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                tray.Visible = false;
                tray.Dispose();
            };
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            stealthAffinity = BestAffinity();
            ApplyStealth();

            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_H);

            // 数据目录：Cookie、登录态、历史、localStorage 全存在这里，跟着程序走
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            try
            {
                env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 运行环境初始化失败：\n" + ex.Message +
                    "\n\nWin10/11 一般自带；Win7 需要手动安装 WebView2 Runtime：\n" +
                    "https://developer.microsoft.com/microsoft-edge/webview2/",
                    "StealthBrowser", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            OpenTab(HOME_URL);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                ToggleBossKey();
            base.WndProc(ref m);
        }

        // ---------------- 隐身 ----------------
        void ApplyStealth()
        {
            SetWindowDisplayAffinity(Handle, stealthOn ? stealthAffinity : WDA_NONE);
            string mode = stealthAffinity == WDA_EXCLUDEFROMCAPTURE ? "透明" : "黑块";
            btnStealth.Text = stealthOn ? "隐身:开·" + mode : "隐身:关";
            SetStatus(stealthOn ? "防截屏隐身已开启（" + mode + "模式）" : "防截屏隐身已关闭");
        }

        // ---------------- 老板键 ----------------
        void ToggleBossKey()
        {
            if (Visible)
            {
                Hide();
                tray.Visible = true;
                tray.ShowBalloonTip(1500, "StealthBrowser", "已隐藏到托盘，再按 Ctrl+Alt+H 恢复", ToolTipIcon.Info);
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
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ---------------- 标签页 ----------------
        BrowserTab CurrentTab()
        {
            if (tabs.SelectedTab == null) return null;
            return tabs.SelectedTab.Tag as BrowserTab;
        }

        public void OpenTab(string url)
        {
            if (env == null) return;
            var page = new TabPage("新标签页");
            var tab = new BrowserTab(this, page);
            page.Tag = tab;
            page.Controls.Add(tab);
            tab.Dock = DockStyle.Fill;
            tabs.TabPages.Add(page);
            tabs.SelectedTab = page;
            tab.Init(env, url);
        }

        public void CloseTab(BrowserTab tab)
        {
            if (tab != null) CloseTabPage(tab.Page);
        }

        void CloseTabPage(TabPage page)
        {
            if (page == null) return;
            var tab = page.Tag as BrowserTab;
            tabs.TabPages.Remove(page);
            if (tab != null)
            {
                try { tab.Web.Dispose(); } catch { }
                tab.Dispose();
            }
            page.Dispose();
            if (tabs.TabCount == 0) OpenTab(HOME_URL);
        }

        public void SyncTabTitle(BrowserTab tab)
        {
            if (tab == null || tab.IsDisposed) return;
            try
            {
                string title = tab.Web.CoreWebView2.DocumentTitle;
                if (string.IsNullOrEmpty(title)) title = "新标签页";
                if (title.Length > 20) title = title.Substring(0, 20) + "…";
                tab.Page.Text = title;
            }
            catch { }
        }

        public void SyncAddressBar(BrowserTab tab)
        {
            if (tab == null || tab.Page != tabs.SelectedTab) return;
            try
            {
                var src = tab.Web.Source;
                if (src != null) addressBar.Text = src.ToString();
            }
            catch { }
        }

        public void SetStatus(string s)
        {
            if (!IsDisposed) statusLabel.Text = s;
        }

        // ---------------- 地址栏 ----------------
        void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                NavigateAddressBar();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        void NavigateAddressBar()
        {
            var tab = CurrentTab();
            if (tab == null) return;
            string url = SmartUrl(addressBar.Text);
            tab.Navigate(url);
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
