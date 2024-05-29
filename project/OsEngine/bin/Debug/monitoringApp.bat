@echo off
chcp 866 >NUL
setlocal

set "APP_DIR=C:\Users\User\Desktop\Debug"
set "TARGET_EXE=OsEngine.exe"
set "ARGUMENTS=-robotslight"

:check_running
tasklist /FI "IMAGENAME eq %TARGET_EXE%" 2>NUL | findstr /I /C:"%TARGET_EXE%" >NUL

if errorlevel 1 (
    cd "%APP_DIR%"
    start "" ".\%TARGET_EXE%" %ARGUMENTS%
)

timeout /t 10 /nobreak >NUL
goto check_running