# HDRReset — Fix Lenovo Legion Pro 27UD-10 HDR Brightness Dimming

A small Windows utility for the **Lenovo Legion Pro 27UD-10 QD-OLED**.

Some users have reported that HDR brightness drops significantly after the monitor has been running for a while. HDRReset is a workaround for this issue.

## How it works

HDRReset uses **DDC/CI** to resend the monitor's **HDR Photo** mode command periodically.

It does **not** toggle Windows HDR or change the monitor's brightness setting.

* Refreshes HDR once on startup
* Automatically refreshes every 30 minutes
* Runs in the system tray
* Press `1` to refresh immediately

## Download

**[Download HDRReset.zip](https://github.com/ruhui12621-hub/legionpro27ud10HDRReset/releases/latest/download/HDRReset.zip)**

Extract the ZIP and run `Install.bat`.

The installer creates a Windows startup entry and launches HDRReset.

## Compatibility

**Tested on:**

* Lenovo Legion Pro 27UD-10 QD-OLED
* Windows 10 / Windows 11 x64

The workaround uses:

`DDC/CI VCP 0xEF / 0x09` — HDR Photo

Other monitors may use different DDC/CI commands. Compatibility with other models is **not guaranteed**.

## Disclaimer

This is a **workaround**, not an official Lenovo fix.
