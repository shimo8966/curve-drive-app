@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
if not exist "%~dp0Bridge.exe" goto launch
"%~dp0Bridge.exe" --ready
if errorlevel 3 goto launch
if errorlevel 2 goto other
if errorlevel 1 goto launch
start "" "http://127.0.0.1:18718/"
exit /b 0
:launch
start "CurveDrive v2.9a" "%ComSpec%" /d /c ""%~dp0Start.cmd""
exit /b 0
:other
echo Another CurveDrive is running. Close its bridge window before switching folders.
pause
exit /b 1
