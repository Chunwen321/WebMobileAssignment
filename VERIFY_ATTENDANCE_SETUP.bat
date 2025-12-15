@echo off
echo ========================================
echo ATTENDANCE SYSTEM - VERIFICATION CHECK
echo ========================================
echo.

set ERRORS=0

echo [1/7] Checking controller file...
if exist "WebMobileAssignment\Controllers\AdminController.cs" (
    echo [OK] AdminController.cs exists
) else (
    echo [ERROR] AdminController.cs not found!
    set /a ERRORS+=1
)
echo.

echo [2/7] Checking controller namespace...
findstr /C:"namespace WebMobileAssignment.Controllers" WebMobileAssignment\Controllers\AdminController.cs >nul
if errorlevel 1 (
    echo [ERROR] Controller has wrong namespace!
    set /a ERRORS+=1
) else (
    echo [OK] Controller namespace is correct
)
echo.

echo [3/7] Checking view files...
set VIEW_COUNT=0
if exist "WebMobileAssignment\Views\Admin\AttendanceTake.cshtml" (
    echo [OK] AttendanceTake.cshtml exists
    set /a VIEW_COUNT+=1
)
if exist "WebMobileAssignment\Views\Admin\AttendancePinEntry.cshtml" (
    echo [OK] AttendancePinEntry.cshtml exists
    set /a VIEW_COUNT+=1
)
if exist "WebMobileAssignment\Views\Admin\AttendanceManagement.cshtml" (
    echo [OK] AttendanceManagement.cshtml exists
    set /a VIEW_COUNT+=1
)
if exist "WebMobileAssignment\Views\Admin\AttendanceRecords.cshtml" (
    echo [OK] AttendanceRecords.cshtml exists
    set /a VIEW_COUNT+=1
)

if %VIEW_COUNT% LSS 4 (
    echo [ERROR] Missing view files! Found %VIEW_COUNT%/4
    set /a ERRORS+=1
)
echo.

echo [4/7] Checking database model...
findstr /C:"public DbSet<AttendanceSession> AttendanceSessions" WebMobileAssignment\Models\DB.cs >nul
if errorlevel 1 (
    echo [ERROR] AttendanceSession not in DB model!
    set /a ERRORS+=1
) else (
    echo [OK] AttendanceSession in DB model
)
echo.

echo [5/7] Checking database table...
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "C:\Users\User\source\repos\WebMobileAssignment\WebMobileAssignment\DB.MDF" -Q "SELECT COUNT(*) as Count FROM sys.tables WHERE name = 'AttendanceSessions'" -h -1 >temp.txt 2>nul
set /p TABLE_EXISTS=<temp.txt
del temp.txt 2>nul
if "%TABLE_EXISTS%"=="1" (
    echo [OK] AttendanceSessions table exists
) else (
    echo [ERROR] AttendanceSessions table not found!
    set /a ERRORS+=1
)
echo.

echo [6/7] Checking launchSettings.json...
findstr /C:"LAN" WebMobileAssignment\Properties\launchSettings.json >nul
if errorlevel 1 (
    echo [ERROR] LAN profile not found in launchSettings!
    set /a ERRORS+=1
) else (
    echo [OK] LAN profile configured
)
echo.

echo [7/7] Verifying controller actions...
findstr /C:"AttendanceTake" WebMobileAssignment\Controllers\AdminController.cs >nul
if errorlevel 1 (
    echo [ERROR] AttendanceTake action not found!
    set /a ERRORS+=1
) else (
    echo [OK] AttendanceTake action exists
)
echo.

echo ========================================
echo VERIFICATION SUMMARY
echo ========================================
if %ERRORS% EQU 0 (
    echo.
    echo [SUCCESS] All checks passed! ?
    echo.
    echo The attendance system is ready to use.
    echo.
    echo Next steps:
    echo 1. Run: START_ATTENDANCE_SYSTEM.bat
    echo 2. Open: http://localhost:5045/Admin/AttendanceTake
    echo.
) else (
    echo.
    echo [FAILED] %ERRORS% error(s) found! ?
    echo.
    echo Please fix the errors above before running.
    echo.
)
echo ========================================

pause
