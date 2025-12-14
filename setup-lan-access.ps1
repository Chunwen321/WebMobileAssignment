# ================================================
# ?? Quick Setup Script for LAN Access
# ================================================
# Run this script as Administrator to set up everything automatically

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ASP.NET Core LAN Access Setup" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "? ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "`nRight-click PowerShell and select 'Run as Administrator', then run this script again.`n" -ForegroundColor Yellow
    pause
    exit
}

# Step 1: Create Firewall Rule
Write-Host "Step 1: Creating Firewall Rule..." -ForegroundColor Yellow

try {
    # Check if rule already exists
    $existingRule = Get-NetFirewallRule -DisplayName "ASP.NET Core App (Port 5045)" -ErrorAction SilentlyContinue
    
    if ($existingRule) {
        Write-Host "  ? Firewall rule already exists" -ForegroundColor Green
    } else {
        New-NetFirewallRule -DisplayName "ASP.NET Core App (Port 5045)" `
                            -Direction Inbound `
                            -LocalPort 5045 `
                            -Protocol TCP `
                            -Action Allow `
                            -Profile Any | Out-Null
        Write-Host "  ? Firewall rule created successfully" -ForegroundColor Green
    }
} catch {
    Write-Host "  ? Failed to create firewall rule: $_" -ForegroundColor Red
}

# Step 2: Get IP Address
Write-Host "`nStep 2: Finding your laptop's IP address..." -ForegroundColor Yellow

$ipAddress = Get-NetIPAddress -AddressFamily IPv4 | 
             Where-Object {$_.InterfaceAlias -like "*Wi-Fi*" -or $_.InterfaceAlias -like "*Wireless*"} | 
             Where-Object {$_.IPAddress -notlike "169.254.*"} |
             Select-Object -First 1

if ($ipAddress) {
    Write-Host "  ? Your laptop's IP address: " -NoNewline -ForegroundColor Green
    Write-Host "$($ipAddress.IPAddress)" -ForegroundColor Cyan
    $laptopIP = $ipAddress.IPAddress
} else {
    Write-Host "  ? WiFi IP not found. Checking Ethernet..." -ForegroundColor Yellow
    
    $ipAddress = Get-NetIPAddress -AddressFamily IPv4 | 
                 Where-Object {$_.InterfaceAlias -like "*Ethernet*"} | 
                 Where-Object {$_.IPAddress -notlike "169.254.*"} |
                 Select-Object -First 1
    
    if ($ipAddress) {
        Write-Host "  ? Your laptop's IP address (Ethernet): " -NoNewline -ForegroundColor Green
        Write-Host "$($ipAddress.IPAddress)" -ForegroundColor Cyan
        $laptopIP = $ipAddress.IPAddress
    } else {
        Write-Host "  ? Could not find network IP address" -ForegroundColor Red
        $laptopIP = "YOUR_IP_ADDRESS"
    }
}

# Step 3: Check if application is running
Write-Host "`nStep 3: Checking if application is running..." -ForegroundColor Yellow

$appRunning = netstat -an | Select-String -Pattern ":5045.*LISTENING"

if ($appRunning) {
    Write-Host "  ? Application is running on port 5045" -ForegroundColor Green
} else {
    Write-Host "  ? Application is NOT running" -ForegroundColor Yellow
    Write-Host "    Start your app with: dotnet run --launch-profile LAN" -ForegroundColor Gray
}

# Step 4: Get WiFi Network Name
Write-Host "`nStep 4: Checking WiFi connection..." -ForegroundColor Yellow

try {
    $wifiInfo = netsh wlan show interfaces | Select-String -Pattern "SSID"
    if ($wifiInfo) {
        $ssid = ($wifiInfo[0] -split ":")[1].Trim()
        Write-Host "  ? Connected to WiFi: $ssid" -ForegroundColor Green
        Write-Host "    Make sure your phone is connected to the same network!" -ForegroundColor Gray
    } else {
        Write-Host "  ? WiFi information not available" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ? Could not retrieve WiFi information" -ForegroundColor Yellow
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n?? To access from your phone:" -ForegroundColor Yellow
Write-Host "   1. Make sure your phone is on the same WiFi network" -ForegroundColor White
Write-Host "   2. Open your phone's browser" -ForegroundColor White
Write-Host "   3. Go to: " -NoNewline -ForegroundColor White
Write-Host "http://$laptopIP:5045" -ForegroundColor Cyan

Write-Host "`n?? To start your application:" -ForegroundColor Yellow
Write-Host "   Option 1 (Visual Studio): Select 'LAN' profile and click Run" -ForegroundColor White
Write-Host "   Option 2 (Command Line):" -ForegroundColor White
Write-Host "   cd WebMobileAssignment" -ForegroundColor Gray
Write-Host "   dotnet run --launch-profile LAN" -ForegroundColor Gray

Write-Host "`n?? Quick Access URLs:" -ForegroundColor Yellow
Write-Host "   Home:     http://$laptopIP:5045/" -ForegroundColor Cyan
Write-Host "   Login:    http://$laptopIP:5045/Account/Login" -ForegroundColor Cyan
Write-Host "   PIN Entry: http://$laptopIP:5045/Admin/AttendancePinEntry" -ForegroundColor Cyan

Write-Host "`n?? Troubleshooting:" -ForegroundColor Yellow
Write-Host "   - Verify firewall rule: Get-NetFirewallRule -DisplayName '*5045*'" -ForegroundColor Gray
Write-Host "   - Check if app is listening: netstat -an | findstr :5045" -ForegroundColor Gray
Write-Host "   - Test from laptop first: http://localhost:5045" -ForegroundColor Gray

Write-Host "`n? Firewall configured" -ForegroundColor Green
Write-Host "? IP address identified" -ForegroundColor Green
if ($appRunning) {
    Write-Host "? Application is running" -ForegroundColor Green
} else {
    Write-Host "??  Application needs to be started" -ForegroundColor Yellow
}

Write-Host "`n" 
pause
