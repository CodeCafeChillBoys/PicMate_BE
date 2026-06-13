namespace PhoneGrapher.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 30;
}

public sealed class VnPayOptions
{
    public const string SectionName = "VnPay";

    public string Version { get; init; } = "2.1.0";
    public string Command { get; init; } = "pay";
    public string TmnCode { get; init; } = string.Empty;
    public string HashSecret { get; init; } = string.Empty;
    public string PaymentUrl { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = string.Empty;
    public string Locale { get; init; } = "vn";
    public string CurrencyCode { get; init; } = "VND";
}

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>OAuth 2.0 Web Client ID lấy từ Google Cloud Console. Trống = chưa bật đăng nhập Google.</summary>
    public string ClientId { get; init; } = string.Empty;
}

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    /// <summary>Bật để gửi mail qua Brevo HTTP API (chạy được trên Render free, không bị chặn cổng như SMTP).</summary>
    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string SenderEmail { get; init; } = string.Empty;
    public string SenderName { get; init; } = "PICMate";
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Tắt khi chưa cấu hình SMTP để dev không bị lỗi; bật khi đã điền Host/credentials.</summary>
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "PICMate";
}
