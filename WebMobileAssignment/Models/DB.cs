using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebMobileAssignment.Models;

public class DB(DbContextOptions<DB> options) : DbContext(options)
{
    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Parent> Parents { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Attendance> Attendances { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    base.OnModelCreating(modelBuilder);

    // Configure User entity
        modelBuilder.Entity<User>()
          .HasIndex(u => u.Email)
            .IsUnique();

        // Configure Parent-Student one-to-many relationship
        modelBuilder.Entity<Student>()
     .HasOne(s => s.Parent)
  .WithMany(p => p.Students)
        .HasForeignKey(s => s.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure cascade delete behavior for User relationships
      modelBuilder.Entity<Admin>()
         .HasOne(a => a.User)
  .WithMany()
 .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Teacher>()
   .HasOne(t => t.User)
     .WithMany()
   .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Student>()
     .HasOne(s => s.User)
      .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Parent>()
            .HasOne(p => p.User)
     .WithMany()
        .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [Key]
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
  [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string UserType { get; set; } = string.Empty; // Admin / Teacher / Student / Parent

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}

public class Admin
{
    [Key]
    [MaxLength(20)]
    public string AdminId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}

public class Teacher
{
    [Key]
    [MaxLength(20)]
    public string TeacherId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
  public string? SubjectTeach { get; set; }

  public DateTime HireDate { get; set; }

    // Navigation property
  public ICollection<Class> Classes { get; set; } = new List<Class>();
}

public class Student
{
    [Key]
    [MaxLength(20)]
    public string StudentId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [MaxLength(20)]
    public string? ClassId { get; set; }

    [ForeignKey(nameof(ClassId))]
    public Class? Class { get; set; }

    [MaxLength(20)]
    public string? ParentId { get; set; }

    [ForeignKey(nameof(ParentId))]
    public Parent? Parent { get; set; }

    public DateTime DateOfBirth { get; set; }

    [MaxLength(10)]
    public string? Gender { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

public class Parent
{
    [Key]
    [MaxLength(20)]
    public string ParentId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();
}

public class Class
{
    [Key]
    [MaxLength(20)]
    public string ClassId { get; set; } = string.Empty;

    [Required]
[MaxLength(100)]
    public string ClassName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? TeacherId { get; set; }

    [ForeignKey(nameof(TeacherId))]
    public Teacher? Teacher { get; set; }

    [MaxLength(50)]
    public string? RoomNumber { get; set; }

    [MaxLength(200)]
    public string? ScheduleInfo { get; set; }

    // Navigation properties
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

public class Attendance
{
    [Key]
    [MaxLength(20)]
    public string AttendanceId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string StudentId { get; set; } = string.Empty;

    [ForeignKey(nameof(StudentId))]
public Student Student { get; set; } = null!;

    [MaxLength(20)]
    public string ClassId { get; set; } = string.Empty;

    [ForeignKey(nameof(ClassId))]
    public Class Class { get; set; } = null!;

    public DateTime Date { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty; // Present / Absent / Late / Excused

    [MaxLength(20)]
    public string? MarkedByTeacherId { get; set; }

    [ForeignKey(nameof(MarkedByTeacherId))]
    public Teacher? MarkedByTeacher { get; set; }
}