# HDRReset

一个专门用于解决 **Lenovo Legion Pro 27UD-10 QD-OLED** 在使用过程中出现画面亮度异常变暗问题的 Windows 小工具。

本程序通过 DDC/CI 向显示器重新发送 HDR Photo 指令，从而刷新显示器的 HDR 状态。

---

# 中文

## 项目简介

部分 **Lenovo Legion Pro 27UD-10 QD-OLED** 用户可能会遇到这样的问题：

显示器在 HDR 模式下正常使用一段时间后，画面会出现明显的亮度下降，亮度可能降低到接近 SDR 的水平。

本程序就是针对这一问题开发的。

HDRReset 不修改 Windows 的 HDR 设置，而是直接通过显示器的 DDC/CI 接口向显示器发送 HDR Photo 指令，以刷新显示器当前的 HDR 状态。

目前程序主要用于 **Lenovo Legion Pro 27UD-10 QD-OLED**。

## 功能

* 程序启动后立即刷新一次 HDR
* 每 30 分钟自动刷新一次 HDR
* 按数字键 `1` 可以立即刷新 HDR，并重新开始 30 分钟计时
* 程序默认在系统托盘后台运行
* 左键点击托盘图标可以打开主窗口
* 托盘右键菜单可以执行「刷新HDR」
* 主界面提供「刷新HDR」按钮
* 单文件运行，无需安装 .NET Runtime

## 下载

前往 [Releases](../../releases) 下载最新版本。

下载发布页面中的 ZIP 压缩包并解压。

## 安装与使用

**解压 ZIP 压缩包后，只需要运行：**

`Install.bat`

无需手动运行 `HDRReset.exe`。

运行 `Install.bat` 后，安装脚本会自动：

1. 创建程序的开机启动项
2. 立即运行 HDRReset

程序启动后会**自动最小化到系统托盘，不会显示主窗口**。

正常运行时，HDRReset 会在后台自动工作：

* 启动后立即执行一次 HDR 刷新
* 每 30 分钟自动刷新一次 HDR
* 按数字键 `1` 可以立即刷新 HDR，并重新开始 30 分钟计时

如果需要打开主窗口：

**左键点击系统托盘中的 HDRReset 图标即可。**

也可以通过托盘图标的右键菜单执行「刷新HDR」。

### 重要提示

**安装后，请不要移动或删除解压出来的 HDRReset 文件夹。**

程序的开机启动配置指向该文件夹中的程序文件，因此需要保持该文件夹及其内部文件的位置不变。

如果需要卸载 HDRReset：

1. 删除系统中的 HDRReset 开机启动项（如果安装脚本提供了对应的卸载处理）
2. 退出 HDRReset
3. **直接删除整个 HDRReset 文件夹即可**

## 系统托盘

HDRReset 安装并运行后默认不会显示主窗口，而是在系统托盘后台运行。

可以通过以下方式操作程序：

* **左键点击托盘图标** → 打开主窗口
* **右键点击托盘图标** → 打开托盘菜单
* **托盘菜单 → 刷新HDR** → 立即执行 HDR 刷新

## 适用设备

**Lenovo Legion Pro 27UD-10 QD-OLED**

本程序专门针对 **Lenovo Legion Pro 27UD-10 QD-OLED** 开发和测试。

程序使用该显示器特定的 DDC/CI 指令。

其他显示器型号的 DDC/CI 实现和 VCP 指令可能完全不同，因此本程序不声称兼容其他型号。

## DDC/CI 指令

程序向显示器发送：

VCP Code: `0xEF`

Value: `0x09`

对应显示器的 **HDR Photo** 模式。

程序直接通过 DDC/CI 与物理显示器通信，而不是修改 Windows 的 HDR 设置。

## 系统要求

* Windows 10 / Windows 11
* x64 系统
* Lenovo Legion Pro 27UD-10 QD-OLED
* 显示器启用 DDC/CI

## 注意事项

本程序的主要用途是解决 **Lenovo Legion Pro 27UD-10 QD-OLED 在使用过程中出现的异常变暗问题**。

