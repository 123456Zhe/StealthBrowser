# StealthBrowser

一个带**防截屏隐身**的开源小浏览器。基于 [SharpBrowser](https://github.com/sharpbrowser/SharpBrowser)
（MIT 许可证）的成熟浏览器底座改造，内核为 Chromium（CefSharp），界面为经典 WinForms：
原生标题栏可拖动、有最小化/最大化/关闭按钮，标签页可正常关闭。

## 功能

- **防截屏隐身**（默认开启）：截图、录屏、远程桌面/监控软件里，这个窗口会被跳过——
  Win10 2004+ 上截屏里直接透出后面的桌面（透明）；Win10 1903 及更早版本上系统 API 不支持透明，
  会降级为黑块（这是 Windows 的限制，不是浏览器的锅）。你自己屏幕上照常显示。
  工具栏有 🛡 开关可随时切换。隐身调的是浏览器自己进程的官方 API，无跨进程注入，
  Windows Defender 无感。
- **老板键**：`Ctrl+Alt+H` 一键藏到托盘，再按恢复（托盘双击也可恢复）。
- **成熟浏览器功能**（继承自 SharpBrowser）：多标签页、地址栏、书签、历史记录、
  下载管理、会话恢复（重启后恢复上次打开的标签页）、右键菜单、
  快捷键（`Ctrl+T` 新标签页、`Ctrl+W` 关闭、`F12` 开发者工具等）。
- **Chromium 内核（内嵌）**：发布包自带 CefSharp/CEF（构建时用最新稳定版），
  不依赖系统组件，老机器、U 盘便携即插即用；.NET 8 self-contained 发布，
  目标电脑不需要装任何 .NET。
- **数据持久化**：Cookie、登录态、历史记录、localStorage 全存在程序目录下的 `data` 文件夹，
  下载存到 `downloads` 文件夹；整个文件夹拷走即迁移，公共电脑上也不怕。

## 下载

去 [Releases](https://github.com/123456Zhe/StealthBrowser/releases) 下 `StealthBrowser-win-x64.zip`
（约 200MB，含内嵌 Chromium 内核），解压即用，免安装。整个文件夹可直接放 U 盘里带走。

## 运行环境

- Windows 10/11 x64（内核最低支持 Win10 1903；隐身透明效果需要 Win10 2004+）
- 不需要装 .NET，也不需要系统装 WebView2

## 自己编译

```powershell
# 需要 .NET 8 SDK
.\build.ps1
```

或直接 push 到 GitHub，Action 会自动编译（打 `v*` tag 自动发 Release）。

## 原理

`SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`：DWM 在合成层把该窗口排除在一切
软件捕获之外。因为是浏览器自己调自己窗口的 API，不涉及注入其他进程，所以不会触发
Defender / 杀软。

## 局限

- 只能防**软件**捕获，防不住有人站在身后看或手机拍照。
- Win10 1903 及更早版本上隐身效果为黑块（系统 API 限制，透明需要 Win10 2004+）。

## 致谢与 License

- 浏览器底座：[SharpBrowser](https://github.com/sharpbrowser/SharpBrowser)，MIT 许可证
  （许可证全文见 `LICENSE.sharpbrowser`）。
- 本项目：MIT
