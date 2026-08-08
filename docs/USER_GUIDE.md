# Afran Hospital Queue System — User Guide

## System roles

- **Server:** runs the Queue API and stores the SQLite queue database.
- **Kiosk:** creates and prints patient tickets.
- **Doctor station:** selects a room, calls tickets, recalls tickets, and completes consultations.
- **Waiting-room TV:** displays the current ticket, room, and waiting list and announces called tickets.

## Start the system

### Using the launcher

1. Start `Start-AfranHospital.bat` from the release folder.
2. On the server computer, open the launcher and start **Queue API**.
3. Confirm that the API status shows **Online**.
4. Start the Kiosk, Doctor, and TV modules as required.

The launcher uses port `5000` by default. If the server has a firewall, allow inbound TCP port `5000` on the server computer only.

### Separate computers

On each client computer, enter the server address in the launcher, for example:

```text
http://192.168.1.10:5000
```

The server and clients must use the same API key. Set it before starting the applications:

```powershell
$env:AFRAN_QUEUE_API_KEY="your-long-private-key"
```

The API server uses the matching setting:

```powershell
$env:Security__ApiKey="your-long-private-key"
```

Do not place the real key in source control or screenshots.

## Register a patient at the kiosk

1. Select the patient language.
2. Select the patient gender.
3. Wait for the ticket number to appear.
4. Take the printed ticket.

The kiosk only prints a ticket after the server confirms it. If the API is unavailable, it shows an error and does not print an unsynchronized local ticket.

## Call a patient from the doctor station

1. Open **Doctor station**.
2. Enter the destination room in **Room number**, such as `3`, `101`, or `205`.
3. Select **Call next ticket**.
4. Confirm that the ticket and room shown on the doctor screen are correct.

The selected room is stored with the called ticket and is sent to the TV and announcement system. Room numbers should be short numeric identifiers used by the hospital.

### Complete and call next

Select **Complete and call next** after the consultation. The current ticket is marked completed and the next waiting ticket is called into the room currently entered in **Room number**.

If the next ticket is not called, verify that the room field is correct and that waiting tickets exist.

### Recall a ticket

Select **Recall ticket** to announce the current called ticket again. The stored room is used for the recall, so changing the room field does not move an already-called ticket.

## Voice announcements

The TV and doctor station announce called tickets twice.

- If a matching prerecorded ticket audio file exists, it is played.
- The room is announced from a matching room audio file when available.
- Otherwise, Windows text-to-speech announces the ticket and room.

Room audio files can use either naming format:

```text
Assets\Voices\Amharic\Room205.mp3
Assets\Voices\Amharic\room-205.wav
```

Dynamic rooms without prerecorded audio use text-to-speech. Keep the room number visible and readable because it is also shown on the TV.

## Waiting-room TV

The TV displays:

- the current ticket;
- the destination room;
- the next waiting tickets; and
- the waiting count.

It updates through the Queue API and SignalR. If the API is offline, it shows a demo display; do not use demo tickets for real patient flow.

## Troubleshooting

### API offline

- Confirm Queue API is running on the server.
- Confirm the client URL uses the correct server IP and port.
- Test connectivity with `Test-NetConnection SERVER-IP -Port 5000`.
- Confirm the API key is set on both server and client.

### Ticket does not print

- Confirm a printer is installed and selected as the default printer.
- Confirm the API returned a ticket before troubleshooting printing.

### Room is wrong

- Check the doctor station’s **Room number** before calling.
- For an already-called ticket, use **Complete** and call it again from the correct room if policy allows.
- Confirm the TV is connected to the same API server.

### Voice is missing

- Confirm Windows audio output and volume.
- Confirm the TV/doctor computer has the required voice assets.
- Add a room audio file or allow Windows text-to-speech to provide the room announcement.

## Daily shutdown

1. Finish or record the current consultation.
2. Close the Kiosk, Doctor, and TV modules.
3. Stop the Queue API from the launcher.
4. Back up `SQLite.db` from the API folder before maintenance or upgrades.