本程序使用的是该显示器实际测试过的 DDC/CI 指令：

`VCP 0xEF / Value 0x09`

其他显示器型号可能使用不同的 VCP Code、Value 或其他控制方式。

**请不要假定本程序可以用于其他显示器。**

如果你使用的不是 Lenovo Legion Pro 27UD-10 QD-OLED，本项目不保证程序能够正常工作。

---

# English

## Introduction

HDRReset is a small Windows utility specifically developed to address an issue where the **Lenovo Legion Pro 27UD-10 QD-OLED** may become significantly dimmer during use.

When the monitor is running in HDR mode, the screen may become noticeably darker after being used for a period of time, with brightness dropping to a level closer to SDR.

This utility was developed specifically to address this behavior.

Instead of changing the Windows HDR settings, HDRReset communicates directly with the monitor through DDC/CI and sends an HDR Photo command to refresh the monitor's HDR state.

The program is primarily intended for the **Lenovo Legion Pro 27UD-10 QD-OLED**.

## Features

* Refreshes HDR once when the program starts
* Automatically refreshes HDR every 30 minutes
* Press `1` to refresh HDR immediately and restart the 30-minute timer
* Runs in the system tray by default
* Left-click the tray icon to open the main window
* Right-click the tray icon to access the `Refresh HDR` option
* Provides a `Refresh HDR` button in the main window
* Single-file executable, no .NET Runtime installation required

## Download

Go to [Releases](../../releases) and download the latest version.

Download and extract the ZIP package.

## Installation and Usage

**After extracting the ZIP package, simply run:**

`Install.bat`

There is no need to run `HDRReset.exe` manually.

When `Install.bat` is executed, it will automatically:

1. Create a startup entry for HDRReset
2. Launch HDRReset immediately

After launching, HDRReset will **automatically minimize to the system tray without displaying the main window**.

Once running in the background, HDRReset will:

* Perform an HDR refresh immediately after startup
* Automatically refresh HDR every 30 minutes
* Refresh HDR immediately and restart the 30-minute timer when `1` is pressed

To open the main window:

**Left-click the HDRReset icon in the system tray.**

You can also right-click the tray icon to access the `Refresh HDR` option.

### Important

**After installation, do not move or delete the extracted HDRReset folder.**

The Windows startup configuration points to the program files inside this folder, so the folder and its contents must remain in their original location.

To uninstall HDRReset:

1. Remove the HDRReset startup entry from Windows, if applicable
2. Exit HDRReset
3. **Simply delete the entire HDRReset folder**

## System Tray

After installation, HDRReset runs in the background in the system tray instead of displaying the main window.

You can control the program as follows:

* **Left-click the tray icon** → Open the main window
* **Right-click the tray icon** → Open the tray menu
* **Tray menu → Refresh HDR** → Immediately refresh HDR

## Supported Device

**Lenovo Legion Pro 27UD-10 QD-OLED**

This utility was specifically developed and tested for the **Lenovo Legion Pro 27UD-10 QD-OLED**.

The program uses DDC/CI commands specific to this monitor.

Other monitor models may use completely different DDC/CI implementations and VCP commands, so compatibility with other models is not claimed.

## DDC/CI Command

The program sends:

VCP Code: `0xEF`

Value: `0x09`

This corresponds to the monitor's **HDR Photo** mode.

The program communicates directly with the physical monitor through DDC/CI rather than changing the Windows HDR setting.

## Requirements

* Windows 10 / Windows 11
* x64 system
* Lenovo Legion Pro 27UD-10 QD-OLED
* DDC/CI enabled on the monitor

## Notes

The primary purpose of this utility is to address the **brightness reduction issue experienced by the Lenovo Legion Pro 27UD-10 QD-OLED during use**.

The program uses the DDC/CI command that has been specifically tested with this monitor:

`VCP 0xEF / Value 0x09`

Other monitor models may use different VCP codes, values, or control mechanisms.

**Do not assume that this utility is compatible with other monitor models.**

If you are not using a Lenovo Legion Pro 27UD-10 QD-OLED, this project does not guarantee that the utility will work correctly.
