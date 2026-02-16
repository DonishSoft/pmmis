namespace PMMIS.Web.Extensions;

/// <summary>
/// Расширения для работы с датами
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Преобразует DateTime в UTC, устанавливая DateTimeKind.Utc
    /// </summary>
    public static DateTime ToUtc(this DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    /// <summary>
    /// Преобразует nullable DateTime в UTC
    /// </summary>
    public static DateTime? ToUtc(this DateTime? date)
    {
        return date.HasValue ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc) : null;
    }
}
