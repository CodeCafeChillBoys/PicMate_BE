using System.Globalization;
using System.Text;

namespace PhoneGrapher.Infrastructure.Payments;

/// <summary>
/// CRC-16/CCITT-FALSE (đa thức 0x1021, khởi tạo 0xFFFF, không đảo bit, không XOR đầu ra).
/// Đây là biến thể chuẩn EMVCo dùng cho trường 63 của mã VietQR.
/// </summary>
internal static class Crc16
{
    public static string Compute(string input)
    {
        ushort crc = 0xFFFF;

        foreach (var b in Encoding.UTF8.GetBytes(input))
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
