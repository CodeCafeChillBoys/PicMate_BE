# PICMate — hướng dẫn chạy dự án trên máy mới

Đọc hết mục 1 trước khi chạy. Có một mục liên quan tới tiền thật.

## 1. CẢNH BÁO: tài khoản nhận tiền

Chức năng thanh toán VietQR chuyển **tiền thật** vào tài khoản ngân hàng ghi trong `BE/src/PhoneGrapher.Api/appsettings.json`:

```json
"VietQr": {
  "BankBin": "970405",
  "AccountNumber": "5402205457446",
  ...
}
```

Đây là tài khoản Agribank của chủ dự án gốc. **Nếu bạn không đổi, mọi khoản khách chuyển sẽ vào tài khoản người khác, không phải bạn.**

Muốn nhận tiền về tài khoản của mình thì sửa ba dòng:

| Trường | Điền gì |
|---|---|
| `BankBin` | Mã BIN Napas của ngân hàng bạn. Tra tại `https://api.vietqr.io/v2/banks` |
| `AccountNumber` | Số tài khoản của bạn |
| `AccountName` | Tên chủ tài khoản, **viết hoa không dấu**, đúng như khi tra cứu số tài khoản trên app ngân hàng |

Chỉ muốn demo, không đụng tiền thật thì đặt `"Enabled": false` và dùng phương thức VNPay sandbox hoặc COD.

## 2. Yêu cầu môi trường

- .NET SDK 8 trở lên
- Node.js 20 trở lên
- PostgreSQL 14 trở lên

## 3. Chạy Backend

### 3.1 Sửa chuỗi kết nối

Mặc định trong `appsettings.json`:

```
Host=localhost;Port=5432;Database=PicMateDB;Username=postgres;Password=12345
```

Sửa cho khớp Postgres của bạn. Không cần tạo sẵn database, migration sẽ tạo.

### 3.2 Tạo database và áp migration

```bash
cd BE
dotnet restore
dotnet ef database update -p src/PhoneGrapher.Infrastructure -s src/PhoneGrapher.Api
```

Thiếu công cụ `dotnet-ef` thì cài trước: `dotnet tool install --global dotnet-ef`.

**Bỏ qua bước này là chức năng thanh toán hỏng**, vì thiếu 5 cột `ExpiresAt`, `CustomerClaimedAt`, `VerifiedByUserId`, `VerifiedAt`, `VerificationNote` trong bảng `payment_transactions`.

### 3.3 Chạy

```bash
dotnet run --project src/PhoneGrapher.Api
```

Mặc định `http://localhost:5274`. Swagger ở `http://localhost:5274/swagger`.

## 4. Chạy Frontend

### 4.1 Tạo file .env.local — BẮT BUỘC

`FE/.env.local` không nằm trong zip vì bị gitignore. **Không tạo file này thì frontend sẽ gọi sang backend deploy của chủ dự án gốc chứ không gọi máy bạn** — xem `FE/src/services/http.js`, giá trị mặc định là `https://picmate-api.onrender.com`.

```bash
cd FE
cp .env.example .env.local
```

Rồi mở `.env.local` và đặt:

```
VITE_API_BASE_URL=http://localhost:5274
```

### 4.2 Cài và chạy

```bash
npm install
npm run dev
```

Mặc định `http://localhost:5173`. **Giữ đúng cổng 5173** — đường dẫn quay về của VNPay đang ghi cứng cổng này trong `PaymentsController.cs`.

## 5. Thử luồng thanh toán VietQR

1. Đăng ký một tài khoản Customer và một tài khoản Grapher.
2. Đăng nhập bằng tài khoản Admin, duyệt KYC cho Grapher đó (`/admin-dashboard`, tab Phone-Graphers).
3. Đăng nhập Grapher, tạo một gói dịch vụ. Muốn thử tiền thật thì để giá **2000**.
4. Đăng nhập Customer, đặt lịch, chọn **"Chuyển khoản ngân hàng (VietQR)"**.
5. Trang QR hiện ra. **Quét thử bằng app ngân hàng và kiểm tra app điền đúng số tài khoản, số tiền, nội dung trước khi bấm chuyển.**
6. Chuyển xong bấm **"Tôi đã chuyển khoản"**.
7. Đăng nhập Admin, vào tab **"Đối soát"**, đối chiếu nội dung chuyển khoản với sao kê ngân hàng rồi bấm **Duyệt**.
8. Trang của khách tự chuyển sang màn hình thành công trong khoảng 5 giây.

Đơn chưa bấm "Tôi đã chuyển khoản" sẽ **tự huỷ sau 15 phút**. Đơn đã bấm thì không bao giờ bị tự huỷ, kể cả quá hạn.

## 6. Những thứ chạy được nhưng âm thầm không hoạt động

| Hạng mục | Trạng thái | Cách bật |
|---|---|---|
| Email xác nhận thanh toán | **Tắt** — `Smtp.Enabled: false` và `Brevo.Enabled: false` | Điền thông tin SMTP hoặc API key Brevo rồi bật `Enabled` lên `true`. Không bật thì hệ thống vẫn chạy bình thường, chỉ là khách không nhận email nào |
| Đăng nhập Google | Dùng Client ID của dự án gốc | Tạo OAuth Client ID riêng ở Google Cloud Console, điền vào `GoogleAuth:ClientId` (backend) và `VITE_GOOGLE_CLIENT_ID` (frontend). Hai giá trị phải trùng nhau |
| Tính năng AI (Gemini) | `GeminiSettings:ApiKey` đang là chuỗi `"Key"` | Điền API key thật từ Google AI Studio |
| VNPay | Sandbox, tiền giả | Muốn tiền thật cần đăng ký merchant, yêu cầu giấy phép kinh doanh |

## 7. Nếu đem deploy

- **CORS** trong `Program.cs` chỉ cho phép `localhost` và `127.0.0.1`. Deploy lên tên miền khác phải sửa chỗ này, không thì trình duyệt chặn hết request.
- Đường dẫn quay về của VNPay ghi cứng `http://localhost:5173/payment-result` trong `PaymentsController.cs`.
- `appsettings.json` đang chứa số tài khoản ngân hàng và khoá VNPay. Nếu repo của bạn công khai thì phải chuyển các giá trị này sang biến môi trường.
- Job tự huỷ đơn quá hạn chạy bên trong tiến trình API. Deploy nhiều instance thì instance nào cũng chạy job.

## 8. Chạy kiểm thử

```bash
cd BE
dotnet test tests/PhoneGrapher.Infrastructure.Tests
```

59 test, gồm cả bộ test khoá cấu trúc chuỗi VietQR. **Sửa `VietQrService` mà test đỏ thì đừng sửa test cho hết đỏ** — mã QR sai cấu trúc nghĩa là tiền có thể đi nhầm tài khoản.

## 9. Tài liệu thêm

- `docs/phone-grapher-backend.md` — kiến trúc và danh sách API
- `docs/vietqr-checklist-kiem-thu.md` — checklist kiểm thử trước khi mở cho khách trả tiền thật
- `docs/superpowers/specs/2026-07-29-vietqr-payment-design.md` — thiết kế chi tiết luồng VietQR
