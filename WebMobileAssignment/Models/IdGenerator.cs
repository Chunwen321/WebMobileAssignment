namespace WebMobileAssignment.Models;

public static class IdGenerator
{
    private static readonly object _lock = new object();
    private static int _userCounter = 1;
    // REMOVED: private static int _adminCounter = 1;
    private static int _teacherCounter = 1;
    private static int _studentCounter = 1;
    private static int _parentCounter = 1;
    private static int _subjectCounter = 1;
    private static int _classCounter = 1;
    private static int _attendanceCounter = 1;
    private static int _notificationCounter = 1;

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

    // REMOVED: GenerateAdminId method

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

    public static string GenerateSubjectId(DB db)
    {
        lock (_lock)
        {
            string id;
            do
            {
                id = $"SUB{_subjectCounter:D4}";
                _subjectCounter++;
            } while (db.Subjects.Any(s => s.SubjectId == id));
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

    public static string GenerateNotificationId(DB db)
    {
        lock (_lock)
        {
            string id;
            do
            {
                id = $"NOT{_notificationCounter:D4}";
                _notificationCounter++;
            } while (db.Notifications.Any(n => n.NotificationId == id));
            return id;
        }
    }

    // Initialize counters from existing data
    public static void InitializeCounters(DB db)
    {
        lock (_lock)
        {
            _userCounter = GetNextCounter(db.Users.Select(u => u.UserId).ToList(), "U");
            // REMOVED: _adminCounter initialization
            _teacherCounter = GetNextCounter(db.Teachers.Select(t => t.TeacherId).ToList(), "T");
            _studentCounter = GetNextCounter(db.Students.Select(s => s.StudentId).ToList(), "S");
            _parentCounter = GetNextCounter(db.Parents.Select(p => p.ParentId).ToList(), "P");
            _subjectCounter = GetNextCounter(db.Subjects.Select(s => s.SubjectId).ToList(), "SUB");
            _classCounter = GetNextCounter(db.Classes.Select(c => c.ClassId).ToList(), "C");
            _attendanceCounter = GetNextCounter(db.Attendances.Select(a => a.AttendanceId).ToList(), "ATT");
            _notificationCounter = GetNextCounter(db.Notifications.Select(n => n.NotificationId).ToList(), "NOT");
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