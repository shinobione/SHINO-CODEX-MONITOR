@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0INSTALLER-V4.6.ps1"
if errorlevel 1 (echo. & echo Installation impossible. La version precedente reste disponible. & pause)
