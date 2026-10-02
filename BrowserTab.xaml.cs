using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace StealthBrowser
{
    public partial class BrowserTab : UserControl
    {
        readonly MainWindow owner;
        MainWindow.TabInfo tabInfo;
        bool initialized = false;

        // F12 / Ctrl+W 的 JS 桥接
        const string KEY_JS = @"
window.addEventListener('keydown', function(e) {
    if (e.key === 'F12') {
        e.preventDefault();
        window.chrome.webview.postMessage('F12');
    }
    if (e.ctrlKey && (e.key === 'w' || e.key === 'W')) {
        e.preventDefault();
        window.chrome.webview.postMessage('CTRL_W');
    }
}, true);";

        public BrowserTab(MainWindow owner)
        {
            this.owner = owner;
            InitializeComponent();
        }

        public WebView2 WebControl => Web;

        public async void Init(CoreWebView2Environment env, string url)
        {
            if (initialized) return;
            initialized = true;
            try
            {
                await Web.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 初始化失败：\n" + ex.Message,
                    "StealthBrowser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var core = Web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            core.AddScriptToExecuteOnDocumentCreatedAsync(KEY_JS);
            core.WebMessageReceived += (s, e) =>
            {
                try
                {
                    string msg = e.TryGetWebMessageAsString();
                    if (msg == "F12") core.OpenDevToolsWindow();
                    else if (msg == "CTRL_W") owner.Dispatcher.Invoke(() => owner.CloseTab(tabInfo));
                }
                catch { }
            };

            core.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
                try
                {
                    string u = e.Uri;
                    if (string.IsNullOrEmpty(u)) return;
                    owner.Dispatcher.Invoke(() => owner.OpenTab(u));
                }
                catch { }
            };

            core.DownloadStarting += (s, e) =>
            {
                try
                {
                    string dlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
                    Directory.CreateDirectory(dlDir);
                    e.ResultFilePath = Path.Combine(dlDir,
                        Path.GetFileName(e.ResultFilePath) ?? "download");
                    e.Handled = true;
                }
                catch { }
            };
            core.DownloadStarting += (s, e) =>
            {
                try
                {
                    string name = Path.GetFileName(e.ResultFilePath);
                    owner.Dispatcher.Invoke(() => owner.SetStatus("正在下载：" + name));
                }
                catch { }
            };
            core.DownloadStateChanged += (s, e) =>
            {
                try
                {
                    if (e.State == CoreWebView2DownloadState.Completed)
                        owner.Dispatcher.Invoke(() => owner.SetStatus("下载完成：" + Path.GetFileName(e.ResultFilePath)));
                    else if (e.State == CoreWebView2DownloadState.Interrupted)
                        owner.Dispatcher.Invoke(() => owner.SetStatus("下载中断"));
                }
                catch { }
            };

            core.DocumentTitleChanged += (s, e) =>
                owner.Dispatcher.Invoke(() => owner.SyncTabTitle(tabInfo));
            Web.SourceChanged += (s, e) =>
                owner.Dispatcher.Invoke(() => owner.SyncAddressBar(tabInfo));

            if (!string.IsNullOrEmpty(url))
                Navigate(url);
        }

        public void SetTabInfo(MainWindow.TabInfo info) { tabInfo = info; }

        public void Navigate(string url)
        {
            try
            {
                if (Web.CoreWebView2 != null)
                    Web.CoreWebView2.Navigate(url);
                else
                    Web.Source = new Uri(url);
            }
            catch { }
        }

        public void GoBack()
        {
            try { if (Web.CoreWebView2 != null && Web.CoreWebView2.CanGoBack) Web.CoreWebView2.GoBack(); }
            catch { }
        }

        public void GoForward()
        {
            try { if (Web.CoreWebView2 != null && Web.CoreWebView2.CanGoForward) Web.CoreWebView2.GoForward(); }
            catch { }
        }

        public void Reload()
        {
            try { if (Web.CoreWebView2 != null) Web.CoreWebView2.Reload(); }
            catch { }
        }
    }
}
