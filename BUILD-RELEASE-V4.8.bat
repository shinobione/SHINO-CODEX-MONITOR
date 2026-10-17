@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD-RELEASE-V4.8.ps1"
if errorlevel 1 (echo. & echo Build echoue. Voir erreur ci-dessus. & pause)
