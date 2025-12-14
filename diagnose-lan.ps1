# ================================================
# ?? Diagnostic Script for LAN Access Issues
# ================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LAN Access Diagnostics" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Network Information
Write-Host "1??  Network Configuration" -ForegroundColor Yellow
Write-Host "????????????????????????????????????????" -ForegroundColor Gray

$networkAdapters = Get-NetIPAddress -AddressFamily IPv4 | 
                   Where-Object {$_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*"} |
                   Select-Object InterfaceAlias, IPAddress, PrefixLength

if ($networkAdapters) {
    $networkAdapters | Format-Table -AutoSize
} else {
    Write-Host "   ? No active network adapters found" -ForegroundColor Red
}

# 2. WiFi Connection Status
Write-Host "`n2??  WiFi Connection" -ForegroundColor Yellow
Write-Host "????????????????????????????????????????" -ForegroundColor Gray

try {
    $wifiStatus = netsh wlan show interfaces
    if ($wifiStatus -match "State\s+:\s+connected") {
        Write-Host "   ? WiFi is connected" -ForegroundColor Green
        
        $ssid = ($wifiStatus | Select-String -Pattern "SSID" | Select-Object -First 1) -replace ".*:\s*", ""
        Write-Host "   ?? Network: $ssid" -ForegroundColor Cyan
        
        $signal = ($wifiStatus | Select-String -Pattern "Signal") -replace ".*:\s*", ""
        Write-Host "   ?? Signal: $signal" -ForegroundColor Cyan
    } else {
        Write-Host "   ??  WiFi is not connected" -ForegroundColor Yellow
        Write-Host "   ?? Try connecting to WiFi first" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ??  Could not retrieve WiFi information" -ForegroundColor Yellow
    Write-Host "   ?? WiFi might not be available or adapter is disabled" -ForegroundColor Gray
}

# 3. Application Status
Write-Host "`n3??  Application Status (Port 5045)" -ForegroundColor Yellow
Write-Host "????????????????????????????????????????" -ForegroundColor Gray

$listeningPorts = netstat -an | Select-String -Pattern ":5045"

if ($listeningPorts) {
    Write-Host "   ? Application is running on port 5045" -ForegroundColor Green
    $listeningPorts | ForEach-Object {
        Write-Host "   ?? $_" -ForegroundColor Cyan
    }
    
    # Check if listening on 0.0.0.0
    if ($listeningPorts -match "0\.0\.0\.0:5045") {
        Write-Host "   ? Listening on all interfaces (0.0.0.0)" -ForegroundColor Green
    } elseif ($listeningPorts -match "127\.0\.0\.1:5045") {
        Write-Host "   ??  Only listening on localhost (127.0.0.1)" -ForegroundColor Yellow
        Write-Host "   ?? Use the 'LAN' launch profile to listen on all interfaces" -ForegroundColor Gray
    }
} else {
    Write-Host "   ? Application is NOT running on port 5045" -ForegroundColor Red
    Write-Host "   ?? Start with: dotnet run --launch-profile LAN" -ForegroundColor Gray
}

# 4. Firewall Rules
Write-Host "`n4??  Firewall Rules" -ForegroundColor Yellow
Write-Host "????????????????????????????????????????" -ForegroundColor Gray

$firewallRule = Get-NetFirewallRule -DisplayName "*5045*" -ErrorAction SilentlyContinue

if ($firewallRule) {
    Write-Host "   ? Firewall rule exists" -ForegroundColor Green
    
    $firewallRule | ForEach-Object {
        $portFilter = $_ | Get-NetFirewallPortFilter
        $addressFilter = $_ | Get-NetFirewallAddressFilter
        
        Write-Host "   ?? Rule: $($_.DisplayName)" -ForegroundColor Cyan
        Write-Host "      Enabled: $($_.Enabled)" -ForegroundColor Gray
        Write-Host "      Direction: $($_.Direction)" -ForegroundColor Gray
        Write-Host "      Action: $($_.Action)" -ForegroundColor Gray
        Write-Host "      Protocol: $($portFilter.Protocol)" -ForegroundColor Gray
        Write-Host "      Local Port: $($portFilter.LocalPort)" -ForegroundColor Gray
    }
} else {
    Write-Host "   ? No firewall rule found for port 5045" -ForegroundColor Red
    Write-Host "   ?? Run SETUP_LAN.bat to create the firewall rule" -ForegroundColor Gray
}

# 5. Firewall Status
Write-Host "`n5??  Windows Firewall Status" -ForegroundColor Yellow
Write-Host "????????????????????????????????????????" -ForegroundColor Gray

$firewallProfiles = Get-NetFirewallProfile | Select-Object Name, Enabled

$firewallProfiles | ForEach-Object {
    $status = if ($_.Enabled) { "? Enabled" } else { "??  Disabled" }
    $color = if ($_.Enabled) { "Green" } else { "Yellow" }
    Write-Host "   $($_.Name): " -NoNewline -ForegroundColor Gray
    Write-Host $status -ForegroundColor $color
}

# 6. Connectivity Test
Write-Host "`n6??  Connectivity Tests" -ForegroundColor Yellow
Write-Host "????????????????????????????????????????" -ForegroundColor Gray

# Test localhost
Write-Host "   Testing localhost..." -NoNewline
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5045" -Method Head -TimeoutSec 2 -ErrorAction Stop
    Write-Host " ? Success" -ForegroundColor Green
} catch {
    Write-Host " ? Failed" -ForegroundColor Red
    Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Gray
}

# Test 127.0.0.1
Write-Host "   Testing 127.0.0.1..." -NoNewline
try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:5045" -Method Head -TimeoutSec 2 -ErrorAction Stop
    Write-Host " ? Success" -ForegroundColor Green
} catch {
    Write-Host " ? Failed" -ForegroundColor Red
    Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Gray
}

