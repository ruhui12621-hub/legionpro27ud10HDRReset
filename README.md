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

- 程序启动后立即刷新一次 HDR
- 每 30 分钟自动刷新一次 HDR
- 按数字键 `1` 可以立即刷新 HDR，并重新开始 30 分钟计时
- 支持系统托盘运行
- 托盘右键菜单可以执行「刷新HDR」
- 主界面提供「刷新HDR」按钮
- 单文件运行，无需安装 .NET Runtime

## 下载

前往 [Releases](../../releases) 下载最新版本。

下载发布页面中的 ZIP 压缩包并解压即可。

## 使用方法

解压 ZIP 文件后，运行：

`HDRReset.exe`

程序启动后会立即执行一次 HDR 刷新，然后每 30 分钟自动刷新一次。

也可以通过以下方式手动刷新：

- 点击主界面的「刷新HDR」
- 按数字键 `1`
- 右键点击系统托盘图标 → 「刷新HDR」

手动刷新后，30 分钟计时会重新开始。

## 安装

本程序不需要传统意义上的安装程序。

直接运行：

`Install.bat`

即可完成程序的安装和自动启动配置。

完成后程序会按照安装脚本中的设置运行，无需手动配置。

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

- Windows 10 / Windows 11
- x64 系统
- Lenovo Legion Pro 27UD-10 QD-OLED
- 显示器启用 DDC/CI

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

- Refreshes HDR once when the program starts
- Automatically refreshes HDR every 30 minutes
- Press `1` to refresh HDR immediately and restart the 30-minute timer
- Supports running in the system tray
- Right-click the tray icon to select `刷新HDR`
- Provides a `刷新HDR` button in the main window
- Single-file executable, no .NET Runtime installation required

## Download

Go to [Releases](../../releases) and download the latest version.

Download and extract the ZIP package.

## Usage

Run:

`HDRReset.exe`

The program immediately performs an HDR refresh when started, then automatically refreshes HDR every 30 minutes.

You can also manually refresh HDR by:

- Clicking `刷新HDR` in the main window
- Pressing the `1` key
- Right-clicking the system tray icon and selecting `刷新HDR`

A manual refresh restarts the 30-minute timer.

## Installation

This program does not require a traditional installer.

Simply run:

`Install.bat`

The batch file performs the required installation and startup configuration automatically.

No additional manual configuration is required.

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

- Windows 10 / Windows 11
- x64 system
- Lenovo Legion Pro 27UD-10 QD-OLED
- DDC/CI enabled on the monitor

## Notes

The primary purpose of this utility is to address the **brightness reduction issue experienced by the Lenovo Legion Pro 27UD-10 QD-OLED during use**.

The program uses the DDC/CI command that has been specifically tested with this monitor:

`VCP 0xEF / Value 0x09`

Other monitor models may use different VCP codes, values, or control mechanisms.

**Do not assume that this utility is compatible with other monitor models.**

If you are not using a Lenovo Legion Pro 27UD-10 QD-OLED, this project does not guarantee that the utility will work correctly.
