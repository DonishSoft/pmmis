using PMMIS.Web.Extensions;

namespace PMMIS.Tests.Extensions;

/// <summary>
/// Тесты для DateTimeExtensions
/// </summary>
public class DateTimeExtensionsTests
{
    [Fact]
    public void ToUtc_ShouldSetUtcKind_WhenDateTimeProvided()
    {
        // Arrange
        var localDate = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Local);
        
        // Act
        var result = localDate.ToUtc();
        
        // Assert
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(localDate.Year, result.Year);
        Assert.Equal(localDate.Month, result.Month);
        Assert.Equal(localDate.Day, result.Day);
    }

    [Fact]
    public void ToUtc_ShouldReturnSameDateTime_WhenAlreadyUtc()
    {
        // Arrange
        var utcDate = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        
        // Act
        var result = utcDate.ToUtc();
        
        // Assert
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(utcDate, result);
    }

    [Fact]
    public void ToUtc_NullableDateTime_ShouldReturnNull_WhenNull()
    {
        // Arrange
        DateTime? nullDate = null;
        
        // Act
        var result = nullDate.ToUtc();
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToUtc_NullableDateTime_ShouldSetUtcKind_WhenHasValue()
    {
        // Arrange
        DateTime? date = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Local);
        
        // Act
        var result = date.ToUtc();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
    }

    [Fact]
    public void ToUtc_ShouldHandleUnspecifiedKind()
    {
        // Arrange
        var unspecifiedDate = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);
        
        // Act
        var result = unspecifiedDate.ToUtc();
        
        // Assert
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }
}
