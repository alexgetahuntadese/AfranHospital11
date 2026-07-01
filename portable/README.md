# Afran Hospital Queue System - Portable Deployment

This is a self-contained portable version of the Afran Hospital Queue System that requires no installation or .NET runtime dependencies.

## Directory Structure

```
portable/
├── AfranHospitalKiosk/          # WPF Desktop Application
│   ├── AfranHospitalKiosk.exe   # Main executable
│   ├── Assets/                  # Logos, videos, audio files
│   ├── start-kiosk.bat          # Launch kiosk mode
│   ├── start-doctor.bat         # Launch doctor station
│   ├── start-tv.bat             # Launch TV display
│   └── start-server.bat        # Launch server manager
└── QueueApi/                    # Backend API Server
    ├── QueueApi.exe             # API executable
    ├── SQLite.db                # Database (created on first run)
    └── start-api.bat            # Launch API server
```

## Quick Start

### 1. Start the API Server
Navigate to the `QueueApi` folder and run:
```batch
start-api.bat
```
The API will start on `http://0.0.0.0:5000`

### 2. Launch Applications

**Server Manager (Recommended):**
Navigate to `AfranHospitalKiosk` folder and run:
```batch
start-server.bat
```
This opens a control panel to launch and manage all applications.

**Individual Modes:**
- **Kiosk (Patient Registration):** `start-kiosk.bat`
- **Doctor Station:** `start-doctor.bat`  
- **TV Display:** `start-tv.bat`

## Network Configuration

For LAN deployment, set the API address before launching applications:

```batch
set AFRAN_QUEUE_API=http://192.168.1.10:5000
start-kiosk.bat
```

Replace `192.168.1.10` with your server's actual IP address.

## Deployment Architecture

**Recommended Setup:**
- **Server PC:** Run `QueueApi.exe` and optionally `start-server.bat`
- **Kiosk PC:** Run `start-kiosk.bat` with API address configured
- **Doctor PC:** Run `start-doctor.bat` with API address configured  
- **TV PC:** Run `start-tv.bat` with API address configured

## Features

- **Kiosk Mode:** Patient registration with language selection (Amharic/Oromo/English)
- **Doctor Mode:** Call next patient, complete tickets, recall announcements
- **TV Mode:** Waiting room display with queue information and announcements
- **Server Manager:** Central control panel for launching all applications
- **Audio Announcements:** Amharic ticket announcements (requires audio files in Assets folder)

## Requirements

- Windows 10/11 (64-bit)
- No .NET installation required (self-contained)
- Network connection for LAN deployment
- Printer (optional, for ticket printing)

## Troubleshooting

**API Connection Issues:**
- Verify API server is running on port 5000
- Check firewall settings allow port 5000
- Ensure correct IP address in `AFRAN_QUEUE_API` environment variable

**Audio Not Playing:**
- Verify audio files exist in `Assets\Voices\Amharic\`
- Check system audio settings
- Audio playback is optional - queue display will work without it

**Database Issues:**
- SQLite.db is created automatically on first run
- Delete `SQLite.db` to reset the queue system

## Keyboard Shortcuts

- **Escape:** Close any application window
- Applications auto-close when server manager closes

## Support

For issues or questions, refer to the main project documentation.
