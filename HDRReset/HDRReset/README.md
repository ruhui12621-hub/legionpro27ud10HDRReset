# HDRReset

基于 .NET 8 / WPF 的 Legion Pro 27UD-10 HDR 自动刷新工具。当前源码使用单 EXE 双进程架构：常驻托盘宿主负责刷新和内存历史，WPF 界面仅在点击托盘图标时作为独立子进程启动。

## 构建与安装

1. 在 Windows 上安装 .NET 8 SDK。
2. 在本目录运行 `publish.bat`，生成自包含的单文件 `bin\Release\net8.0-windows\win-x64\publish\HDRReset.exe`。
3. 发布成功后，返回上一级目录，运行 `Install.bat`。安装脚本会创建开机启动快捷方式并启动托盘宿主。

批处理文件只包含 ASCII 命令文本，并使用带引号的相对路径，支持将整个项目解压到包含空格或中文的目录中。

详细运行机制见 [README-TWO-PROCESS.md](README-TWO-PROCESS.md)。
