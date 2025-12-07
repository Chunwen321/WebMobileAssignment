# TROUBLESHOOTING: Attendance Pages Not Found

## Problem
Getting 404 errors when accessing attendance pages:
- `/Admin/AttendanceTake`
- `/Admin/AttendancePinEntry`
- `/Admin/AttendanceManagement`
- `/Admin/AttendanceRecords`

## Solution Steps

### 1. **Rebuild the Application**
```bash
# Clean the solution
dotnet clean

# Rebuild
dotnet build
```

### 2. **Verify Database Table Exists**
The `AttendanceSessions` table should exist. Check with:
```sql
SELECT * FROM AttendanceSessions
```

### 3. **Restart the Application**
- Stop the application if running (Ctrl+C in terminal)
- Start again:
```bash
dotnet run --launch-profile LAN
```

### 4. **Access the Pages**

Navigate to these URLs in order:

#### Test on Localhost:
1. **Take Attendance:** `http://localhost:5045/Admin/AttendanceTake`
2. **PIN Entry:** `http://localhost:5045/Admin/AttendancePinEntry`
3. **Manage:** `http://localhost:5045/Admin/AttendanceManagement`
4. **Records:** `http://localhost:5045/Admin/AttendanceRecords`

#### Test on LAN (from mobile):
Replace `localhost` with your computer's IP address:
```
http://192.168.1.XXX:5045/Admin/AttendanceTake
```

### 5. **Check Navigation Menu**

The sidebar should show:
```
?? Attendance
   ?? ?? Take Attendance
   ?? ?? Manage Attendance
   ?? ?? View Records
```

### 6. **Common Issues & Fixes**

#### Issue: "Controller not found"
**Fix:**
- Check namespace in `AdminController.cs` should be `TuitionAttendanceSystem.Controllers`
- Rebuild solution

#### Issue: "View not found"
**Fix:**
- Verify views exist in `Views/Admin/` folder:
  - `AttendanceTake.cshtml`
  - `AttendancePinEntry.cshtml`
  - `AttendanceManagement.cshtml`
  - `AttendanceRecords.cshtml`

#### Issue: "Classes not found" error
**Fix:**
```sql
-- Add some test classes if none exist
INSERT INTO Classes (ClassId, ClassName, MaxCapacity, CurrentCapacity)
VALUES 
  ('CLASS001', 'Mathematics 101', 30, 5),
  ('CLASS002', 'Science Lab', 25, 8);
```

#### Issue: QR Code not showing
**Fix:**
- Ensure jQuery is loaded (check browser console)
- Check QRCode.js CDN is accessible
- Verify internet connection

#### Issue: Cannot generate PIN
**Fix:**
1. Open browser DevTools (F12)
2. Check Console tab for errors
3. Check Network tab when clicking "Generate PIN"
4. Verify AJAX request to `/Admin/GenerateAttendancePin` succeeds

### 7. **Quick Test Workflow**

1. **Go to Take Attendance**
   ```
   http://localhost:5045/Admin/AttendanceTake
   ```

2. **Select a class from dropdown**
   - If no classes appear, add classes first

3. **Click "Generate PIN Code"**
   - Should see 6-digit PIN
   - QR code should appear

4. **Test PIN Entry (on same browser)**
   ```
   http://localhost:5045/Admin/AttendancePinEntry?pin=123456
   ```
   - PIN should auto-fill
   - Enter student ID (e.g., `S0001`)
   - Click Submit

5. **Check Attendance Management**
   ```
   http://localhost:5045/Admin/AttendanceManagement
   ```
   - Should see today's attendance records

### 8. **Debug Mode**

If pages still not working, check with these commands:

```bash
# Check if routes are registered
dotnet run --urls="http://localhost:5045" --environment=Development

# View all registered routes (add this temporarily to Program.cs)
app.MapGet("/debug-routes", (IEnumerable<EndpointDataSource> endpointSources) =>
{
    var endpoints = endpointSources.SelectMany(es => es.Endpoints);
    return endpoints.Select(e => e.DisplayName);
});
```

### 9. **Clear Browser Cache**

Sometimes views are cached:
1. Press `Ctrl + Shift + Delete`
2. Clear "Cached images and files"
3. Hard refresh: `Ctrl + F5`

### 10. **Verify Controller Actions**

Open `AdminController.cs` and verify these methods exist:
- ? `AttendanceTake()`
- ? `GenerateAttendancePin()`
- ? `AttendancePinEntry()`
- ? `SubmitAttendancePin()`
- ? `AttendanceManagement()`
- ? `UpdateAttendanceStatus()`
- ? `AttendanceRecords()`

All methods should be `public async Task<IActionResult>` and not have `[NonAction]` attribute.

## Expected Behavior

When working correctly:

1. **AttendanceTake** page shows:
   - Class selection dropdown
   - Duration selector
   - "Generate PIN Code" button
   - Instructions panel

2. After generating PIN:
   - 6-digit PIN displays in purple gradient box
   - QR code appears
   - Session info (enrolled count, expiry time)

3. **AttendancePinEntry** page shows:
   - 6 large digit input boxes
   - Student ID field
   - Submit button
   - If accessed via QR: PIN auto-fills

4. **AttendanceManagement** page shows:
   - Date and class filters
   - List of today's attendance
   - Quick status edit buttons

5. **AttendanceRecords** page shows:
   - Statistics cards (Total, Present, Absent, Rate)
   - Filter options
   - Full attendance history table

## Still Not Working?

If you've tried everything above and it still doesn't work:

1. **Check the error message** - What does it say exactly?
2. **Check browser console** - Any JavaScript errors?
3. **Check server logs** - Any exceptions in terminal?
4. **Verify file paths** - Are views in correct folders?
5. **Check routing** - Is the URL exactly `/Admin/AttendanceTake` (case-sensitive)?

## Contact for Help

Provide this information:
- Exact error message
- URL you're trying to access
- Screenshot of browser DevTools Console
- Any server error logs from terminal
