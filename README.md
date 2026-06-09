# Afran Hospital Queue System

Fullscreen WPF desktop screens for a LAN-deployed registration queue:

- `kiosk`: patient registration flow, language -> gender -> ticket print.
- `doctor`: doctor/reception station for calling and completing tickets.
- `tv`: waiting-room display for now-serving and next tickets.

## Run Modes

```powershell
dotnet run
dotnet run -- doctor
dotnet run -- tv
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

## Natural Amharic Voice

The TV screen can announce called tickets in a local neural Amharic voice.
Run this once on the TV PC before launching `AfranHospitalKiosk.exe tv`:

```powershell
.\Tools\AmharicTts\setup.ps1
```

The setup downloads the CPU PyTorch runtime and Meta MMS Amharic TTS model
(`facebook/mms-tts-amh`) into `Tools\AmharicTts`. After that, ticket audio is
generated locally and cached as WAV files. The first announcement can take a
moment; repeated tickets play from cache.

The model expects romanized Amharic input, so the app speaks phrases like:

```text
ibakwo kutir em meto amist wede memezgebiya kotari hulet yihidu
```

That corresponds to: "Please, number M105, go to registration counter two."
