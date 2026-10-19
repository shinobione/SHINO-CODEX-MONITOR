@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0INSTALLER-V5.1.ps1"
if errorlevel 1 (echo. & echo Installation V5.1 non effectuee. Voir le diagnostic. & pause)
