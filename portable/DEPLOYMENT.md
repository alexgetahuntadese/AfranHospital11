# Afran Hospital Queue System - 4-Machine Deployment Guide

## Overview

This guide covers deploying the Afran Hospital Queue System across 4 machines:
1. **Server Machine** - Runs the backend API
2. **Kiosk Machine** - Patient registration interface
3. **Doctor Machine** - Doctor/reception station
4. **TV Machine** - Waiting room display

## Prerequisites

- All 4 machines must be on the same LAN network
- Windows 10/11 (64-bit) on all machines
- No .NET installation required (portable version)
- Network connectivity between all machines

## Step 1: Copy Portable Folder

Copy the entire `portable/` folder to all 4 machines (USB drive, network share, etc.).

## Step 2: Setup Server Machine

On the machine that will run the backend API:

1. Navigate to the `portable/` folder
2. Run: `setup-server.bat`
3. The script will:
   - Display the machine's IP address
   - Start the QueueApi server
   - Show the API URL (e.g., `http://192.168.1.10:5000`)

**Note this IP address** - you'll need it for the other machines.

## Step 3: Setup Client Machines

On each client machine (Kiosk, Doctor, TV):

1. Navigate to the `portable/` folder
2. Run the appropriate setup script:
   - **Kiosk Machine:** `setup-kiosk.bat`
   - **Doctor Machine:** `setup-doctor.bat`
   - **TV Machine:** `setup-tv.bat`
3. When prompted, enter the **Server Machine IP address** from Step 2
4. The script will:
   - Test network connectivity to the server
   - Configure the API address
   - Launch the appropriate application

## Network Configuration

### Firewall Settings

On the **Server Machine**, ensure port 5000 is allowed:

**Windows Firewall:**
```powershell
netsh advfirewall firewall add rule name="Afran Queue API" dir=in action=allow protocol=TCP localport=5000
```

### Static IP (Recommended)

For reliable operation, set a static IP on the server machine:
1. Open Network Settings
2. Change adapter options
3. Right-click your network adapter → Properties
4. IPv4 → Properties
5. Use static IP (e.g., 192.168.1.10)

## Startup Order

1. **Start Server Machine first** - Run `setup-server.bat`
2. **Wait 10 seconds** for API to fully start
3. **Start client machines** in any order:
   - Run `setup-kiosk.bat` on kiosk machine
   - Run `setup-doctor.bat` on doctor machine
   - Run `setup-tv.bat` on TV machine

## Troubleshooting

### Client cannot connect to server

1. **Check server is running:** Verify API server is running on server machine
2. **Check network:** Ping server IP from client machine
3. **Check firewall:** Ensure port 5000 is not blocked
4. **Check IP address:** Verify correct server IP is used

### Server IP changes

If the server machine gets a different IP address:
1. Restart `setup-server.bat` to get new IP
2. Re-run setup scripts on client machines with new IP

### Applications not starting

1. Verify portable folder copied completely
2. Check Windows version (requires 64-bit Windows 10/11)
3. Run as Administrator if needed

## Advanced: Server Manager UI

On the server machine, you can use the Server Manager UI instead of command-line setup:

```batch
cd portable\AfranHospitalKiosk
start-server.bat
```

This provides a graphical interface to:
- Start/Stop the API server
- Launch Kiosk, Doctor, and TV applications
- Monitor application status
- Close all applications at once

## Quick Reference

| Machine | Script | Purpose |
|--------|--------|---------|
| Server | `setup-server.bat` | Start API backend |
| Kiosk | `setup-kiosk.bat` | Patient registration |
| Doctor | `setup-doctor.bat` | Doctor station |
| TV | `setup-tv.bat` | Waiting room display |

## Support

For issues or questions, refer to the main README.md or project documentation.
