using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyLeasing.API.Models;

[Table("Notification")]
public partial class Notification
{
    [Key]
    [Column("NotificationID")]
    public int NotificationId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Required]
    [StringLength(500)]
    public string Message { get; set; } = null!;

    // LeaseUpdate / MaintenanceUpdate / PaymentReminder / General
    [StringLength(50)]
    public string? NotificationType { get; set; }

    // Read / Unread
    [StringLength(20)]
    public string Status { get; set; } = "Unread";

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    [InverseProperty("Notifications")]
    public virtual User User { get; set; } = null!;
}

[Table("Feedback")]
public partial class Feedback
{
    [Key]
    [Column("FeedbackID")]
    public int FeedbackId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Column("UnitID")]
    public int UnitId { get; set; }

    public int? Rating { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    public bool IsVisible { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    [InverseProperty("Feedbacks")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("UnitId")]
    [InverseProperty("Feedbacks")]
    public virtual Unit Unit { get; set; } = null!;
}

[Table("Log")]
public partial class Log
{
    [Key]
    [Column("LogID")]
    public int LogId { get; set; }

    [Column("UserID")]
    public int? UserId { get; set; }

    [StringLength(100)]
    public string? Action { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }

    // Info / Warning / Error
    [StringLength(20)]
    public string? LogLevel { get; set; }

    // Web / API
    [StringLength(20)]
    public string? Source { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    [InverseProperty("Logs")]
    public virtual User? User { get; set; }
}

[Table("PasswordResetCode")]
public partial class PasswordResetCode
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(6)]
    public string Code { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("Document")]
public partial class Document
{
    [Key]
    [Column("DocumentID")]
    public int DocumentId { get; set; }

    [Column("UserID")]
    public int? UserId { get; set; }

    [Column("ApplicationID")]
    public int? ApplicationId { get; set; }

    [Required]
    [StringLength(200)]
    public string FileName { get; set; } = null!;

    [StringLength(50)]
    public string? FileType { get; set; }

    [StringLength(500)]
    public string? StoragePath { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UploadedAt { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    [InverseProperty("Documents")]
    public virtual User? User { get; set; }

    [ForeignKey("ApplicationId")]
    [InverseProperty("Documents")]
    public virtual LeaseApplication? Application { get; set; }
}
