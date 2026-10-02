using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace StealthBrowser
{
    // 单个标签页：包一个 WebView2 控件
    public class BrowserTab : UserControl
    {
        public WebView2 Web { get; private set; }
        public TabPage Page { get; private set; }
        readonly MainForm owner;

        public BrowserTab(MainForm owner, TabPage page)
        {
            this.owner = owner;
            this.Page = page;
            Web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(Web);
        }

        public async void Init(CoreWebView2Environment env, string url)
        {
            try
            {
                await Web.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show("标签页初始化失败：" + ex.Message, "StealthBrowser",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var core = Web.CoreWebView2;

            core.DocumentTitleChanged += (s, e) => owner.SyncTabTitle(this);
            Web.SourceChanged += (s, e) => owner.SyncAddressBar(this);

            // 拦截 target=_blank：在新标签页打开，而不是弹 Edge 窗口
            core.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
                owner.OpenTab(e.Uri);
            };

            // 快捷键（F12 / Ctrl+W）：AcceleratorKeyPressed 只暴露在 Controller 上，
            // WinForms 控件拿不到，故用注入脚本监听按键、经 WebMessageReceived 转发回来
            core.WebMessageReceived += (s, e) =>
            {
                string msg;
                if (!e.TryGetWebMessageAsString(out msg)) return;
                if (msg == "wv2key:F12") core.OpenDevToolsWindow();
                else if (msg == "wv2key:CTRLW") owner.CloseTab(this);
            };
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                "addEventListener('keydown', function(e) {" +
                "  if (e.keyCode === 123) { chrome.webview.postMessage('wv2key:F12'); }" +
                "  else if (e.ctrlKey && e.keyCode === 87) { chrome.webview.postMessage('wv2key:CTRLW'); e.preventDefault(); }" +
                "}, true);");

            // 下载保存到程序目录下的 downloads 文件夹
            core.DownloadStarting += (s, e) =>
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
                Directory.CreateDirectory(dir);
                string name = Path.GetFileName(e.ResultFilePath);
                if (string.IsNullOrEmpty(name)) name = "download";
                e.ResultFilePath = Path.Combine(dir, name);
                owner.SetStatus("正在下载：" + name);
            };

            if (!string.IsNullOrEmpty(url))
                core.Navigate(url);
        }

        public void Navigate(string url)
        {
            if (Web.CoreWebView2 != null) Web.CoreWebView2.Navigate(url);
        }

        public void GoBack()
        {
            var c = Web.CoreWebView2;
            if (c != null && c.CanGoBack) c.GoBack();
        }

        public void GoForward()
        {
            var c = Web.CoreWebView2;
            if (c != null && c.CanGoForward) c.GoForward();
        }

        public void Reload()
        {
            if (Web.CoreWebView2 != null) Web.CoreWebView2.Reload();
        }
    }
}
