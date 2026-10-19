@echo off
setlocal
powershell.exe -NoLogo -NoProfile -File "%~dp0INSTALLER-V5.2.ps1"
if errorlevel 1 (echo. & echo Codex Periscope installation did not complete. & pause)
