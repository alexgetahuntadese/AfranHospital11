# Running Afran Hospital Queue System on Old PCs

This app is a Windows WPF desktop app. For old PCs, the easiest deployment is a
self-contained publish so the computer does not need the .NET runtime installed.

## Minimum Recommendation

- Windows 10 or newer
- 64-bit Windows preferred
- 4 GB RAM recommended
- Same LAN/Wi-Fi network for kiosk, doctor, TV, and API/server machines

Very old Windows 7 or Windows 8 PCs are not recommended for this .NET 8 WPF app.

## Build the Desktop App

From the project folder:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

The app will be published here:

```text
bin\Release\net8.0-windows\win-x64\publish
```

Copy the whole `publish` folder to the old PC.

## If the Old PC Is 32-bit

Use this instead:

```powershell
dotnet publish -c Release -r win-x86 --self-contained true
```

The output will be under:

```text
bin\Release\net8.0-windows\win-x86\publish
```

## Run the Screens

Inside the copied `publish` folder:

```powershell
.\AfranHospitalKiosk.exe kiosk
.\AfranHospitalKiosk.exe doctor
.\AfranHospitalKiosk.exe tv
```

Use `Esc` to close a fullscreen window during testing.

## Run the Queue API on the Server PC

Publish the API separately:

```powershell
dotnet publish .\QueueApi\QueueApi.csproj -c Release -r win-x64 --self-contained true
```

Run the published API executable on one LAN/server PC. The API listens on:

```text
http://0.0.0.0:5000
```

## Connect Kiosk, Doctor, and TV PCs to the API

On each kiosk, doctor, or TV PC, set the API address before launching the app.
Replace `192.168.1.10` with the server PC's LAN IP address:

```powershell
$env:AFRAN_QUEUE_API="http://192.168.1.10:5000"
.\AfranHospitalKiosk.exe kiosk
```

For doctor mode:

```powershell
$env:AFRAN_QUEUE_API="http://192.168.1.10:5000"
.\AfranHospitalKiosk.exe doctor
```

For TV mode:

```powershell
$env:AFRAN_QUEUE_API="http://192.168.1.10:5000"
.\AfranHospitalKiosk.exe tv
```

## Best Machine Layout for Old PCs

- Strongest PC: API/server
- TV display PC: use a newer/stronger machine if possible
- Old PCs: better for kiosk or doctor mode

TV mode uses more resources because it can display slideshow/video and play
ticket audio.

## Troubleshooting

If the app does not connect to the queue:

1. Make sure the server/API PC is turned on.
2. Make sure all PCs are on the same LAN.
3. Check that the API address uses the server PC's correct IP address.
4. Allow port `5000` through Windows Firewall on the server PC.

If the app does not open:

1. Confirm the PC is running Windows 10 or newer.
2. Try the `win-x86` publish if the PC is 32-bit.
3. Copy the entire `publish` folder, not only the `.exe`.
