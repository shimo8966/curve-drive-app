REM CurveDrive v3.0 - APPEND ONLY - NO DELETE
REM #jww
REM #cd
REM #hf
REM #h0
REM #0 Select the PLAN START point (R)Read
REM #e
@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "TARGET=%~dp0JWC_TEMP.TXT"
set "LOGFILE=%~dp0Batch.log"
>> "%LOGFILE%" echo [%DATE% %TIME%] CurveDrive v3.0 import
>> "%LOGFILE%" echo Batch: "%~f0"
>> "%LOGFILE%" echo Current folder: "%CD%"
>> "%LOGFILE%" echo Target: "%TARGET%"
if not exist "%TARGET%" goto notjww
copy /y "%TARGET%" "%~dp0Input.txt" >nul
if errorlevel 1 goto capturefailed
REM Clear the JWW unexecuted marker BEFORE running the importer.
REM Never leave stale geometry on any failure path.
> "%TARGET%" echo heCurveDrive: import did not finish. See Batch.log and Import.log.
if errorlevel 1 goto writefailed
if not exist "%~dp0Bridge.exe" goto noexe
"%~dp0Bridge.exe" --import "%TARGET%" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto failed
>> "%LOGFILE%" echo RESULT: ISSUED ONCE. CAD drawing is NOT verified.
exit /b 0
:notjww
>> "%LOGFILE%" echo RESULT: JWC_TEMP missing. Run this BAT from Jw_cad external transform.
exit /b 1
:capturefailed
> "%TARGET%" echo heCurveDrive: cannot capture input. See Batch.log.
>> "%LOGFILE%" echo RESULT: Cannot capture input.
exit /b 1
:noexe
> "%TARGET%" echo heCurveDrive: Bridge.exe missing. Run Open.cmd first.
>> "%LOGFILE%" echo RESULT: Bridge.exe missing.
exit /b 1
:failed
>> "%LOGFILE%" echo RESULT: Import failed. See Import.log and Jw_cad error message.
exit /b 1
:writefailed
>> "%LOGFILE%" echo RESULT: Cannot write JWC_TEMP. Check folder permissions.
exit /b 1
