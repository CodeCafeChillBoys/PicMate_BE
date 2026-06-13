using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class AuthService(
    PhoneGrapherDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions,
    IOptions<GoogleAuthOptions> googleAuthOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly GoogleAuthOptions _googleAuthOptions = googleAuthOptions.Value;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new InvalidOperationException("Unsupported user role.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = role
        };

        if (role == UserRole.Grapher)
        {
            user.GrapherProfile = CreateDefaultGrapherProfile();
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail && x.IsActive, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> GoogleAuthAsync(GoogleAuthRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_googleAuthOptions.ClientId))
        {
            throw new InvalidOperationException("Đăng nhập Google chưa được cấu hình.");
        }

        if (string.IsNullOrWhiteSpace(request.Credential))
        {
            throw new UnauthorizedAccessException("Thiếu thông tin xác thực Google.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleAuthOptions.ClientId }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, settings);
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAccessException("Xác thực Google thất bại.");
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new UnauthorizedAccessException("Tài khoản Google chưa xác minh email.");
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is not null)
        {
            if (user.Provider != AuthProvider.Google)
            {
                throw new InvalidOperationException("Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng mật khẩu.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");
            }

            return await BuildAuthResponseAsync(user, cancellationToken);
        }

        var role = ParseRole(request.Role);
        user = new User
        {
            FullName = string.IsNullOrWhiteSpace(payload.Name) ? normalizedEmail.Split('@')[0] : payload.Name.Trim(),
            Email = normalizedEmail,
            PhoneNumber = string.Empty,
            // Tài khoản Google không dùng mật khẩu; đặt hash ngẫu nhiên để cột non-null và không thể đăng nhập bằng mật khẩu.
            PasswordHash = passwordHasher.Hash(Guid.NewGuid().ToString("N")),
            AvatarUrl = payload.Picture,
            Role = role,
            Provider = AuthProvider.Google
        };

        if (role == UserRole.Grapher)
        {
            user.GrapherProfile = CreateDefaultGrapherProfile();
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        return new CurrentUserResponse(user.Id, user.FullName, user.Email, user.Role.ToString().ToLowerInvariant(), user.AvatarUrl);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = jwtTokenService.CreateAccessToken(user);
        var refreshToken = jwtTokenService.CreateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = passwordHasher.Hash(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString().ToLowerInvariant(),
            user.AvatarUrl,
            GetRedirect(user.Role),
            accessToken,
            refreshToken);
    }

    private static string GetRedirect(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => "/admin-dashboard",
            UserRole.Grapher => "/photographer-dashboard",
            _ => "/customer-dashboard"
        };
    }

    // Google chỉ cho tạo tài khoản Customer hoặc Grapher (không bao giờ Admin), chấp nhận cả "photographer".
    private static UserRole ParseRole(string? role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        return normalized is "grapher" or "photographer" ? UserRole.Grapher : UserRole.Customer;
    }

    private static GrapherProfile CreateDefaultGrapherProfile()
    {
        return new GrapherProfile
        {
            Bio = string.Empty,
            Location = string.Empty,
            KycStatus = KycStatus.Pending,
            ServicePackages = new List<GrapherServicePackage>
            {
                new()
                {
                    Name = "Chụp ngoại cảnh",
                    Description = "Một giờ chụp bằng điện thoại ngoài trời, nhận ảnh trong ngày.",
                    Price = 150000m,
                    DurationMinutes = 60,
                    IsActive = true
                },
                new()
                {
                    Name = "Chụp Studio",
                    Description = "Một giờ chụp bằng điện thoại trong studio, nhận ảnh trong ngày.",
                    Price = 200000m,
                    DurationMinutes = 60,
                    IsActive = true
                },
                new()
                {
                    Name = "Chụp sự kiện",
                    Description = "Hai giờ chụp sự kiện bằng điện thoại, nhận ảnh trong 24h.",
                    Price = 350000m,
                    DurationMinutes = 120,
                    IsActive = true
                }
            }
        };
    }
}
