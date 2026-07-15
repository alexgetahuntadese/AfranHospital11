@echo off
echo ========================================
echo Afran Hospital Queue System - Kiosk Setup
echo ========================================
echo.
echo This machine will run the Patient Registration Kiosk.
echo.
set /p SERVER_IP="Enter the Server Machine IP address (e.g., 192.168.1.10): "
echo.
echo Setting API address to: http://%SERVER_IP%:5000
set AFRAN_QUEUE_API=http://%SERVER_IP%:5000
echo.
echo Testing connection to server...
ping -n 1 %SERVER_IP% > nul
if %errorlevel% neq 0 (
    echo WARNING: Cannot reach server at %SERVER_IP%
    echo Please check:
    echo 1. Server machine is running
    echo 2. Both machines are on the same network
    echo 3. Firewall allows port 5000
    echo.
    echo Press any key to continue anyway...
    pause > nul
) else (
    echo Server is reachable.
)
echo.
echo Press any key to start the Kiosk...
pause > nul
cd /d "%~dp0AfranHospitalKiosk"
start-kiosk.bat
