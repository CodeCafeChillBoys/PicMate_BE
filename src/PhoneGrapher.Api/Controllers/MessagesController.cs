using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Infrastructure.Persistence;
using System.Security.Claims;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly PhoneGrapherDbContext _dbContext;

    public MessagesController(PhoneGrapherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("history/{otherUserId}")]
    public async Task<IActionResult> GetChatHistory(Guid otherUserId)
    {
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdStr) || !Guid.TryParse(currentUserIdStr, out var currentUserId))
            return Unauthorized();

        var messages = await _dbContext.Messages
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                        (m.SenderId == otherUserId && m.ReceiverId == currentUserId))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                m.ReceiverId,
                m.Content,
                m.CreatedAt,
                m.IsRead
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdStr) || !Guid.TryParse(currentUserIdStr, out var currentUserId))
            return Unauthorized();

        // Get all unique users this user has chatted with
        var sentUserIds = await _dbContext.Messages
            .Where(m => m.SenderId == currentUserId)
            .Select(m => m.ReceiverId)
            .Distinct()
            .ToListAsync();

        var receivedUserIds = await _dbContext.Messages
            .Where(m => m.ReceiverId == currentUserId)
            .Select(m => m.SenderId)
            .Distinct()
            .ToListAsync();

        var contactIds = sentUserIds.Union(receivedUserIds).ToList();

        var contacts = await _dbContext.Users
            .Where(u => contactIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.AvatarUrl,
                u.Role
            })
            .ToListAsync();

        return Ok(contacts);
    }
}
