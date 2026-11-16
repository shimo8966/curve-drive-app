@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
title CurveDrive Bridge v3.1
set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto nocsc
echo Compiling CurveDrive v3.1 using Windows .NET Framework...
"%CSC%" /nologo /noconfig /codepage:65001 /langversion:5 /target:exe /out:"%~dp0Bridge.new.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "%~dp0Bridge.cs" "%~dp0AutoBridge.cs" "%~dp0CadContext.cs" "%~dp0CadNative.cs"
if errorlevel 1 goto failed
move /y "%~dp0Bridge.new.exe" "%~dp0Bridge.exe" >nul
if errorlevel 1 goto failed
echo Keep this window open. Close OLD bridge windows before starting.
"%~dp0Bridge.exe" --open
echo Bridge stopped. Read any error above.
pause
exit /b 0
:nocsc
echo ERROR: .NET Framework 4.x C# compiler not found.
echo Contact your PC administrator. This program does not alter PC settings.
pause
exit /b 1
:failed
echo ERROR: Build failed or Bridge.exe is still running.
echo Close OLD bridge windows and start again. Keep any compiler error text.
pause
exit /b 1
