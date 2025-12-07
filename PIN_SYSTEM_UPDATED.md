# ?? PIN Code System - Updated Implementation

## ? Changes Made

### 1. **One-Time PIN Generation Per Class**

**Previous Behavior:**
- Teachers could generate multiple PIN codes for the same class
- No restriction on regeneration

**New Behavior:**
- ? **Each class can only have ONE PIN code** (generated once only)
- ? PIN code is **permanently stored** in the database
- ? Attempting to generate again shows error: *"PIN code has already been generated for this class. Each class can only have one PIN code."*

---

### 2. **Removed Duration Selection**

**Previous Behavior:**
- Teachers could choose expiration time (15 min, 30 min, 60 min, 120 min)
- PIN would expire after selected duration

**New Behavior:**
- ? **No duration selector** in the UI
- ? PIN is **valid only during class hours** (based on class schedule)
- ? Expiry is automatically set to class end time

---

### 3. **Class Schedule Validation**

**Requirements:**
- Class MUST have:
  - ? **Day** (e.g., Monday, Tuesday)
  - ? **Start Time** (e.g., 14:00)
  - ? **End Time** (e.g., 16:00)

**Validation on PIN Generation:**
```
If class schedule is not set:
  ? Error: "Class schedule not configured. Please set class day and time first."
```

---

### 4. **Attendance Time Validation**

**Previous Behavior:**
- Students could submit PIN anytime before expiry

**New Behavior:**
Students can ONLY mark attendance if:

? **Current time is within class hours**
```
Example:
Class: Monday 14:00 - 16:00
Current time: Tuesday 10:00
? Error: "Attendance can only be taken during class hours (14:00 - 16:00). Current time: 10:00 AM"
```

? **Current day matches class day**
```
Example:
Class scheduled for: Wednesday
Current day: Thursday
? Error: "This class is scheduled for Wednesday, not Thursday"
```

---

## ?? Updated Database Records

### AttendanceSession Table

**Each class will have ONE record:**

| SessionId | PinCode | ClassId | CreatedDate | ExpiryDate | IsActive |
|-----------|---------|---------|-------------|------------|----------|
| SESSION00001 | 123456 | CLASS001 | 2024-12-03 14:00 | 2024-12-03 16:00 | true |
| SESSION00002 | 789012 | CLASS002 | 2024-12-03 10:00 | 2024-12-03 12:00 | true |

**Key Points:**
- ? One row per class (cannot be regenerated)
- ? `ExpiryDate` = Class end time
- ? PIN remains in database forever (for record keeping)

---

## ?? User Flow

### Teacher Workflow

**1. Generate PIN (First Time)**
```
1. Navigate to: /Admin/AttendanceTake
2. Select Class: "Mathematics 101"
3. Click: "Generate PIN Code"

Result:
  ? PIN created: 123456
  ? QR code displayed
  ? Valid only during class hours
```

**2. Attempt to Generate Again (Blocked)**
```
1. Navigate to: /Admin/AttendanceTake  
2. Select Same Class: "Mathematics 101"
3. Click: "Generate PIN Code"

Result:
  ? Error: "PIN code has already been generated for this class. 
             Each class can only have one PIN code."
```

---

### Student Workflow

**Scenario 1: During Class Hours (Valid)**
```
Class Schedule: Monday 14:00 - 16:00
Current Time: Monday 14:30

1. Open: http://SERVER/Admin/AttendancePinEntry?pin=123456
2. PIN auto-fills
3. Enter Student ID: S0001
4. Click: Submit

Result:
  ? "Attendance marked successfully for John Doe"
```

**Scenario 2: Outside Class Hours (Blocked)**
```
Class Schedule: Monday 14:00 - 16:00
Current Time: Monday 18:00

1. Open: http://SERVER/Admin/AttendancePinEntry?pin=123456
2. PIN auto-fills
3. Enter Student ID: S0001
4. Click: Submit

Result:
  ? "Attendance can only be taken during class hours (14:00 - 16:00). 
      Current time: 06:00 PM"
```

**Scenario 3: Wrong Day (Blocked)**
```
Class Schedule: Wednesday 14:00 - 16:00
Current Time: Friday 14:30

1. Open: http://SERVER/Admin/AttendancePinEntry?pin=123456
2. PIN auto-fills
3. Enter Student ID: S0001
4. Click: Submit

Result:
  ? "This class is scheduled for Wednesday, not Friday"
```

---

## ?? Validation Rules Summary

| Check | Rule | Error Message |
|-------|------|---------------|
| **Class Schedule** | Must have Day, StartTime, EndTime | "Class schedule not configured. Please set class day and time first." |
| **PIN Existence** | Cannot generate if PIN already exists | "PIN code has already been generated for this class. Each class can only have one PIN code." |
| **Current Time** | Must be between StartTime and EndTime | "Attendance can only be taken during class hours (XX:XX - XX:XX). Current time: XX:XX" |
| **Current Day** | Must match scheduled day | "This class is scheduled for [Day], not [CurrentDay]" |
| **Student Enrollment** | Student must be enrolled in class | "Student not enrolled in this class" |
| **Duplicate Check** | No duplicate attendance for same day | "Attendance already marked for today" |

---

## ?? Updated UI

### AttendanceTake.cshtml

**Removed:**
- ? Duration dropdown (15 min, 30 min, 60 min, 120 min)
- ? "End Session" button
- ? "New Session" button
- ? Expiry time display

**Added:**
- ? Alert: "PIN can only be generated once per class and is valid during class hours only."
- ? Display: "Class Time: Monday 14:00-16:00" (instead of expiry)
- ? Warning: "This PIN is valid only during class hours and cannot be regenerated."

