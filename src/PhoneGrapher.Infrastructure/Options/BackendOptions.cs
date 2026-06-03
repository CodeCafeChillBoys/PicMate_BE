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
