# ?? Security Testing Guide

## Quick Test Scenarios

### ? Test 1: Unauthenticated Access (Should FAIL)

**Steps:**
1. Open browser in **Incognito/Private mode** (or clear cookies)
2. Try to access these URLs directly:
 - `https://localhost:7079/Admin/Dashboard`
   - `https://localhost:7079/Teacher/TeachDashboard`
   - `https://localhost:7079/Student/StudDashboard`
   - `https://localhost:7079/Parent/Dashboard`

**Expected Result:**
- ? **ALL** should redirect to `/Account/Login`
- ? **NONE** should show the dashboard

**If it fails:**
- Check `Program.cs` has `app.UseAuthentication()` before `app.UseAuthorization()`
- Verify cookies are actually cleared

---

### ? Test 2: Wrong Role Access (Should FAIL)

#### Test 2a: Student tries to access Admin
**Steps:**
1. Login as **Student** (any student account)
2. After login, manually change URL to: `https://localhost:7079/Admin/Dashboard`

**Expected Result:**
- ? Access Denied page OR redirect to login
- ? **NOT** able to view admin dashboard

#### Test 2b: Admin tries to access Student
**Steps:**
1. Login as **Admin**
2. Try URL: `https://localhost:7079/Student/StudDashboard`

**Expected Result:**
- ? Access Denied
- ? **NOT** able to view student dashboard

#### Test 2c: Cross-Role Matrix
Test all combinations:

| Logged In As | Try Accessing | Expected Result |
|--------------|---------------|-----------------|
| Student | `/Admin/Dashboard` | ? Access Denied |
| Student | `/Teacher/TeachDashboard` | ? Access Denied |
| Student | `/Parent/Dashboard` | ? Access Denied |
| Teacher | `/Admin/Dashboard` | ? Access Denied |
| Teacher | `/Student/StudDashboard` | ? Access Denied |
| Teacher | `/Parent/Dashboard` | ? Access Denied |
| Parent | `/Admin/Dashboard` | ? Access Denied |
| Parent | `/Teacher/TeachDashboard` | ? Access Denied |
| Parent | `/Student/StudDashboard` | ? Access Denied |
| Admin | `/Teacher/TeachDashboard` | ? Access Denied |
| Admin | `/Student/StudDashboard` | ? Access Denied |
| Admin | `/Parent/Dashboard` | ? Access Denied |

**All should result in Access Denied!**

---

### ? Test 3: Correct Role Access (Should SUCCEED)

| Logged In As | Can Access | Expected |
|--------------|------------|----------|
| Admin | `/Admin/Dashboard` | ? **SUCCESS** |
| Admin | `/Admin/StudentIndex` | ? **SUCCESS** |
| Admin | `/Admin/TeacherIndex` | ? **SUCCESS** |
| Teacher | `/Teacher/TeachDashboard` | ? **SUCCESS** |
| Teacher | `/Teacher/TeachClasses` | ? **SUCCESS** |
| Student | `/Student/StudDashboard` | ? **SUCCESS** |
| Student | `/Student/StudTakeAttendance` | ? **SUCCESS** |
| Parent | `/Parent/Dashboard` | ? **SUCCESS** |
| Parent | `/Parent/StudentProfile` | ? **SUCCESS** |

**All should load successfully!**

---

### ? Test 4: Public PIN Entry (Should SUCCEED)

**Steps:**
1. **Log out** completely (or use incognito mode)
2. Go to: `https://localhost:7079/Admin/AttendancePinEntry`
3. Or: `https://localhost:7079/Admin/AttendancePinEntry?pin=123456`

**Expected Result:**
- ? **Page loads** without login (for kiosk/shared devices)
- ? Shows PIN entry form
- ? Can submit with valid student ID

**Why this works:**
- Marked with `[AllowAnonymous]`
- Security through PIN code validation
- Needed for shared attendance kiosks

---

### ? Test 5: Session Persistence

**Test 5a: Remember Me**
**Steps:**
1. Login with "Remember Me" **checked**
2. Close browser completely
3. Reopen and go to your dashboard URL

**Expected:**
- ? Still logged in
- ? Can access dashboard

**Test 5b: No Remember Me**
**Steps:**
1. Login with "Remember Me" **unchecked**
2. Close browser completely
3. Reopen and go to your dashboard URL

**Expected:**
- ? Redirected to login
- Session expired

---

### ? Test 6: Logout

**Steps:**
1. Login as any user
2. Access your dashboard (verify it loads)
3. Click Logout
4. Try to access the dashboard URL again

**Expected:**
- ? Logout successful
- ? Dashboard redirects to login
- ? Session cleared

---

## ?? Troubleshooting Failed Tests

### Problem: Unauthenticated access WORKS (should fail)

**Causes:**
1. `[Authorize]` attribute missing
2. Middleware order wrong in `Program.cs`
3. Authentication not configured

