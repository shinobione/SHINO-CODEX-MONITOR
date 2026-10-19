@echo off
setlocal
powershell.exe -NoLogo -NoProfile -File "%~dp0INSTALL-PERISCOPE-V5.2.ps1"
if errorlevel 1 (echo. & echo Codex Periscope installation failed. See the error above. & pause)
