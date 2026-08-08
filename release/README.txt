HIWOT FANA INTERNAL MEDICINE SPECIALTY CLINIC QUEUE SYSTEM

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
