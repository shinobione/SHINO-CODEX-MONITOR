@echo off
setlocal
powershell.exe -NoLogo -NoProfile -File "%~dp0BUILD-PREVIEW-V5.1.ps1"
if errorlevel 1 (echo. & echo Preview V5.1 : build ou tests echoues. & pause)
