@echo off
rem F24 + Windows UI Automation helper
setlocal
cd /d "%~dp0"
if exist "%~dp0WuwaTappoHelper.exe" (
    start "" "%~dp0WuwaTappoHelper.exe"
    exit /b
)
powershell.exe -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Launch-UiaHelper.ps1"
if errorlevel 1 pause
