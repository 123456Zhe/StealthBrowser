# StealthBrowser

一个带**防截屏隐身**的开源小浏览器。基于 WebView2（和 Chrome 同一 Chromium 内核，evergreen 永远最新），
隐身功能做在浏览器自己进程里——直接调 Windows 官方 API，无跨进程注入，Windows Defender 无感。

## 功能

- **防截屏隐身**（默认开启）：截图、录屏、远程桌面/监控软件里，这个窗口会被跳过——
  Win10 2004+ 上截屏里直接透出后面的桌面（透明），Win7/8 上降级为黑块；你自己屏幕上照常显示。
  工具栏可随时开关。
- **Chromium 内核**：WebView2 evergreen，永远是最新版 Chromium，不用操心升级。
- **数据持久化**：Cookie、登录态、历史记录、localStorage 全存在程序目录下的 `data` 文件夹，
  整个文件夹拷走即迁移，公共电脑上也不怕。
- **老板键**：`Ctrl+Alt+H` 一键藏到托盘，再按恢复。
- 标签页：`＋标签页`新建，双击标签页标题 / `Ctrl+W` 关闭；`target=_blank` 链接在新标签页打开。
- `F12` 开发者工具；下载自动存到 `downloads` 文件夹。

## 下载

去 [Releases](https://github.com/123456Zhe/StealthBrowser/releases) 下 `StealthBrowser-win-x64.zip`，
解压即用，免安装。

## 运行环境

- WebView2 Runtime：Win10 / Win11 自带；Win7 需要手动装一次：
  https://developer.microsoft.com/microsoft-edge/webview2/
- 需要 .NET Framework 4.7.2+（Win10 自带；Win7 需安装）

## 自己编译

```powershell
# 需要 .NET SDK
.\build.ps1
```

或直接 push 到 GitHub，Action 会自动编译（打 `v*` tag 自动发 Release）。

## 原理

`SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`：DWM 在合成层把该窗口排除在一切
软件捕获之外。因为是浏览器自己调自己窗口的 API，不涉及注入其他进程，所以不会触发
Defender / 杀软——这也是它和"外部工具隐藏别的窗口"方案的本质区别。

## 局限

- 只能防**软件**捕获，防不住有人站在身后看或手机拍照。
- Win7/8 上隐身效果为黑块（系统 API 限制），Win10 2004+ 才是透明透出。
- 目前只有 64 位构建。

## License

MIT