**Behavior Changes:**
- After PIN generation, the "Select Class" card is hidden
- Only PIN display remains (no option to create new session)

---

## ?? Technical Implementation

### Controller Changes: `AdminController.cs`

#### `GenerateAttendancePin` Method

**Added Validations:**
```csharp
// Check class schedule exists
if (!classEntity.StartTime.HasValue || !classEntity.EndTime.HasValue || string.IsNullOrEmpty(classEntity.Day))
    return Json(new { success = false, message = "Class schedule not configured..." });

// Check if PIN already exists (one per class)
var existingSession = await _context.AttendanceSessions
    .FirstOrDefaultAsync(s => s.ClassId == classId);

if (existingSession != null)
    return Json(new { success = false, message = "PIN code has already been generated..." });
```

**Expiry Calculation:**
```csharp
// Set expiry to class end time
var today = DateTime.Today;
var classEndTime = today.Add(classEntity.EndTime.Value);

// If class already ended today, set to next week's class
if (DateTime.Now > classEndTime)
{
    classEndTime = classEndTime.AddDays(7);
}
```

#### `SubmitAttendancePin` Method

**Added Time Validations:**
```csharp
// Check current time is within class hours
var currentTime = DateTime.Now.TimeOfDay;
var classStartTime = session.Class.StartTime.Value;
var classEndTime = session.Class.EndTime.Value;

if (currentTime < classStartTime || currentTime > classEndTime)
{
    return Json(new { 
        success = false, 
        message = $"Attendance can only be taken during class hours..." 
    });
}

// Check current day matches class day
var currentDayOfWeek = DateTime.Now.DayOfWeek.ToString();
if (!string.IsNullOrEmpty(session.Class.Day) && 
    !session.Class.Day.Equals(currentDayOfWeek, StringComparison.OrdinalIgnoreCase))
{
    return Json(new { 
        success = false, 
        message = $"This class is scheduled for {session.Class.Day}..." 
    });
}
```

---

## ?? Testing Checklist

### Test 1: Generate PIN for New Class
- [ ] Select class without existing PIN
- [ ] Click "Generate PIN Code"
- [ ] Verify PIN displays
- [ ] Verify QR code appears
- [ ] Verify class time shows instead of expiry

### Test 2: Attempt to Regenerate PIN
- [ ] Select same class again
- [ ] Click "Generate PIN Code"
- [ ] Verify error message: "PIN code has already been generated..."

### Test 3: Submit During Class Hours
- [ ] Set system time to match class schedule
- [ ] Open PIN entry page
- [ ] Enter valid Student ID
- [ ] Click Submit
- [ ] Verify: "Attendance marked successfully"

### Test 4: Submit Outside Class Hours
- [ ] Set system time outside class schedule
- [ ] Open PIN entry page
- [ ] Enter valid Student ID
- [ ] Click Submit
- [ ] Verify error: "Attendance can only be taken during class hours..."

### Test 5: Submit on Wrong Day
- [ ] Set system date to different day than class schedule
- [ ] Open PIN entry page
- [ ] Enter valid Student ID
- [ ] Click Submit
- [ ] Verify error: "This class is scheduled for..."

### Test 6: Class Without Schedule
- [ ] Create class without Day/StartTime/EndTime
- [ ] Try to generate PIN
- [ ] Verify error: "Class schedule not configured..."

---

## ?? Notes for Administrators

### Setting Up Classes for PIN System

**Classes MUST have:**
1. **Day** - Select from: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
2. **Start Time** - e.g., 14:00
3. **End Time** - e.g., 16:00

**How to Set:**
```
1. Go to: Class Management ? Manage Classes
2. Click: "Edit" on a class
3. Fill in: Day, Start Time, End Time
4. Save
```

### PIN Management

**Important Points:**
- ? Generate PIN **before class starts**
- ? PIN is **permanent** (cannot be changed)
- ? One PIN per class (no regeneration)
- ? Students can use PIN **only during class hours**
- ? PIN remains in database for **record keeping**

**If you need to change PIN:**
1. Delete the existing `AttendanceSession` record from database
2. Generate new PIN (only do this if absolutely necessary!)

---

## ?? Security Improvements

1. **One-Time Generation**
   - Prevents multiple PIN sessions for same class
   - Reduces confusion for students

2. **Time-Based Validation**
   - Students cannot mark attendance outside class hours
   - Prevents early/late submissions

3. **Day Validation**
   - Ensures attendance is only for scheduled class days
   - Prevents mistakes on wrong days

4. **Database Record**
   - All PIN sessions are permanently logged
   - Audit trail for accountability

---

## ?? Deployment Steps

1. ? **Stop running application**
   ```
   taskkill /F /IM WebMobileAssignment.exe
   ```

2. ? **Build application**
   ```
   dotnet build
   ```

3. ? **Run application**
   ```
   dotnet run --launch-profile LAN
   ```

4. ? **Test the system**
   - Generate PIN for a class
   - Try to regenerate (should fail)
   - Test PIN entry during/outside class hours

---

## ?? Troubleshooting

### Error: "Class schedule not configured"
**Solution:** Go to Class Management and set Day, Start Time, and End Time for the class

### Error: "PIN code has already been generated"
**Solution:** This is expected behavior. Each class can only have one PIN. If you need to reset, contact administrator.

### Error: "Attendance can only be taken during class hours"
**Solution:** Wait until class time OR adjust your system clock for testing

### Error: "This class is scheduled for [different day]"
**Solution:** Attendance can only be marked on the scheduled day

---

**Version:** 2.0.0 (Updated PIN System)  
**Last Updated:** December 2024  
**Changes:** One-time PIN generation + Class hours validation
