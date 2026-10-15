@echo off
setlocal
echo Verification et creation du paquet Windows...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD-RELEASE-V4.5.ps1"
if errorlevel 1 (echo. & echo Erreur pendant les autotests ou le build. Aucune installation lancee. & pause & exit /b 1)
echo Verification terminee. Installation de la version compilee...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0dist\SHINO_CODEX_MONITOR_V4_5_WINDOWS\INSTALLER-V4.5.ps1"
if errorlevel 1 (echo. & echo Installation non validee. Ancienne version conservee/restaurable. & pause & exit /b 1)
echo SHINO CODEX MONITOR V4.5 lance.
