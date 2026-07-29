using System.Globalization;

namespace PhoneGrapher.Infrastructure.Services.Analytics;

/// <summary>
/// Khoảng thời gian của dashboard cùng cách chia mốc.
/// Cửa sổ trượt: luôn kết thúc ở thời điểm hiện tại, lùi về sau đúng độ dài của khoảng,
/// khớp với cách diễn đạt "30 ngày qua" trên giao diện.
/// Mọi khoảng đều chia thành 12 đoạn đều nhau.
/// </summary>
public sealed record AnalyticsWindow(
    string Range,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset PreviousFromUtc)
{
    public const int BucketCount = 12;

    /// <summary>Giờ Việt Nam, dùng để hiển thị nhãn ngày cho đúng múi giờ người xem.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public TimeSpan Duration => ToUtc - FromUtc;

    public TimeSpan BucketSize => Duration / BucketCount;

    public static AnalyticsWindow Create(string? range, DateTimeOffset nowUtc)
    {
        var normalized = (range ?? string.Empty).Trim().ToLowerInvariant();

        // Giá trị lạ rơi về 30d thay vì ném lỗi: dashboard không nên chết vì một tham số sai.
        var duration = normalized switch
        {
            "today" => TimeSpan.FromDays(1),
            "7d" => TimeSpan.FromDays(7),
            "quarter" => TimeSpan.FromDays(90),
            _ => TimeSpan.FromDays(30)
        };

        var canonical = normalized switch
        {
            "today" or "7d" or "quarter" => normalized,
            _ => "30d"
        };

        var from = nowUtc - duration;

        return new AnalyticsWindow(canonical, from, nowUtc, from - duration);
    }

    /// <summary>12 đoạn liên tiếp phủ kín khoảng, đoạn cuối kết thúc đúng tại ToUtc.</summary>
    public IReadOnlyList<AnalyticsBucket> Buckets()
    {
        var size = BucketSize;

        return Enumerable.Range(0, BucketCount)
            .Select(i =>
            {
                var start = FromUtc + size * i;
                // Đoạn cuối lấy đúng ToUtc để không hụt vài tick do làm tròn.
                var end = i == BucketCount - 1 ? ToUtc : FromUtc + size * (i + 1);
                return new AnalyticsBucket(start, end, FormatLabel(start));
            })
            .ToArray();
    }

    /// <summary>Khoảng "Hôm nay" đọc theo giờ, các khoảng dài hơn đọc theo ngày.</summary>
    private string FormatLabel(DateTimeOffset startUtc)
    {
        var local = startUtc.ToOffset(VietnamOffset);

        return Range == "today"
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("dd/MM", CultureInfo.InvariantCulture);
    }
}

public sealed record AnalyticsBucket(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Label);
