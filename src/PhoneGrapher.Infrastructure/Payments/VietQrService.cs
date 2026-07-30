using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Infrastructure.Options;

namespace PhoneGrapher.Infrastructure.Payments;

public sealed class VietQrService(IOptions<VietQrOptions> options) : IVietQrService
{
    private const string NapasGuid = "A000000727";
    private const string ServiceCodeTransferToAccount = "QRIBFTTA";
    private const string CurrencyVnd = "704";
    private const string CountryVn = "VN";

    private readonly VietQrOptions _options = options.Value;

    public string BuildPayload(decimal amount, string memo)
    {
        if (string.IsNullOrWhiteSpace(_options.BankBin))
        {
            throw new InvalidOperationException("Chưa cấu hình VietQr:BankBin.");
        }

        if (string.IsNullOrWhiteSpace(_options.AccountNumber))
        {
            throw new InvalidOperationException("Chưa cấu hình VietQr:AccountNumber.");
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền phải lớn hơn 0.");
        }

        if (string.IsNullOrEmpty(memo) || !memo.All(IsAllowedMemoChar))
        {
            throw new ArgumentException("Nội dung chuyển khoản chỉ được chứa A-Z và 0-9.", nameof(memo));
        }

        var amountText = decimal
            .ToInt64(Math.Round(amount, 0, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

        var beneficiary = Field("00", _options.BankBin) + Field("01", _options.AccountNumber);

        var merchantAccount = Field("00", NapasGuid)
            + Field("01", beneficiary)
            + Field("02", ServiceCodeTransferToAccount);

        var additionalData = Field("08", memo);

        var payload = new StringBuilder()
            .Append(Field("00", "01"))
            .Append(Field("01", "12"))
            .Append(Field("38", merchantAccount))
            .Append(Field("53", CurrencyVnd))
            .Append(Field("54", amountText))
            .Append(Field("58", CountryVn))
            .Append(Field("62", additionalData))
            .Append("6304")
            .ToString();

        return payload + Crc16.Compute(payload);
    }

    private static bool IsAllowedMemoChar(char c) => c is >= 'A' and <= 'Z' or >= '0' and <= '9';

    /// <summary>Dựng một trường TLV: tag 2 chữ số, độ dài 2 chữ số, rồi giá trị.</summary>
    private static string Field(string tag, string value)
    {
        if (value.Length > 99)
        {
            throw new ArgumentException(
                $"Giá trị của tag {tag} dài {value.Length} ký tự, vượt giới hạn 99 ký tự của EMVCo.",
                nameof(value));
        }

        return $"{tag}{value.Length:D2}{value}";
    }
}
