using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Infrastructure.Options;

namespace PhoneGrapher.Infrastructure.Payments;

public sealed class VnPayService(IOptions<VnPayOptions> options) : IVnPayService
{
    private readonly VnPayOptions _options = options.Value;

    public string CreatePaymentUrl(PaymentTransaction payment, string clientIpAddress)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = _options.Version,
            ["vnp_Command"] = _options.Command,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = decimal.ToInt64(payment.Amount * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = _options.CurrencyCode,
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(clientIpAddress) ? "127.0.0.1" : clientIpAddress,
            ["vnp_Locale"] = _options.Locale,
            ["vnp_OrderInfo"] = $"Thanh toan booking {payment.BookingId}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TxnRef"] = payment.TransactionCode
        };

        var query = BuildQuery(parameters);
        var secureHash = ComputeHmacSha512(_options.HashSecret, query);

        return $"{_options.PaymentUrl}?{query}&vnp_SecureHash={secureHash}";
    }

    public bool VerifyCallback(IReadOnlyDictionary<string, string> query)
    {
        if (!query.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
        {
            return false;
        }

        var filtered = query
            .Where(x => !string.Equals(x.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase)
                && x.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        var hashData = BuildQuery(filtered);
        var expected = ComputeHmacSha512(_options.HashSecret, hashData);
        return string.Equals(expected, secureHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join("&", parameters
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
    }
}
