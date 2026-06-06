namespace PhoneGrapher.Application.Dtos;

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string PhoneNumber,
    string Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string? Avatar,
    string Redirect,
    string AccessToken,
    string RefreshToken);

public sealed record CurrentUserResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string? AvatarUrl);

public sealed record UpdateUserProfileRequest(
    string FullName,
    string? AvatarUrl);