# Test actual IP
$laptopIP = (Get-NetIPAddress -AddressFamily IPv4 | 
             Where-Object {$_.InterfaceAlias -like "*Wi-Fi*" -or $_.InterfaceAlias -like "*Wireless*" -or $_.InterfaceAlias -like "*Ethernet*"} | 
             Where-Object {$_.IPAddress -notlike "169.254.*"} |
             Select-Object -First 1).IPAddress

if ($laptopIP) {
    Write-Host "   Testing $laptopIP..." -NoNewline
    try {
        $response = Invoke-WebRequest -Uri "http://${laptopIP}:5045" -Method Head -TimeoutSec 2 -ErrorAction Stop
        Write-Host " ? Success" -ForegroundColor Green
    } catch {
        Write-Host " ? Failed" -ForegroundColor Red
        Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

# 7. Summary and Recommendations
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Summary & Recommendations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$issues = @()

# Check each component
if (-not $networkAdapters) {
    $issues += "? No active network connection"
}

if (-not $listeningPorts) {
    $issues += "? Application is not running"
}

if (-not ($listeningPorts -match "0\.0\.0\.0:5045")) {
    $issues += "??  Application not listening on all interfaces"
}

if (-not $firewallRule) {
    $issues += "? Firewall rule missing"
}

if ($issues.Count -eq 0) {
    Write-Host "`n   ? All systems operational!" -ForegroundColor Green
    Write-Host "`n   ?? Access from your phone using:" -ForegroundColor Yellow
    Write-Host "   http://$laptopIP:5045" -ForegroundColor Cyan
} else {
    Write-Host "`n   ??  Issues detected:" -ForegroundColor Yellow
    $issues | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    
    Write-Host "`n   ?? Recommended actions:" -ForegroundColor Yellow
    
    if ($issues -match "network connection") {
        Write-Host "   1. Connect to WiFi or Ethernet" -ForegroundColor Gray
    }
    
    if ($issues -match "not running") {
        Write-Host "   1. Start the application with:" -ForegroundColor Gray
        Write-Host "      dotnet run --launch-profile LAN" -ForegroundColor Cyan
    }
    
    if ($issues -match "not listening") {
        Write-Host "   1. Make sure you're using the 'LAN' launch profile" -ForegroundColor Gray
        Write-Host "      In launchSettings.json, use the profile with 'http://0.0.0.0:5045'" -ForegroundColor Cyan
    }
    
    if ($issues -match "Firewall rule missing") {
        Write-Host "   1. Run SETUP_LAN.bat as Administrator to create firewall rule" -ForegroundColor Gray
    }
}

Write-Host "`n   ?? For detailed setup instructions, see SETUP_LAN_ACCESS.md" -ForegroundColor Cyan
Write-Host "`n"
pause
