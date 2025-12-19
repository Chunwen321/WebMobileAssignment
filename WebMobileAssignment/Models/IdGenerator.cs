namespace WebMobileAssignment.Models;

public static class IdGenerator
{
    private static readonly object _lock = new object();
    private static int _userCounter = 1;
 private static int _adminCounter = 1;
    private static int _teacherCounter = 1;
    private static int _studentCounter = 1;
    private static int _parentCounter = 1;
    private static int _classCounter = 1;
    private static int _enrollmentCounter = 1;
    private static int _attendanceCounter = 1;
    private static int _leaveApplicationCounter = 1;
    private static int _notificationCounter = 1;
    private static int _sessionCounter = 1;
    private static readonly Random _random = new Random();

    public static string GenerateUserId(DB db)
 {
        lock (_lock)
      {
 string id;
       do
    {
                id = $"U{_userCounter:D4}";
       _userCounter++;
      } while (db.Users.Any(u => u.UserId == id));
      return id;
      }
    }

    public static string GenerateAdminId(DB db)
    {
        lock (_lock)
        {
      string id;
            do
        {
                id = $"A{_adminCounter:D4}";
 _adminCounter++;
        } while (db.Admins.Any(a => a.AdminId == id));
 return id;
   }
    }

    public static string GenerateTeacherId(DB db)
    {
    lock (_lock)
   {
            string id;
    do
        {
   id = $"T{_teacherCounter:D4}";
  _teacherCounter++;
    } while (db.Teachers.Any(t => t.TeacherId == id));
            return id;
        }
    }

    public static string GenerateStudentId(DB db)
    {
        lock (_lock)
      {
            string id;
    do
        {
       id = $"S{_studentCounter:D4}";
        _studentCounter++;
   } while (db.Students.Any(s => s.StudentId == id));
            return id;
        }
    }

    public static string GenerateParentId(DB db)
    {
        lock (_lock)
        {
   string id;
            do
            {
    id = $"P{_parentCounter:D4}";
        _parentCounter++;
       } while (db.Parents.Any(p => p.ParentId == id));
   return id;
        }
    }

    public static string GenerateClassId(DB db)
    {
        lock (_lock)
        {
            string id;
  do
            {
      id = $"C{_classCounter:D4}";
       _classCounter++;
            } while (db.Classes.Any(c => c.ClassId == id));
        return id;
        }
  }

    public static string GenerateEnrollmentId(DB db)
    {
        lock (_lock)
        {
            string id;
            do
            {
                id = $"E{_enrollmentCounter:D5}";
                _enrollmentCounter++;
            } while (db.Enrollments.Any(e => e.EnrollmentId == id));
            return id;
        }
    }

    public static string GenerateAttendanceId(DB db)
    {
        lock (_lock)
      {
    string id;
do
      {
           id = $"ATT{_attendanceCounter:D4}";
            _attendanceCounter++;
  } while (db.Attendances.Any(a => a.AttendanceId == id));
return id;
        }
    }

    public static string GenerateLeaveApplicationId(DB db)
    {
        lock (_lock)
        {
            string id;
            do
            {
                id = $"L{_leaveApplicationCounter:D4}";
                _leaveApplicationCounter++;
            } while (db.LeaveApplications.Any(l => l.LeaveId == id));
            return id;
        }
    }

    public static string GenerateNotificationId(DB db)
    {
        lock (_lock)
        {
            string id;
            do
            {
                id = $"N{_notificationCounter:D5}";
                _notificationCounter++;
            } while (db.Notifications.Any(n => n.NotificationId == id));
            return id;
        }
    }

    public static string GenerateSessionId(DB db)
    {
        lock (_lock)
        {
            string id;
            do
            {
                id = $"AS{_sessionCounter:D5}";
                _sessionCounter++;
            } while (db.AttendanceSessions.Any(s => s.SessionId == id));
            return id;
        }
    }

    public static string GenerateAttendancePinCode(DB db)
    {
        lock (_lock)
        {
            string pinCode;
            do
            {
                pinCode = _random.Next(100000, 999999).ToString();
            } while (db.AttendanceSessions.Any(s => s.PinCode == pinCode && s.IsActive));
            return pinCode;
        }
    }

    // Initialize counters from existing data
  public static void InitializeCounters(DB db)
    {
        lock (_lock)
        {
       _userCounter = GetNextCounter(db.Users.Select(u => u.UserId).ToList(), "U");
          _adminCounter = GetNextCounter(db.Admins.Select(a => a.AdminId).ToList(), "A");
 _teacherCounter = GetNextCounter(db.Teachers.Select(t => t.TeacherId).ToList(), "T");
     _studentCounter = GetNextCounter(db.Students.Select(s => s.StudentId).ToList(), "S");
            _parentCounter = GetNextCounter(db.Parents.Select(p => p.ParentId).ToList(), "P");
            _classCounter = GetNextCounter(db.Classes.Select(c => c.ClassId).ToList(), "C");
            _enrollmentCounter = GetNextCounter(db.Enrollments.Select(e => e.EnrollmentId).ToList(), "E");
            _attendanceCounter = GetNextCounter(db.Attendances.Select(a => a.AttendanceId).ToList(), "ATT");
            _leaveApplicationCounter = GetNextCounter(db.LeaveApplications.Select(l => l.LeaveId).ToList(), "LEV");
            _notificationCounter = GetNextCounter(db.Notifications.Select(n => n.NotificationId).ToList(), "N");
            _sessionCounter = GetNextCounter(db.AttendanceSessions.Select(s => s.SessionId).ToList(), "AS");
        }
    }

    private static int GetNextCounter(List<string> existingIds, string prefix)
    {
  if (!existingIds.Any())
 return 1;

     var maxNumber = existingIds
  .Where(id => id.StartsWith(prefix))
            .Select(id => int.TryParse(id.Substring(prefix.Length), out int num) ? num : 0)
         .DefaultIfEmpty(0)
        .Max();

        return maxNumber + 1;
    }
}
