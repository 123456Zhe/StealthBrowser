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

            core.AcceleratorKeyPressed += (s, e) =>
            {
                if (e.VirtualKey == 123) // F12 → 开发者工具
                {
                    e.Handled = true;
                    core.OpenDevToolsWindow();
                }
                else if (e.VirtualKey == 87 && Control.ModifierKeys == Keys.Control) // Ctrl+W → 关标签
                {
                    e.Handled = true;
                    owner.CloseTab(this);
                }
            };

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
