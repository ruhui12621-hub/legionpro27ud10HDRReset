@echo off
chcp 65001 >nul
setlocal

set "BASE=%~dp0"
set "EXE=%BASE%bin\Release\net8.0-windows\win-x64\publish\HDRReset.exe"
set "WORKDIR=%BASE%bin\Release\net8.0-windows\win-x64\publish"


REM ============================================================
REM 1. 创建开机启动快捷方式
REM ============================================================

powershell.exe -NoProfile -Command ^
"$startup = [Environment]::GetFolderPath('Startup'); ^
$shortcut = Join-Path $startup 'HDRReset.lnk'; ^
$ws = New-Object -ComObject WScript.Shell; ^
$sc = $ws.CreateShortcut($shortcut); ^
$sc.TargetPath = '%EXE%'; ^
$sc.WorkingDirectory = '%WORKDIR%'; ^
$sc.Description = 'HDRReset 开机自启动'; ^
$sc.Save()"


REM ============================================================
REM 2. 直接运行 HDRReset
REM ============================================================

start "" "%EXE%"

endlocal
exit /b 0