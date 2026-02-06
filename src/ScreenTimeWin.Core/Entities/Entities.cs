using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScreenTimeWin.Core.Entities;

public class AppIdentity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string ProcessName { get; set; } = string.Empty;
    
    public string? FilePathHash { get; set; }
    
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    
    public string? Category { get; set; }
    public string? IconBase64 { get; set; } // Cache icon in DB
}

public class UsageSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid AppId { get; set; }
    
    [ForeignKey("AppId")]
    public AppIdentity? App { get; set; }
    
    public string WindowTitle { get; set; } = string.Empty;
    public string? SiteDomain { get; set; }
    
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    
    public int DurationSeconds { get; set; }
}

public class DailyAggregate
{
    // Composite Key (DateLocal, AppId, SiteDomain) handled in OnModelCreating
    public string DateLocal { get; set; } = string.Empty; // yyyy-MM-dd
    
    public Guid AppId { get; set; }
    
    [ForeignKey("AppId")]
    public AppIdentity? App { get; set; }
    
    public string? SiteDomain { get; set; }
    
    public int TotalSeconds { get; set; }
}

public class LimitRule
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid AppId { get; set; }
    
    [ForeignKey("AppId")]
    public AppIdentity? App { get; set; }
    
    public int? DailyLimitMinutes { get; set; }
    
    // TimeSpan stored as ticks or string in SQLite usually, but EF Core handles TimeSpan well.
    public TimeSpan? CurfewStartLocal { get; set; }
    public TimeSpan? CurfewEndLocal { get; set; }
    
    public Models.ActionOnLimit ActionOnLimit { get; set; }
    
    public bool Enabled { get; set; } = true;
}

public class GlobalSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class FocusSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public DateTime StartLocal { get; set; }
    public DateTime? EndLocal { get; set; }
    
    public Models.FocusMode FocusMode { get; set; }
    public int? DurationMinutes { get; set; }
}

public class InterceptEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public DateTime TimeUtc { get; set; }
    public Guid AppId { get; set; }
    
    [ForeignKey("AppId")]
    public AppIdentity? App { get; set; }
    
    public string Reason { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
