# Attendance System with PIN Code & QR Code

## ?? Features

### 1. **PIN Code Attendance Taking**
- Generate unique 6-digit PIN codes for each class session
- Configurable session duration (15 min to 2 hours)
- Automatic expiry of PIN codes
- Real-time attendance tracking

### 2. **QR Code Support**
- Auto-generate QR codes for each PIN session
- Students can scan QR code with their mobile phones
- Auto-fills PIN code on mobile device
- Works on LAN network (same WiFi)

### 3. **Mobile-Friendly Interface**
- Responsive design for phones and tablets
- Large PIN input digits for easy entry
- Touch-optimized UI
- Paste support for PIN codes

### 4. **Attendance Management**
- View and edit attendance records
- Change status (Present/Absent/Late)
- Filter by class, date, student
- Real-time updates

### 5. **Comprehensive Reports**
- Attendance history with statistics
- Filter by date range, class, student
- Export and print capabilities
- Attendance rate calculations

## ?? Getting Started

### Running on LAN (for QR Code Support)

1. **Find Your Computer's IP Address**
   ```bash
   # Windows
   ipconfig
   # Look for IPv4 Address (e.g., 192.168.1.100)
   
   # Mac/Linux
   ifconfig
   # or
   hostname -I
   ```

2. **Run with LAN Profile**
   ```bash
   dotnet run --launch-profile LAN
   ```

3. **Access URLs**
   - **From your computer:** `http://localhost:5045`
   - **From mobile (same WiFi):** `http://YOUR_IP:5045`
     - Example: `http://192.168.1.100:5045`

### Firewall Configuration

If you can't access from mobile, allow the port through firewall:

**Windows:**
```powershell
netsh advfirewall firewall add rule name="ASP.NET Core" dir=in action=allow protocol=TCP localport=5045
```

**Mac:**
```bash
sudo /usr/libexec/ApplicationFirewall/socketfilterfw --add dotnet
```

## ?? How to Use

### For Teachers/Admins

#### 1. Take Attendance
1. Navigate to **Attendance > Take Attendance**
2. Select the class
3. Choose session duration
4. Click **Generate PIN Code**
5. Display the PIN and QR code to students

#### 2. Share with Students
**Option A: Show PIN Number**
- Students manually enter the 6-digit code on their phones

**Option B: Display QR Code**
- Students scan the QR code
- PIN automatically fills in
- They only need to enter their Student ID

#### 3. Monitor Check-ins
- Real-time display of students checking in
- See who has marked attendance
- Track session expiry time

### For Students

#### Using PIN Code (Manual Entry)

1. **On Phone (same WiFi):**
   - Open browser
   - Go to `http://TEACHER_IP:5045/Admin/AttendancePinEntry`
   - Enter 6-digit PIN
   - Enter Student ID
   - Click Submit

2. **On School Computer:**
   - Same process using `http://localhost:5045`

#### Using QR Code (Scan)

1. **Scan QR Code** displayed by teacher
2. Browser automatically opens
3. **PIN auto-fills** ?
4. Enter Student ID only
5. Click Submit

## ?? System Flow

```
Teacher Flow:
1. Login to Admin Portal
2. Select Class ? Generate PIN
3. Display QR Code/PIN to Students
4. Monitor Real-time Check-ins
5. End Session or Generate New PIN

Student Flow:
1. Scan QR Code OR Navigate to URL
2. PIN Auto-fills (QR) OR Enter Manually
3. Enter Student ID
4. Submit
5. See Success Message
```

## ?? Database Structure

### AttendanceSession Table
```sql
SessionId (PK)
PinCode (6 digits)
ClassId (FK)
CreatedByTeacherId (FK)
CreatedDate
ExpiryDate
IsActive
SessionType
```

### Attendance Table (Enhanced)
```sql
AttendanceId (PK)
StudentId (FK)
ClassId (FK)
Date
TakenOn (Timestamp)
Status (Present/Absent/Late)
MarkedByTeacherId (FK) - NULL for self-marked
```

## ?? UI Features

### Take Attendance Page
- Class selection dropdown
- Duration selector
- Large PIN code display
- QR code generation
- Session info (enrolled count, expiry)
- Instructions panel

### PIN Entry Page (Mobile)
- Clean, focused interface
- Large digit inputs (50px × 60px)
- Auto-advance on input
- Backspace navigation
- Paste support
- Success animation
- Error handling

### Management Page
- Filter by date/class
- Quick status updates
- Inline editing
- Toast notifications

### Records Page
- Advanced filtering
- Statistics dashboard
- Print support
- Export capabilities

## ?? Security Features

1. **PIN Expiry** - Automatic session timeout
2. **Unique Sessions** - One PIN per class session
3. **Validation** - Check student enrollment
4. **Duplicate Prevention** - No duplicate attendance for same day
5. **Audit Trail** - Track who marked attendance and when

## ?? Configuration

### Session Duration Options
- 15 minutes (quick classes)
- 30 minutes (default)
- 60 minutes (standard)
- 120 minutes (long sessions)

### Customization Points
1. **PIN Length** - Currently 6 digits (can be changed)
2. **QR Code Size** - 200x200px (adjustable)
3. **Session Types** - Class or Individual
4. **Status Options** - Present/Absent/Late/Leave

## ?? Troubleshooting

### QR Code Not Working
1. Ensure mobile device on same WiFi
2. Check firewall allows port 5045
3. Verify IP address is correct
4. Try `http://` not `https://`

### PIN Not Accepting
1. Check PIN hasn't expired
2. Verify student is enrolled in class
3. Ensure no duplicate attendance today
4. Check session is still active

### Mobile Can't Connect
1. Ping server IP from phone
2. Disable Windows Firewall temporarily
3. Check router settings
4. Verify port 5045 is open

## ?? API Endpoints

```csharp
// Generate PIN Session
POST /Admin/GenerateAttendancePin
Parameters: classId, durationMinutes
Returns: sessionId, pinCode, qrUrl, expiryDate

// Submit Attendance
POST /Admin/SubmitAttendancePin
Parameters: pinCode, studentId
Returns: success, message, studentName, className, time

// Update Status
POST /Admin/UpdateAttendanceStatus
Parameters: attendanceId, status
Returns: success, message
```

## ?? Best Practices

1. **Generate PIN at Start of Class**
   - Gives students time to check in
   - Reduces late arrivals

2. **Use QR Codes for Large Classes**
   - Faster than manual entry
   - Reduces errors

3. **Monitor Check-ins in Real-time**
   - Identify missing students
   - Take manual action if needed

4. **End Sessions Promptly**
   - Prevents late check-ins
   - Maintains accuracy

5. **Regular Backups**
   - Export attendance records
   - Keep historical data

## ?? Supported Browsers

- ? Chrome (Desktop & Mobile)
- ? Safari (iOS)
- ? Firefox
- ? Edge
- ? Samsung Internet

## ?? Future Enhancements

- [ ] Geofencing (location-based check-in)
- [ ] Face recognition integration
- [ ] Email/SMS notifications to parents
- [ ] Bulk attendance upload
- [ ] Analytics dashboard
- [ ] Mobile app (native)
- [ ] Bluetooth proximity detection
- [ ] Integration with LMS

## ?? Support

For issues or questions:
1. Check this README
2. Review error messages in browser console
3. Check server logs
4. Contact system administrator

## ?? License

This attendance system is part of the Web Mobile Assignment project.

---

**Version:** 1.0.0  
**Last Updated:** December 2024  
**Framework:** ASP.NET Core 9.0
