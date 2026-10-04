# HDRReset

一个通过 DDC/CI 刷新显示器 HDR 状态的 Windows 小工具。

本程序专门针对 **Lenovo Legion Pro 27UD-10 QD-OLED** 开发和测试。

---

# 中文

## 功能

HDRReset 通过显示器的 DDC/CI 接口向 **Lenovo Legion Pro 27UD-10 QD-OLED** 发送 HDR 指令，用于刷新显示器的 HDR 状态。

- 程序启动后立即刷新一次 HDR
- 每 30 分钟自动刷新一次 HDR
- 按数字键 `1` 可以立即刷新 HDR，并重新开始 30 分钟计时
- 支持系统托盘运行
- 托盘右键菜单可以执行「刷新HDR」
- 主界面提供「刷新HDR」按钮
- 单文件运行，无需安装 .NET Runtime

## 下载

前往 [Releases](../../releases) 下载最新版本。

下载发布页面中的 ZIP 压缩包即可直接使用。

## 使用方法

解压 ZIP 文件后运行：

`HDRReset.exe`

程序启动后会：

1. 立即刷新一次 HDR
2. 等待 30 分钟
3. 再次刷新 HDR
4. 之后每 30 分钟重复一次

也可以手动刷新：

- 点击主界面的「刷新HDR」
- 按数字键 `1`
- 右键点击系统托盘图标 → 「刷新HDR」

手动刷新后，30 分钟计时会重新开始。

## 适用设备

**Lenovo Legion Pro 27UD-10 QD-OLED**

本程序目前仅针对 **Lenovo Legion Pro 27UD-10 QD-OLED** 开发和测试。

程序使用该显示器特定的 DDC/CI 指令。其他显示器型号的 DDC/CI 实现和 VCP 指令可能不同，因此本程序不声称兼容其他型号。

## DDC/CI 指令

程序向显示器发送：

VCP Code: `0xEF`

Value: `0x09`

对应 **HDR Photo** 模式。

程序直接通过 DDC/CI 与物理显示器通信，而不是修改 Windows 的 HDR 设置。

## 系统要求

- Windows 10 / Windows 11
- x64 系统
- Lenovo Legion Pro 27UD-10 QD-OLED
- 显示器需要启用 DDC/CI

## 安装

本程序不需要传统意义上的安装。

解压 ZIP 后即可运行：

`HDRReset.exe`

如果希望程序随 Windows 自动启动，可以运行：

`Install.bat`

安装脚本会创建桌面快捷方式并设置 Windows 启动项。

## 注意事项

本程序使用的是 **Lenovo Legion Pro 27UD-10 QD-OLED 的特定 DDC/CI 指令**。

其他显示器型号可能使用不同的 VCP Code、Value 或其他控制方式，因此：

**请不要直接将本程序用于其他显示器，并假定其具有相同功能。**

如果你使用的不是 Lenovo Legion Pro 27UD-10 QD-OLED，本项目不保证能够正常工作。

---

# English

## Features

HDRReset is a small Windows utility developed specifically to refresh the HDR state of the **Lenovo Legion Pro 27UD-10 QD-OLED** through DDC/CI.

- Refreshes HDR once when the program starts
- Automatically refreshes HDR every 30 minutes
- Press `1` to refresh HDR immediately and restart the 30-minute timer
- Supports running in the system tray
- Right-click the tray icon to select `刷新HDR`
- Provides a `刷新HDR` button in the main window
- Single-file executable, no .NET Runtime installation required

## Download

Go to [Releases](../../releases) and download the latest version.

Download the ZIP package from the release page and extract it to use the program.

## Usage

Extract the ZIP file and run:

`HDRReset.exe`

The program will:

1. Refresh HDR immediately
2. Wait 30 minutes
3. Refresh HDR again
4. Continue repeating every 30 minutes

You can also manually refresh HDR by:

- Clicking `刷新HDR` in the main window
- Pressing the `1` key
- Right-clicking the system tray icon and selecting `刷新HDR`

A manual refresh restarts the 30-minute timer.

## Supported Device

**Lenovo Legion Pro 27UD-10 QD-OLED**

This utility was specifically developed and tested for the **Lenovo Legion Pro 27UD-10 QD-OLED**.

The program uses DDC/CI commands specific to this monitor. Other monitor models may use different DDC/CI implementations and VCP commands, so compatibility with other models is not claimed.

## DDC/CI Command

The program sends:

VCP Code: `0xEF`

Value: `0x09`

This corresponds to the monitor's **HDR Photo** mode.

The program communicates directly with the physical monitor through DDC/CI rather than changing the Windows HDR setting.

## Requirements

- Windows 10 / Windows 11
- x64 system
- Lenovo Legion Pro 27UD-10 QD-OLED
- DDC/CI enabled on the monitor

## Installation

No traditional installation is required.

Extract the ZIP package and run:

`HDRReset.exe`

If you want the program to start automatically with Windows, run:

`Install.bat`

The installation script creates a desktop shortcut and adds the program to Windows startup.

## Notes

This utility uses **DDC/CI commands specific to the Lenovo Legion Pro 27UD-10 QD-OLED**.

Other monitor models may use different VCP codes, values, or control mechanisms.

**Do not assume that this utility is compatible with other monitor models.**

If you are not using a Lenovo Legion Pro 27UD-10 QD-OLED, this project does not guarantee that the utility will work correctly.
