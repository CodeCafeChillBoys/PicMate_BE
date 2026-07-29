using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Domain.Entities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace PhoneGrapher.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly PhoneGrapherDbContext _dbContext;

    public ChatHub(PhoneGrapherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SendMessage(string receiverId, string content)
    {
        var senderIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(senderIdStr) || !Guid.TryParse(senderIdStr, out var senderId))
            return;

        if (!Guid.TryParse(receiverId, out var receiverGuid))
            return;

        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = receiverGuid,
            Content = content,
            IsRead = false
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Broadcast the message to the receiver
        await Clients.User(receiverId).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            senderId = message.SenderId,
            receiverId = message.ReceiverId,
            content = message.Content,
            createdAt = message.CreatedAt,
            isRead = message.IsRead
        });

        // Also broadcast back to the sender
        await Clients.User(senderIdStr).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            senderId = message.SenderId,
            receiverId = message.ReceiverId,
            content = message.Content,
            createdAt = message.CreatedAt,
            isRead = message.IsRead
        });
    }

    public async Task MarkAsRead(string senderId)
    {
        var receiverIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(receiverIdStr) || !Guid.TryParse(receiverIdStr, out var receiverId))
            return;

        if (!Guid.TryParse(senderId, out var senderGuid))
            return;

        var unreadMessages = await _dbContext.Messages
            .Where(m => m.SenderId == senderGuid && m.ReceiverId == receiverId && !m.IsRead)
            .ToListAsync();

        if (unreadMessages.Any())
        {
            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }
            await _dbContext.SaveChangesAsync();

            // Notify the sender that messages were read
            await Clients.User(senderId).SendAsync("MessagesRead", receiverId);
        }
    }
}
