@echo off
echo ========================================
echo ATTENDANCE SYSTEM STARTUP CHECK
echo ========================================
echo.

echo [1/6] Cleaning previous build...
dotnet clean WebMobileAssignment/WebMobileAssignment.csproj --verbosity quiet
echo Done!
echo.

echo [2/6] Rebuilding solution...
dotnet build WebMobileAssignment/WebMobileAssignment.csproj --verbosity quiet
if errorlevel 1 (
    echo BUILD FAILED!
    echo.
    echo Please check the errors above and try again.
    pause
    exit /b 1
)
echo Build successful!
echo.

echo [3/6] Checking database table...
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "C:\Users\User\source\repos\WebMobileAssignment\WebMobileAssignment\DB.MDF" -Q "SELECT COUNT(*) as TableExists FROM sys.tables WHERE name = 'AttendanceSessions'" -h -1
echo.

echo [4/6] Verifying controller namespace...
findstr /C:"namespace WebMobileAssignment.Controllers" WebMobileAssignment\Controllers\AdminController.cs >nul
if errorlevel 1 (
    echo WARNING: Controller namespace might be incorrect!
) else (
    echo Controller namespace: OK
)
echo.

echo [5/6] Getting your IP address...
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4 Address"') do (
    set IP=%%a
    goto :found
)
:found
echo Your LAN IP:%IP%
echo.

echo [6/6] Starting application on LAN...
echo.
echo ========================================
echo ACCESS URLS:
echo ========================================
echo.
echo From this computer:
echo   http://localhost:5045/Admin/AttendanceTake
echo   http://localhost:5045/Admin/Dashboard
echo.
echo From mobile (same WiFi):
echo   http://%IP%:5045/Admin/AttendancePinEntry
echo.
echo ========================================
echo Server is starting...
echo Press Ctrl+C to stop the server
echo ========================================
echo.

cd WebMobileAssignment
dotnet run --launch-profile LAN
