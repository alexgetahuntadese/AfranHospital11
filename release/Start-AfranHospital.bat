@echo off
set "APP_DIR=%~dp0AfranHospital"
set "API_DIR=%~dp0QueueApi"
if /i "%PROCESSOR_ARCHITEW6432%"=="" if /i "%PROCESSOR_ARCHITECTURE%"=="x86" (
    set "APP_DIR=%~dp0AfranHospital-x86"
    set "API_DIR=%~dp0QueueApi-x86"
)
set "APP_EXE=%APP_DIR%\AfranHospitalKiosk.exe"
if not exist "%APP_EXE%" (
    echo HIWOT FANA INTERNAL MEDICINE SPECIALTY CLINIC launcher was not found:
    echo %APP_EXE%
    echo.
    echo This package supports Windows 10/11 64-bit and 32-bit PCs.
    pause
    exit /b 1
)
start "HIWOT FANA INTERNAL MEDICINE SPECIALTY CLINIC Launcher" /D "%APP_DIR%" "%APP_EXE%"
exit /b 0
