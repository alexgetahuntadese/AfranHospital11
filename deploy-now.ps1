param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "release")
)

$ErrorActionPreference = "Stop"
$desktopOutput = Join-Path $OutputDirectory "AfranHospital"
$desktopOutputX86 = Join-Path $OutputDirectory "AfranHospital-x86"
$apiOutput = Join-Path $OutputDirectory "QueueApi"
$apiOutputX86 = Join-Path $OutputDirectory "QueueApi-x86"

New-Item -ItemType Directory -Force -Path $desktopOutput | Out-Null
New-Item -ItemType Directory -Force -Path $desktopOutputX86 | Out-Null
New-Item -ItemType Directory -Force -Path $apiOutput | Out-Null
New-Item -ItemType Directory -Force -Path $apiOutputX86 | Out-Null

Write-Host "Publishing Afran Hospital desktop launcher and modules..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot "AfranHospitalKiosk.csproj") -c Release -r win-x64 --self-contained true --no-restore -o $desktopOutput
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed with exit code $LASTEXITCODE." }

Write-Host "Publishing 32-bit desktop launcher..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot "AfranHospitalKiosk.csproj") -c Release -r win-x86 --self-contained true --no-restore -o $desktopOutputX86
if ($LASTEXITCODE -ne 0) { throw "32-bit desktop publish failed with exit code $LASTEXITCODE." }

Write-Host "Publishing Queue API..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot "QueueApi\QueueApi.csproj") -c Release -r win-x64 --self-contained true --no-restore -o $apiOutput
if ($LASTEXITCODE -ne 0) { throw "Queue API publish failed with exit code $LASTEXITCODE." }

Write-Host "Publishing 32-bit Queue API..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot "QueueApi\QueueApi.csproj") -c Release -r win-x86 --self-contained true --no-restore -o $apiOutputX86
if ($LASTEXITCODE -ne 0) { throw "32-bit Queue API publish failed with exit code $LASTEXITCODE." }

$startScript = @'
@echo off
set "APP_DIR=%~dp0AfranHospital"
set "API_DIR=%~dp0QueueApi"
if /i "%PROCESSOR_ARCHITEW6432%"=="" if /i "%PROCESSOR_ARCHITECTURE%"=="x86" (
    set "APP_DIR=%~dp0AfranHospital-x86"
    set "API_DIR=%~dp0QueueApi-x86"
)
set "APP_EXE=%APP_DIR%\AfranHospitalKiosk.exe"
if not exist "%APP_EXE%" (
    echo Afran Hospital launcher was not found:
    echo %APP_EXE%
    echo.
    echo This package supports Windows 10/11 64-bit and 32-bit PCs.
    pause
    exit /b 1
)
start "Afran Hospital Launcher" /D "%APP_DIR%" "%APP_EXE%"
exit /b 0
'@
Set-Content -LiteralPath (Join-Path $OutputDirectory "Start-AfranHospital.bat") -Value $startScript -Encoding ASCII

$readme = @'
AFRAN HOSPITAL QUEUE SYSTEM

Start-AfranHospital.bat is the single entry point. It opens the launcher UI,
where you can start Queue API, Kiosk, Doctor Station, and Waiting Room TV.

Deployment layout:
  AfranHospital\AfranHospitalKiosk.exe    64-bit desktop launcher and modules
  AfranHospital-x86\AfranHospitalKiosk.exe 32-bit desktop launcher and modules
  QueueApi\QueueApi.exe                    64-bit LAN queue API
  QueueApi-x86\QueueApi.exe                32-bit LAN queue API

For separate computers, copy the complete release folder to each computer.
On client computers, enter the server URL in the launcher, for example:
  http://192.168.1.10:5000

The server computer should run Queue API from the launcher first. Allow TCP
port 5000 through Windows Firewall on the server computer.
'@
Set-Content -LiteralPath (Join-Path $OutputDirectory "README.txt") -Value $readme -Encoding UTF8

Write-Host "Deployment ready: $OutputDirectory" -ForegroundColor Green
Write-Host "Start with: $OutputDirectory\Start-AfranHospital.bat" -ForegroundColor Green
