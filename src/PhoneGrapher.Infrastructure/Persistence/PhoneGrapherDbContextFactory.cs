using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PhoneGrapher.Infrastructure.Persistence;

/// <summary>
/// Dùng cho các lệnh dotnet ef lúc thiết kế (migrations add / database update).
/// </summary>
/// <remarks>
/// Chuỗi kết nối lấy theo thứ tự ưu tiên: biến môi trường → appsettings của project Api → mặc định localhost.
/// Nhờ vậy có thể chạy migration lên DB khác mà không phải sửa mã nguồn:
///   $env:ConnectionStrings__DefaultConnection = "Host=...;Database=...;Username=...;Password=..."
///   dotnet ef database update -p src/PhoneGrapher.Infrastructure -s src/PhoneGrapher.Api
/// </remarks>
public sealed class PhoneGrapherDbContextFactory : IDesignTimeDbContextFactory<PhoneGrapherDbContext>
{
    private const string LocalFallback = "Host=localhost;Port=5432;Database=PicMateDB;Username=postgres;Password=12345";

    public PhoneGrapherDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveApiProjectPath())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = LocalFallback;
        }

        var options = new DbContextOptionsBuilder<PhoneGrapherDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PhoneGrapherDbContext(options);
    }

    /// <summary>
    /// Lệnh ef có thể chạy từ thư mục gốc solution hoặc từ thư mục project, nên đi ngược lên tìm
    /// thư mục chứa appsettings.json của project Api thay vì đoán theo thư mục hiện hành.
    /// </summary>
    private static string ResolveApiProjectPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PhoneGrapher.Api");
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
