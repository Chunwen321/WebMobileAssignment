# ?? Setup Guide: Access Web App from Phone on LAN

## Prerequisites
- Your laptop and phone must be on the **same WiFi network**
- Windows Firewall must allow incoming connections on port 5045

---

## Step 1: Configure Windows Firewall

### Option A: Using PowerShell (Recommended - Run as Administrator)

```powershell
# Allow incoming HTTP traffic on port 5045
New-NetFirewallRule -DisplayName "ASP.NET Core App (Port 5045)" -Direction Inbound -LocalPort 5045 -Protocol TCP -Action Allow

# Verify the rule was created
Get-NetFirewallRule -DisplayName "ASP.NET Core App (Port 5045)"
```

### Option B: Using Windows Defender Firewall GUI

1. Open **Windows Defender Firewall with Advanced Security**
   - Press `Win + R`, type `wf.msc`, press Enter
2. Click **Inbound Rules** in the left panel
3. Click **New Rule...** in the right panel
4. Select **Port**, click Next
5. Select **TCP**, enter `5045` in Specific local ports, click Next
6. Select **Allow the connection**, click Next
7. Check all profiles (Domain, Private, Public), click Next
8. Name it "ASP.NET Core App (Port 5045)", click Finish

---

## Step 2: Find Your Laptop's IP Address

### Using Command Prompt:
```cmd
ipconfig
```

Look for **IPv4 Address** under your WiFi adapter (usually something like `192.168.1.XXX` or `10.0.0.XXX`)

### Using PowerShell:
```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -like "*Wi-Fi*"} | Select-Object IPAddress
```

**Example Output:**
```
IPAddress
---------
192.168.1.100
```

---

## Step 3: Run Your Application with LAN Profile

### Using Visual Studio:
1. Click the dropdown next to the Run button (green play button)
2. Select **LAN** profile
3. Click Run

### Using Command Line:
```cmd
cd WebMobileAssignment
dotnet run --launch-profile LAN
```

You should see output like:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://0.0.0.0:5045
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## Step 4: Access from Your Phone

### On your phone's browser, navigate to:
```
http://YOUR_LAPTOP_IP:5045
```

**Example:**
```
http://192.168.1.100:5045
```

### Test Different Pages:
- **Home:** `http://192.168.1.100:5045/`
- **Admin Login:** `http://192.168.1.100:5045/Account/Login`
- **Attendance PIN Entry:** `http://192.168.1.100:5045/Admin/AttendancePinEntry`

---

## Troubleshooting

### ? Problem: "Can't reach this page" or "Connection refused"

**Solutions:**
1. **Verify Firewall Rule:**
   ```powershell
   Get-NetFirewallRule -DisplayName "ASP.NET Core App (Port 5045)" | Get-NetFirewallPortFilter
   ```
   
2. **Check if app is running:**
   ```powershell
   netstat -an | findstr :5045
   ```
   Should show: `0.0.0.0:5045`

3. **Temporarily disable Windows Firewall (for testing only):**
   - Open Windows Defender Firewall
   - Click "Turn Windows Defender Firewall on or off"
   - Turn off for Private networks
   - Try accessing from phone
   - **Remember to turn it back on!**

4. **Ensure both devices are on the same network:**
   - Check WiFi name on both laptop and phone
   - They must match exactly

---

### ? Problem: "localhost" works but IP address doesn't

**Solution:** Make sure you're using the **LAN** profile, not the **http** or **https** profile.

---

### ? Problem: HTTPS certificate errors

**Solution:** Use HTTP (port 5045) instead of HTTPS for local network access.

---

## Step 5: Test Attendance PIN Entry from Phone

1. **On your laptop (admin):**
   - Go to `http://localhost:5045/Admin/AttendanceTake`
   - Generate a PIN code for a class
   - Note the PIN and QR code URL

2. **On your phone (student):**
   - Open browser and go to: `http://YOUR_LAPTOP_IP:5045/Admin/AttendancePinEntry?pin=123456`
   - Or scan the QR code
   - Enter your Student ID
   - Submit attendance

---

## Security Notes

?? **Important:**
- This setup is for **development/testing only**
- The app is accessible to anyone on your WiFi network
- Use HTTPS with proper SSL certificates for production
- Consider implementing rate limiting for PIN entry
- Monitor access logs for suspicious activity

---

## Quick Reference Card

| Item | Value |
|------|-------|
| **Launch Profile** | LAN |
| **Port** | 5045 |
| **Laptop URL** | http://localhost:5045 |
| **Phone URL** | http://[YOUR_LAPTOP_IP]:5045 |
| **Protocol** | HTTP (not HTTPS) |
| **Firewall Rule** | Allow TCP port 5045 inbound |

---

## Additional Features for Mobile Access

### 1. QR Code Generation
The app already generates QR codes for attendance PIN entry. These work perfectly on mobile!

### 2. Responsive Design
Your views already use Bootstrap, which is mobile-responsive.

### 3. Mobile-Optimized PIN Entry
The `AttendancePinEntry` view is already mobile-friendly with large input fields.

---

## Testing Checklist

- [ ] Firewall rule created
- [ ] Laptop IP address identified
- [ ] App running with LAN profile
- [ ] Phone connected to same WiFi
- [ ] Can access home page from phone
- [ ] Can login from phone
- [ ] Can take attendance from phone
- [ ] QR code scanning works
- [ ] PIN entry works

---

## Need Help?

Run this diagnostic script on your laptop:

```powershell
# Check network configuration
Write-Host "=== Network Configuration ===" -ForegroundColor Cyan
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -like "*Wi-Fi*" -or $_.InterfaceAlias -like "*Ethernet*"} | Select-Object InterfaceAlias, IPAddress

# Check if app is listening
Write-Host "`n=== Application Status ===" -ForegroundColor Cyan
netstat -an | findstr :5045

# Check firewall rule
Write-Host "`n=== Firewall Rule ===" -ForegroundColor Cyan
Get-NetFirewallRule -DisplayName "*5045*" | Select-Object DisplayName, Enabled, Direction

# Check WiFi connection
Write-Host "`n=== WiFi Connection ===" -ForegroundColor Cyan
netsh wlan show interfaces
```

Save this as `diagnose.ps1` and run in PowerShell.

---

**?? You're all set! Enjoy testing your attendance system on mobile devices!**
