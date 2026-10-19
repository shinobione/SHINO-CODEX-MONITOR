@echo off
setlocal
powershell.exe -NoLogo -NoProfile -File "%~dp0BUILD-PREVIEW-V5.2.ps1"
if errorlevel 1 (echo. & echo Codex Periscope preview build failed. & pause)
