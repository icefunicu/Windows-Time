using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Core.Models;
using ScreenTimeWin.Data;
using ScreenTimeWin.Service;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace ScreenTimeWin.Tests;

/// <summary>
/// FocusManager 单元测试
/// </summary>
public class FocusManagerTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbContextOptions<ScreenTimeDbContext> _contextOptions;
    private readonly DataRepository _repository;
    private readonly FocusManager _focusManager;

    public FocusManagerTests()
    {
        // 使用内存数据库
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<ScreenTimeDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ScreenTimeDbContext(_contextOptions);
        context.Database.EnsureCreated();

        var factory = new TestDbContextFactory(_contextOptions);
        _repository = new DataRepository(factory);
        _focusManager = new FocusManager(_repository);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void StartFocus_ShouldActivateFocusSession()
    {
        // Arrange
        var appIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        _focusManager.StartFocus(25, appIds, FocusType.Whitelist, FocusMode.Pomodoro);

        // Assert
        Assert.True(_focusManager.IsActive);
        Assert.True(_focusManager.IsFocusActive);
        Assert.NotNull(_focusManager.FocusStartTime);
        Assert.NotNull(_focusManager.FocusEndTime);
    }

    [Fact]
    public void StopFocus_ShouldDeactivateFocusSession()
    {
        // Arrange
        _focusManager.StartFocus(25, new List<Guid>(), FocusType.Whitelist);

        // Act
        _focusManager.StopFocus();

        // Assert
        Assert.False(_focusManager.IsActive);
        Assert.False(_focusManager.IsFocusActive);
    }

    [Fact]
    public void IsAllowed_WhenNotActive_ShouldReturnTrue()
    {
        // Assert
        Assert.True(_focusManager.IsAllowed(Guid.NewGuid()));
    }

    [Fact]
    public void IsAllowed_Whitelist_ShouldAllowOnlyWhitelistedApps()
    {
        // Arrange
        var allowedAppId = Guid.NewGuid();
        var blockedAppId = Guid.NewGuid();

        _focusManager.StartFocus(25, new List<Guid> { allowedAppId }, FocusType.Whitelist);

        // Assert
        Assert.True(_focusManager.IsAllowed(allowedAppId));
        Assert.False(_focusManager.IsAllowed(blockedAppId));
    }

    [Fact]
    public void IsAllowed_Blacklist_ShouldBlockOnlyBlacklistedApps()
    {
        // Arrange
        var blockedAppId = Guid.NewGuid();
        var allowedAppId = Guid.NewGuid();

        _focusManager.StartFocus(25, new List<Guid> { blockedAppId }, FocusType.Blacklist);

        // Assert
        Assert.False(_focusManager.IsAllowed(blockedAppId));
        Assert.True(_focusManager.IsAllowed(allowedAppId));
    }

    [Fact]
    public void RemainingSeconds_WhenActive_ShouldReturnPositiveValue()
    {
        // Arrange
        _focusManager.StartFocus(25, new List<Guid>(), FocusType.Whitelist);

        // Act
        var remaining = _focusManager.RemainingSeconds;

        // Assert
        Assert.True(remaining > 0);
        Assert.True(remaining <= 25 * 60); // 最多25分钟
    }

    [Fact]
    public void WhitelistAppIds_ShouldContainAddedApps()
    {
        // Arrange
        var appId1 = Guid.NewGuid();
        var appId2 = Guid.NewGuid();

        // Act
        _focusManager.StartFocus(25, new List<Guid> { appId1, appId2 }, FocusType.Whitelist);

        // Assert
        Assert.Contains(appId1, _focusManager.WhitelistAppIds);
        Assert.Contains(appId2, _focusManager.WhitelistAppIds);
    }
}

/// <summary>
/// 测试用 DbContext 工厂
/// </summary>
internal class TestDbContextFactory : IDbContextFactory<ScreenTimeDbContext>
{
    private readonly DbContextOptions<ScreenTimeDbContext> _options;

    public TestDbContextFactory(DbContextOptions<ScreenTimeDbContext> options)
    {
        _options = options;
    }

    public ScreenTimeDbContext CreateDbContext()
    {
        return new ScreenTimeDbContext(_options);
    }
}
