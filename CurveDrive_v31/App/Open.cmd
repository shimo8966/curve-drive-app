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
start "CurveDrive v3.1" "%ComSpec%" /d /c ""%~dp0Start.cmd""
exit /b 0
:other
start "" "%~dp0Help.html"
exit /b 1
