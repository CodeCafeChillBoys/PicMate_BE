using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly PhoneGrapherDbContext _dbContext;

    public UsersController(PhoneGrapherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }
        
        user.AvatarUrl = request.AvatarUrl; // Allow null to remove avatar

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.AvatarUrl
        ));
    }
}
