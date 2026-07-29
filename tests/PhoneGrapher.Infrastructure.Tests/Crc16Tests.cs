using PhoneGrapher.Infrastructure.Payments;

namespace PhoneGrapher.Infrastructure.Tests;

public class Crc16Tests
{
    [Fact]
    public void Compute_ChuoiKiemChungChuan_TraVe29B1()
    {
        Assert.Equal("29B1", Crc16.Compute("123456789"));
    }

    [Fact]
    public void Compute_LuonTraVeDungBonKyTuHexInHoa()
    {
        var result = Crc16.Compute("000201010212");

        Assert.Equal(4, result.Length);
        Assert.All(result, c => Assert.Contains(c, "0123456789ABCDEF"));
    }

    [Fact]
    public void Compute_ChuoiRong_TraVeFFFF()
    {
        Assert.Equal("FFFF", Crc16.Compute(string.Empty));
    }
}