**Fix:**
```csharp
// Check Program.cs has this ORDER:
app.UseAuthentication();  // ? Must come FIRST
app.UseAuthorization();   // ? Must come SECOND
```

---

### Problem: Wrong role can access (should fail)

**Causes:**
1. Role name mismatch (case-sensitive!)
2. Role not set during login
3. Multiple roles accidentally granted

**Fix:**
```csharp
// Check role name matches EXACTLY:
[Authorize(Roles = "Admin")]  // ?
[Authorize(Roles = "admin")]  // ? Wrong case
```

---

### Problem: Correct role CANNOT access (should work)

**Causes:**
1. Role not set in claims during login
2. Typo in role name
3. Cookie expired

**Fix:**
Check `Helper.SignIn` includes role:
```csharp
new Claim(ClaimTypes.Role, role)  // Must be present
```

---

### Problem: Access Denied page not showing

**Expected behavior:**
- Some endpoints redirect to login
- Some show "Access Denied" page

**Configure Access Denied page:**
```csharp
// In Program.cs:
options.AccessDeniedPath = "/Account/AccessDenied";
```

Then create the view at: `Views/Account/AccessDenied.cshtml`

---

## ?? Test Results Template

```
SECURITY TEST RESULTS
Date: ___________
Tester: ___________

[ ] Test 1: Unauthenticated Access
    [ ] /Admin/Dashboard ? Redirect to Login
    [ ] /Teacher/TeachDashboard ? Redirect to Login
    [ ] /Student/StudDashboard ? Redirect to Login
    [ ] /Parent/Dashboard ? Redirect to Login

[ ] Test 2: Wrong Role Access
  [ ] Student cannot access Admin
    [ ] Student cannot access Teacher
    [ ] Student cannot access Parent
    [ ] Teacher cannot access Admin
    [ ] Teacher cannot access Student
    [ ] Teacher cannot access Parent
    [ ] Parent cannot access Admin
    [ ] Parent cannot access Teacher
    [ ] Parent cannot access Student
    [ ] Admin cannot access Teacher
    [ ] Admin cannot access Student
  [ ] Admin cannot access Parent

[ ] Test 3: Correct Role Access
    [ ] Admin can access Admin functions
    [ ] Teacher can access Teacher functions
    [ ] Student can access Student functions
    [ ] Parent can access Parent functions

[ ] Test 4: Public PIN Entry
    [ ] AttendancePinEntry loads without login
    [ ] PIN submission works

[ ] Test 5: Session Persistence
    [ ] Remember Me keeps session
    [ ] No Remember Me expires session

[ ] Test 6: Logout
    [ ] Logout clears session
    [ ] Cannot access after logout

OVERALL STATUS: [ ] PASS / [ ] FAIL

Notes:
_____________________________________________
_____________________________________________
_____________________________________________
```

---

## ?? Priority Tests

If you're short on time, test these **high-priority scenarios**:

### Critical (Must Test):
1. ? **Unauthenticated cannot access** `/Admin/Dashboard`
2. ? **Student cannot access** `/Admin/Dashboard`
3. ? **Admin CAN access** `/Admin/Dashboard`
4. ? **Logout works** properly

### Important (Should Test):
5. ? Teacher/Parent/Student cannot access each other's functions
6. ? PIN entry works without login

### Nice to Have (If Time):
7. ? Remember Me feature
8. ? Session expiration

---

## ?? Pass/Fail Criteria

### ? **PASS** if:
- Unauthenticated users **cannot** access protected pages
- Users **cannot** access other roles' pages
- Users **CAN** access their own role's pages
- Logout properly clears session
- PIN entry works without login

### ? **FAIL** if:
- Unauthenticated access works
- Cross-role access works
- Logout doesn't clear session
- Correct role **cannot** access their pages

---

## ?? Testing Tips

### Use Different Browsers
- Chrome (normal mode)
- Chrome (incognito) - for unauthenticated tests
- Firefox - verify consistency

### Use Browser Dev Tools
- **Application tab** ? Cookies ? Check `.AspNetCore.Cookies`
- **Network tab** ? See redirects (302) vs. access denied (403)
- **Console** ? Check for errors

### Clear State Between Tests
```javascript
// Run in browser console:
document.cookie.split(";").forEach(c => {
    document.cookie = c.trim().split("=")[0] + "=;expires=Thu, 01 Jan 1970 00:00:00 UTC;path=/";
});
location.reload();
```

---

## ? Quick Verification

Run this command to verify attributes are present:

```powershell
# PowerShell - Search for [Authorize] attributes
Get-ChildItem .\Controllers\*.cs -Recurse | Select-String "\[Authorize"
```

**Expected output:**
```
AdminController.cs:14:    [Authorize(Roles = "Admin")]
TeacherController.cs:9:    [Authorize(Roles = "Teacher")]
StudentController.cs:10:    [Authorize(Roles = "Student")]
ParentController.cs:8:    [Authorize(Roles = "Parent")]
```

---

**Happy Testing! ???**

