using System.Globalization;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Payments;

// Namespace PhoneGrapher.Infrastructure.Options che mất class Microsoft.Extensions.Options.Options,
// nên phải đặt alias thì mới gọi được Options.Create.
using MsOptions = Microsoft.Extensions.Options.Options;

namespace PhoneGrapher.Infrastructure.Tests;

public class VietQrServiceTests
{
    private const string ExpectedWithoutCrc =
        "00020101021238570010A00000072701270006970405011354022054574460208QRIBFTTA5303704540715000005802VN62160812PIC4829103756304";

    private static VietQrService CreateService() => new(MsOptions.Create(new VietQrOptions
    {
        Enabled = true,
        BankBin = "970405",
        BankName = "Agribank",
        AccountNumber = "5402205457446",
        AccountName = "NGUYEN VAN A",
        ExpiryMinutes = 15
    }));

    [Fact]
    public void BuildPayload_SinhDungChuoiEmvCoVaGanCrcOCuoi()
    {
        var payload = CreateService().BuildPayload(1_500_000m, "PIC482910375");

        Assert.StartsWith(ExpectedWithoutCrc, payload, StringComparison.Ordinal);
        Assert.Equal(ExpectedWithoutCrc.Length + 4, payload.Length);
        Assert.Equal(Crc16.Compute(ExpectedWithoutCrc), payload[^4..]);
    }

    [Theory]
    [InlineData("0006970405")]           // BIN Agribank
    [InlineData("01135402205457446")]    // số tài khoản
    [InlineData("0208QRIBFTTA")]         // chuyển tới tài khoản, không phải tới thẻ
    [InlineData("0010A000000727")]       // GUID NAPAS
    public void BuildPayload_ChuaDuTruongBatBuoc(string expectedSegment)
    {
        var payload = CreateService().BuildPayload(1_500_000m, "PIC482910375");

        Assert.Contains(expectedSegment, payload, StringComparison.Ordinal);
    }

    // Số tiền truyền dạng chuỗi vì C# không cho phép hằng decimal trong attribute.
    [Theory]
    [InlineData("1500000.49", "54071500000")]
    [InlineData("1500000.50", "54071500001")]
    [InlineData("2000", "54042000")]
    public void BuildPayload_LamTronSoTienVeSoNguyenVnd(string amountText, string expectedAmountField)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);

        var payload = CreateService().BuildPayload(amount, "PIC482910375");

        Assert.Contains(expectedAmountField, payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pic482910375")]      // chữ thường
    [InlineData("PIC 482910375")]     // có khoảng trắng
    [InlineData("THANH TOÁN")]        // có dấu tiếng Việt
    [InlineData("")]
    public void BuildPayload_MemoSaiDinhDang_NemArgumentException(string memo)
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.BuildPayload(1_500_000m, memo));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void BuildPayload_SoTienKhongDuong_NemArgumentOutOfRangeException(int amount)
    {
        var service = CreateService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.BuildPayload(amount, "PIC482910375"));
    }

    [Fact]
    public void BuildPayload_ChuaCauHinhSoTaiKhoan_NemInvalidOperationException()
    {
        var service = new VietQrService(MsOptions.Create(new VietQrOptions
        {
            BankBin = "970405",
            AccountNumber = ""
        }));

        Assert.Throws<InvalidOperationException>(() => service.BuildPayload(1_500_000m, "PIC482910375"));
    }

    [Fact]
    public void BuildPayload_MemoDaiHon99KyTu_NemArgumentException()
    {
        var service = CreateService();
        var memo = new string('A', 100);

        Assert.Throws<ArgumentException>(() => service.BuildPayload(1_500_000m, memo));
    }
}
