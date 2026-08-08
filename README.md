# Afran Hospital Queue System

Fullscreen WPF desktop screens for a LAN-deployed registration queue:

- `kiosk`: patient registration flow, language -> gender -> ticket print.
- `doctor`: doctor/reception station for calling and completing tickets.
- `tv`: waiting-room display for now-serving and next tickets.

## Run Modes

The normal deployment entry point is the launcher. Run `AfranHospitalKiosk.exe`
without arguments to open one control panel for Queue API, Kiosk, Doctor, and TV.
Direct module modes are still available for dedicated machines:

```powershell
dotnet run                 # launcher UI
dotnet run -- doctor
dotnet run -- tv
dotnet run -- kiosk
```

Press `Esc` to close a window during testing.

Run the LAN API:

```powershell
dotnet run --project .\QueueApi\QueueApi.csproj
```

The API listens on:

```text
http://0.0.0.0:5000
```

Operational endpoints:

```text
GET /health/live     process liveness
GET /health/ready    database readiness
```

For a protected LAN deployment, set `Security:ApiKey` in
`QueueApi\appsettings.json` (or the `Security__ApiKey` environment variable)
and set the same value as `AFRAN_QUEUE_API_KEY` on every desktop client. The
API then requires `X-Api-Key` for queue-changing requests. Keep the key out of
source control and deployment documentation.

Browser-based clients should list their exact origins under
`Cors:AllowedOrigins`; when the list is empty, only localhost and private LAN
origins are accepted.

On kiosk/doctor/TV machines, set the API address before launching:

```powershell
$env:AFRAN_QUEUE_API="http://192.168.1.10:5000"
.\AfranHospitalKiosk.exe kiosk
.\AfranHospitalKiosk.exe doctor
.\AfranHospitalKiosk.exe tv
```

## Build

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained false
```

## Deploy Now

Create a ready-to-copy Windows deployment folder with a self-contained desktop
launcher and API:

```powershell
.\deploy-now.ps1
```

Then copy the generated `release` folder and run
`release\Start-AfranHospital.bat`. The launcher automatically uses the
published `QueueApi\QueueApi.exe` next to the desktop application, so a .NET
installation is not required.

## Web TV Display

After starting Queue API, open `http://SERVER-IP:5000/tv/` in any browser on the TV or another LAN computer. The display updates automatically and supports full-screen mode.

## LAN Deployment Architecture

Recommended machine layout:

- Reception/kiosk PC: runs `AfranHospitalKiosk.exe kiosk`
- Doctor/reception desk PC: runs `AfranHospitalKiosk.exe doctor`
- Waiting-room TV mini PC: runs `AfranHospitalKiosk.exe tv`
- Server PC on the same LAN: hosts queue API and database

Recommended network flow:

- Kiosk creates tickets through `POST /api/tickets`
- Doctor station calls next through `POST /api/queue/registration/call-next`
- Doctor station completes through `POST /api/queue/registration/complete`
- TV display reads current queue through `GET /api/queue/registration/display`
- Optional live updates use SignalR at `/queueHub`

Recommended data store:

- SQL Server Express or SQLite on the LAN server
- Tables: `Tickets`, `QueueEvents`, `Counters`, `ServiceDesks`

Current app state:

- The screens and launch modes are ready.
- `QueueApi` provides SQLite storage, queue endpoints, and SignalR `/queueHub`.
- Kiosk creates tickets through the API, with local fallback if the API is offline.
- Doctor and TV modes read from the API and subscribe to SignalR queue updates.
- Publish `QueueApi` and the WPF app separately for LAN deployment.

## Local Amharic Audio

The TV screen can announce called tickets using downloaded or recorded local
audio files. Put Amharic announcement audio files here:

```text
Assets\Voices\Amharic
```

Use the exact ticket code as the file name:

```powershell
Assets\Voices\Amharic\M001.mp3
Assets\Voices\Amharic\M105.wav
Assets\Voices\Amharic\F023.mp3
```

When the doctor screen calls or recalls a ticket, the doctor screen plays the
matching WAV or MP3 file if it exists. The TV screen also plays the same local
audio when it receives a live `TicketCalled` update.

To generate online Amharic ticket audio for every `M001-M999` and `F001-F999`
file, with the speaking voice randomly chosen male or female independent of the
ticket prefix:

```powershell
 .\Tools\VoiceSampleGenerator\Scripts\python.exe .\Tools\VoiceSampleGenerator\generate_amharic_ticket_audio.py --start 1 --end 999 --prefix both --room 102 --male-room 101 --female-room 102 --voice-mode random --force
```

The generated sentence format is:

```text
ቁጥር ኤም ዜሮ ሃያ አምስት፣ ወደ ሐኪም ክፍል 101 ይሂዱ።
```
