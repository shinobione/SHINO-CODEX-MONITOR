@echo off
setlocal
powershell.exe -NoLogo -NoProfile -File "%~dp0BUILD-RELEASE-V5.1.ps1"
if errorlevel 1 (echo. & echo Build V5.1 echoue. Voir erreur ci-dessus. & pause)
