@echo off
echo ========================================
echo Afran Hospital Queue System - Server Setup
echo ========================================
echo.
echo This machine will run the QueueApi backend.
echo.
echo Step 1: Get this machine's IP address
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /i "IPv4"') do (
    set SERVER_IP=%%a
    goto :found_ip
)
:found_ip
set SERVER_IP=%SERVER_IP: =%
echo Server IP: %SERVER_IP%
echo.
echo Step 2: Start the API server
echo The API will run on http://%SERVER_IP%:5000
echo.
echo Press any key to start the API server...
pause > nul
cd /d "%~dp0QueueApi"
start-api.bat
echo.
echo API Server started on http://%SERVER_IP%:5000
echo.
echo IMPORTANT: Use this IP address (%SERVER_IP%) on client machines
echo.
echo Press any key to exit...
pause > nul
