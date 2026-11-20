@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "PROJECT_FILE=%PROJECT_DIR%HDRReset.csproj"
set "OUTPUT_DIR=%PROJECT_DIR%bin\Release\net8.0-windows\win-x64\publish"

if not exist "%PROJECT_FILE%" (
    echo ERROR: HDRReset.csproj was not found.
    echo Expected: "%PROJECT_FILE%"
    pause
    exit /b 1
)

if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

pushd "%PROJECT_DIR%"
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -o "%OUTPUT_DIR%" /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false /p:DebugType=None /p:DebugSymbols=false
set "RESULT=%ERRORLEVEL%"
popd

if not "%RESULT%"=="0" (
    echo.
    echo Publish failed. Check the .NET 8 SDK installation and build output above.
    pause
    exit /b %RESULT%
)

if not exist "%OUTPUT_DIR%\HDRReset.exe" (
    echo ERROR: Publish completed but HDRReset.exe was not found.
    echo Expected: "%OUTPUT_DIR%\HDRReset.exe"
    pause
    exit /b 1
)

echo.
echo Publish completed successfully.
echo Output: "%OUTPUT_DIR%\HDRReset.exe"
pause
exit /b 0
