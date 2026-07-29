using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IGrapherService _grapherService;

        public ChatbotController(IHttpClientFactory httpClientFactory, IConfiguration configuration, IGrapherService grapherService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _grapherService = grapherService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatbotRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Tin nhắn không được trống." });
            }

            var apiKey = _configuration["GeminiSettings:ApiKey"] ?? string.Empty;
            var modelName = _configuration["GeminiSettings:ModelName"] ?? "gemini-2.5-flash";

            if (string.IsNullOrEmpty(apiKey))
            {
                return StatusCode(500, new { error = "Thiếu cấu hình Gemini API Key." });
            }

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            IReadOnlyList<GrapherSummaryResponse> graphersList = new List<GrapherSummaryResponse>();
            // Fetch real database Phone Graphers list
            string graphersContext = string.Empty;
            try
            {
                var searchRequest = new GrapherSearchRequest(null, null, null, null, null, null);
                graphersList = await _grapherService.SearchAsync(searchRequest);
                var sb = new StringBuilder();
                sb.AppendLine("DANH SÁCH THỢ CHỤP (PHONE GRAPHERS) TRÊN HỆ THỐNG:");
                foreach (var g in graphersList.Take(15))
                {
                    sb.AppendLine($"- Tên: {g.Name}");
                    sb.AppendLine($"  ID: {g.Id}");
                    sb.AppendLine($"  Địa điểm: {g.Location}");
                    sb.AppendLine($"  Đánh giá: {g.Rating} sao ({g.ReviewCount} lượt review)");
                    sb.AppendLine($"  Giá thuê theo giờ: {g.Pricing.Hourly:N0} VNĐ");
                    sb.AppendLine($"  Trạng thái hoạt động: {(g.IsOnline ? "Online (Sẵn sàng nhận lịch chụp ngay)" : "Offline")}");
                    sb.AppendLine($"  Phong cách chụp: {string.Join(", ", g.Styles)}");
                    sb.AppendLine();
                }
                graphersContext = sb.ToString();
            }
            catch (Exception)
            {
                graphersContext = "Không thể tải danh sách thợ chụp từ cơ sở dữ liệu lúc này.";
            }

            var systemInstruction = new
            {
                parts = new[] {
                    new {
                        text = $@"Bạn là trợ lý AI của PIC PLS – nền tảng đặt lịch chụp ảnh Phone Grapher #1 Việt Nam.

THÔNG TIN VỀ PIC PLS:
- PIC PLS kết nối khách hàng với Phone Grapher (thợ chụp ảnh bằng điện thoại) tài năng.
- Dịch vụ: Chụp ảnh chân dung, ảnh couple, ảnh gia đình, ảnh sự kiện, quay TikTok, edit ảnh.
- Phong cách phổ biến: Hàn Quốc, Vintage, Minimal, Film, Streetwear.
- Thanh toán an toàn qua VNPay (Escrow – tiền được giữ đến khi hoàn thành).
- Tính năng ""Chụp ngay"": Tìm Phone Grapher đang Online để đặt lịch gấp.
- Preset Shop: Mua/bán preset chỉnh ảnh.
- Hỗ trợ: picpls202@gmail.com
- Facebook: https://www.facebook.com/profile.php?id=61590515463360

DỮ LIỆU DƯỚI CƠ SỞ DỮ LIỆU (DATABASE):
{graphersContext}

QUY TẮC TRẢ LỜI & GỢI Ý PHONG CÁCH:
1. Trả lời bằng tiếng Việt, thân thiện, ngắn gọn và dễ hiểu.

2. KHI NGƯỜI DÙNG CHƯA BIẾT CHỌN PHONG CÁCH NÀO:
   - Hãy gợi ý thực hiện một bài trắc nghiệm nhanh để tìm phong cách phù hợp (ví dụ hỏi về không gian chụp, tone màu yêu thích, kiểu trang phục...).
   - Hỏi TỪNG CÂU HỎI MỘT, không hỏi dồn dập.
   - Để hiển thị các phương án dưới dạng nút bấm trắc nghiệm cho người dùng nhấp trực quan, bạn BẮT BUỘC phải viết mỗi phương án trên một dòng riêng biệt theo định dạng: `- [Option: Nội dung phương án]`.
     Ví dụ:
     *Bạn muốn chụp ở không gian nào?*
     - [Option: Chụp trong nhà (Studio/Quán cafe)]
     - [Option: Chụp ngoài trời (Đường phố/Thiên nhiên)]
   - Sau khi người dùng trả lời xong các câu hỏi trắc nghiệm, hãy tổng hợp phong cách phù hợp nhất cho họ (Hàn Quốc, Vintage, Minimal, Streetwear, Film...) và gợi ý ngay 1-3 thợ chụp phù hợp phong cách đó có trong DATABASE.

3. KHI NGƯỜI DÙNG UPLOAD HÌNH ẢNH:
   - Hãy phân tích hình ảnh đính kèm (màu sắc, ánh sáng, góc chụp, trang phục...).
   - Nhận diện xem phong cách của bức ảnh tương ứng với phong cách nào có trên PIC PLS (Hàn Quốc, Vintage, Minimal, Film, hay Streetwear).
   - Đưa ra lời khuyên phong cách dựa trên ảnh đó và gợi ý các thợ chụp phù hợp từ DATABASE.

4. GỢI Ý THỢ CHỤP & LINK LIÊN KẾT:
   - Khi gợi ý thợ chụp, ghi rõ: Tên, Địa điểm, Điểm đánh giá, Giá thuê/giờ và các Phong cách chụp nổi bật.
   - BẮT BUỘC cung cấp link trực tiếp để khách hàng bấm xem chi tiết hồ sơ: [Xem hồ sơ của {{Tên thợ}}](/photographer/{{ID_của_thợ}}). Ví dụ: [Xem hồ sơ của Nguyễn Văn A](/photographer/12345678-1234-1234-1234-123456789abc)

5. Nếu không tìm thấy thợ chụp phù hợp từ cơ sở dữ liệu, hãy khuyên khách hàng truy cập trang Khám Phá (/explore) để tự tìm kiếm bằng bộ lọc thủ công.
6. Sử dụng các biểu tượng emoji phù hợp để tạo cảm giác thân thiện."
                    }
                }
            };

            var contents = new List<object>();

            // Chat history
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    contents.Add(new
                    {
                        role = msg.Role,
                        parts = msg.Parts != null 
                            ? msg.Parts.Select(p => (object)new { text = p.Text }).ToList() 
                            : new List<object> { new { text = "" } }
                    });
                }
            }

            // Current user message
            var userParts = new List<object>();

            if (!string.IsNullOrEmpty(request.FileBase64) && !string.IsNullOrEmpty(request.MimeType))
            {
                string base64Data = request.FileBase64;
                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }

                userParts.Add(new
                {
                    inlineData = new
                    {
                        mimeType = request.MimeType,
                        data = base64Data
                    }
                });
            }

            userParts.Add(new { text = request.Message });

            contents.Add(new
            {
                role = "user",
                parts = userParts
            });

            var requestBody = new
            {
                contents = contents,
                systemInstruction = systemInstruction,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 1024
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(url, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    // Fallback to local AI generator if Gemini API fails (e.g. no internet/invalid key)
                    var fallbackReply = GenerateLocalFallbackReply(request.Message, graphersList);
                    return Ok(new { reply = fallbackReply });
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return Ok(new { reply = text ?? "Không nhận được phản hồi từ AI." });
            }
            catch (Exception)
            {
                // Fallback to local AI generator on network exception
                var fallbackReply = GenerateLocalFallbackReply(request.Message, graphersList);
                return Ok(new { reply = fallbackReply });
            }
        }

        private string GenerateLocalFallbackReply(string message, IReadOnlyList<GrapherSummaryResponse> graphers)
        {
            var msg = message.ToLower();

            // Quiz Fallback Logic
            if (msg.Contains("chưa biết chọn phong cách") || msg.Contains("chọn phong cách nào") || msg.Contains("quiz") || msg.Contains("trắc nghiệm"))
            {
                return "Chào bạn! Hãy cùng làm một bài trắc nghiệm nhanh 3 câu hỏi để tìm phong cách phù hợp nhất với bạn nhé. ✨\n\n**Câu 1: Bạn muốn chụp ảnh ở không gian nào?**\n- [Option: Chụp ngoài trời (Đường phố, thiên nhiên)]\n- [Option: Chụp trong nhà (Studio, quán cafe)]";
            }

            if (msg.Contains("chụp ngoài trời") || msg.Contains("chụp trong nhà"))
            {
                return "Tuyệt vời! Tiếp tục câu thứ hai nhé:\n\n**Câu 2: Bạn thích tone màu chủ đạo nào cho album của mình?**\n- [Option: Tone ấm áp, cổ điển (Vintage)]\n- [Option: Tone tươi sáng, nhẹ nhàng (Hàn Quốc, Minimal)]\n- [Option: Tone cá tính, đậm nét (Streetwear)]";
            }

            if (msg.Contains("tone ấm áp") || msg.Contains("tone tươi sáng") || msg.Contains("tone cá tính"))
            {
                return "Câu hỏi cuối cùng đây:\n\n**Câu 3: Bạn dự định mặc trang phục theo phong cách nào?**\n- [Option: Đồ jeans cá tính hoặc đồ tối màu]\n- [Option: Váy nhẹ nhàng, áo thun hoặc sơ mi sáng màu]\n- [Option: Đồ retro cổ điển hoặc áo khoác da]";
            }

            if (msg.Contains("đồ jeans") || msg.Contains("váy nhẹ nhàng") || msg.Contains("đồ retro"))
            {
                string recommendedStyle = "Hàn Quốc";
                if (msg.Contains("đồ retro")) recommendedStyle = "Vintage";
                else if (msg.Contains("đồ jeans")) recommendedStyle = "Street";

                var matchedGraphers = graphers
                    .Where(g => g.Styles.Any(s => s.ToLower().Contains(recommendedStyle.ToLower())))
                    .Take(2)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"🎉 Dựa trên các lựa chọn của bạn, phong cách chụp phù hợp nhất dành cho bạn là **{recommendedStyle}**!");
                sb.AppendLine();
                if (matchedGraphers.Count > 0)
                {
                    sb.AppendLine("📸 Dưới đây là các Phone Grapher hàng đầu có phong cách này:");
                    sb.AppendLine();
                    foreach (var g in matchedGraphers)
                    {
                        sb.AppendLine($"👤 **{g.Name}** ({g.Location})");
                        sb.AppendLine($"⭐ {g.Rating} / 5");
                        sb.AppendLine($"💵 Giá: {g.Pricing.Hourly:N0}đ/giờ");
                        sb.AppendLine($"🎨 Gu chụp: {string.Join(", ", g.Styles)}");
                        sb.AppendLine($"👉 [Xem hồ sơ & Đặt lịch ngay](/photographer/{g.Id})");
                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("Hiện tại hệ thống chưa có thợ chụp nào cập nhật phong cách này. Bạn có thể vào trang Khám Phá (/explore) để tự tìm kiếm nhé!");
                }
                return sb.ToString();
            }
            
            // 1. FAQ triggers
            if (msg.Contains("giá") || msg.Contains("bao nhiêu tiền"))
            {
                return "💰 Giá thuê Phone Grapher trên PIC PLS dao động từ 150.000đ - 350.000đ/giờ tùy gói dịch vụ. Bạn có thể xem chi tiết bảng giá trên hồ sơ của từng thợ chụp nhé!";
            }
            if (msg.Contains("hoàn tiền") || msg.Contains("hủy lịch"))
            {
                return "🔄 Chính sách hoàn tiền của PIC PLS:\n- Hủy trước 24h: Hoàn tiền 100%.\n- Hủy từ 12h - 24h: Hoàn tiền 70%.\n- Hủy dưới 12h: Không hoàn tiền.\nBạn gửi yêu cầu qua email picpls202@gmail.com để được xử lý nhé!";
            }
            if (msg.Contains("đặt lịch") || msg.Contains("book"))
            {
                return "📅 Cách đặt lịch chụp rất đơn giản:\n1. Vào mục Khám Phá để tìm thợ chụp.\n2. Chọn gói dịch vụ và chọn ngày giờ.\n3. Tiến hành thanh toán online qua VNPay để hoàn tất đặt lịch!";
            }

            // 2. Recommend filters
            var filtered = graphers.AsEnumerable();
            bool isFiltered = false;

            if (msg.Contains("hàn quốc"))
            {
                filtered = filtered.Where(g => g.Styles.Any(s => s.ToLower().Contains("hàn quốc")));
                isFiltered = true;
            }
            if (msg.Contains("vintage"))
            {
                filtered = filtered.Where(g => g.Styles.Any(s => s.ToLower().Contains("vintage")));
                isFiltered = true;
            }
            if (msg.Contains("minimal"))
            {
                filtered = filtered.Where(g => g.Styles.Any(s => s.ToLower().Contains("minimal")));
                isFiltered = true;
            }
            if (msg.Contains("street"))
            {
                filtered = filtered.Where(g => g.Styles.Any(s => s.ToLower().Contains("street")));
                isFiltered = true;
            }

            if (msg.Contains("hcm") || msg.Contains("hồ chí minh") || msg.Contains("sài gòn"))
            {
                filtered = filtered.Where(g => g.Location.ToLower().Contains("hcm") || g.Location.ToLower().Contains("hồ chí minh") || g.Location.ToLower().Contains("tp.hcm"));
                isFiltered = true;
            }
            if (msg.Contains("hà nội") || msg.Contains("hn"))
            {
                filtered = filtered.Where(g => g.Location.ToLower().Contains("hà nội") || g.Location.ToLower().Contains("hn"));
                isFiltered = true;
            }

            if (msg.Contains("online") || msg.Contains("chụp ngay") || msg.Contains("gấp"))
            {
                filtered = filtered.Where(g => g.IsOnline);
                isFiltered = true;
            }

            var matches = filtered.Take(3).ToList();

            if (isFiltered && matches.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("🔍 Tôi đã tìm thấy một số Phone Grapher phù hợp trong hệ thống:");
                sb.AppendLine();
                foreach (var g in matches)
                {
                    sb.AppendLine($"📸 **{g.Name}** ({g.Location})");
                    sb.AppendLine($"⭐ {g.Rating} / 5 | {g.ReviewCount} đánh giá");
                    sb.AppendLine($"💵 Giá thuê: {g.Pricing.Hourly:N0}đ/giờ");
                    sb.AppendLine($"🎨 Gu chụp: {string.Join(", ", g.Styles)}");
                    sb.AppendLine($"👉 [Xem hồ sơ & Đặt lịch ngay](/photographer/{g.Id})");
                    sb.AppendLine();
                }
                return sb.ToString();
            }

            if (isFiltered && matches.Count == 0)
            {
                return "Hiện tại tôi chưa tìm thấy thợ chụp nào phù hợp chính xác với bộ lọc này trên hệ thống. Bạn có thể vào trang Khám Phá (/explore) để tự tìm kiếm bằng bộ lọc nhé!";
            }

            return "Chào bạn! Mình là trợ lý ảo của PIC PLS. Bạn có thể hỏi mình về:\n- Gợi ý thợ chụp (Ví dụ: 'Tìm thợ chụp phong cách Hàn Quốc ở HCM')\n- Giá dịch vụ & cách đặt lịch chụp\n- Các quy chế hoàn tiền, hủy lịch của hệ thống. 📸";
        }
    }

    // DTOs
    public class ChatbotRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatbotMessage>? History { get; set; }
        public string? FileBase64 { get; set; }
        public string? MimeType { get; set; }
    }

    public class ChatbotMessage
    {
        public string Role { get; set; } = string.Empty;
        public List<ChatbotPart>? Parts { get; set; }
    }

    public class ChatbotPart
    {
        public string Text { get; set; } = string.Empty;
    }
}
