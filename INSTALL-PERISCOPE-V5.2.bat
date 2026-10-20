@echo off
setlocal
rem ExecutionPolicy Bypass applies only to this PowerShell process.
rem No persistent Windows policy, registry or Defender setting is changed.
set "installer=%~dp0INSTALL-PERISCOPE-V5.2.ps1"
if not exist "%installer%" (
  echo ERROR: INSTALL-PERISCOPE-V5.2.ps1 was not found in this folder.
  exit /b 1
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%installer%" %*
set "result=%ERRORLEVEL%"
if not "%result%"=="0" (
  echo.
  echo Codex Periscope installation failed. See the error above.
  pause
)
exit /b %result%
