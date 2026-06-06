using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;

namespace PhoneGrapher.Infrastructure.Persistence;

public static class SeedDataExtensions
{
    public static void SeedData(this ModelBuilder modelBuilder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        
        // IDs
        var vintageId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var koreanId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var minimalId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var streetId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        
        var grapherUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var customerUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        
        var grapherProfileId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var packageId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var bookingId = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var paymentId = Guid.Parse("80000000-0000-0000-0000-000000000001");
        var reviewId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var messageId = Guid.Parse("A0000000-0000-0000-0000-000000000001");

        // StyleTags
        modelBuilder.Entity<StyleTag>().HasData(
            new StyleTag { Id = vintageId, Name = "Vintage", Emoji = "📸", Color = "#C44569", CreatedAt = now },
            new StyleTag { Id = koreanId, Name = "Hàn Quốc", Emoji = "🎌", Color = "#FF6B9D", CreatedAt = now },
            new StyleTag { Id = minimalId, Name = "Minimal", Emoji = "⚪", Color = "#4A90E2", CreatedAt = now },
            new StyleTag { Id = streetId, Name = "Street", Emoji = "🏙️", Color = "#F5A623", CreatedAt = now });

        // Users
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = grapherUserId,
                FullName = "Nguyễn Anh",
                Email = "grapher@picmate.vn",
                PhoneNumber = "0900000001",
                PasswordHash = "seeded-user-register-again-to-login",
                AvatarUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=96&h=96&fit=crop",
                Role = UserRole.Grapher,
                IsActive = true,
                CreatedAt = now
            },
            new User
            {
                Id = customerUserId,
                FullName = "Trần Bình",
                Email = "customer@picmate.vn",
                PhoneNumber = "0900000002",
                PasswordHash = "seeded-user-register-again-to-login",
                AvatarUrl = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=96&h=96&fit=crop",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = now
            });

        // GrapherProfile
        modelBuilder.Entity<GrapherProfile>().HasData(new GrapherProfile
        {
            Id = grapherProfileId,
            UserId = grapherUserId,
            Bio = "Sinh viên đam mê chụp ảnh bằng điện thoại, chuyên ảnh lifestyle và vintage.",
            Location = "TP.HCM",
            District = "Quận 1",
            KycStatus = KycStatus.Approved,
            IsVerified = true,
            IsOnline = true,
            AverageRating = 4.9m,
            ReviewCount = 234,
            CreatedAt = now
        });

        // GrapherStyleTags
        modelBuilder.Entity<GrapherStyleTag>().HasData(
            new GrapherStyleTag { GrapherProfileId = grapherProfileId, StyleTagId = vintageId },
            new GrapherStyleTag { GrapherProfileId = grapherProfileId, StyleTagId = koreanId });

        // GrapherPortfolioItem
        modelBuilder.Entity<GrapherPortfolioItem>().HasData(
            new GrapherPortfolioItem
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000001"),
                GrapherProfileId = grapherProfileId,
                ImageUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=400&h=400&fit=crop",
                Caption = "Mùa thu Hà Nội",
                DisplayOrder = 1,
                CreatedAt = now
            });

        // GrapherActivityArea
        modelBuilder.Entity<GrapherActivityArea>().HasData(
            new GrapherActivityArea
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                GrapherProfileId = grapherProfileId,
                City = "TP.HCM",
                District = "Quận 1",
                CreatedAt = now
            },
            new GrapherActivityArea
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000003"),
                GrapherProfileId = grapherProfileId,
                City = "TP.HCM",
                District = "Quận 3",
                CreatedAt = now
            });

        // GrapherServicePackage
        modelBuilder.Entity<GrapherServicePackage>().HasData(new GrapherServicePackage
        {
            Id = packageId,
            GrapherProfileId = grapherProfileId,
            Name = "Hourly phone shoot",
            Description = "Một giờ chụp bằng điện thoại, nhận ảnh trong ngày.",
            Price = 150000m,
            DurationMinutes = 60,
            IsActive = true,
            CreatedAt = now
        });

        // Booking
        modelBuilder.Entity<Booking>().HasData(new Booking
        {
            Id = bookingId,
            CustomerId = customerUserId,
            GrapherProfileId = grapherProfileId,
            ServicePackageId = packageId,
            ScheduledAt = now.AddDays(2),
            DurationMinutes = 60,
            Location = "Đường sách Nguyễn Văn Bình, Quận 1, TP.HCM",
            Note = "Mang theo phụ kiện vintage nhé",
            Status = BookingStatus.Completed,
            TotalAmount = 150000m,
            PlatformFeeAmount = 15000m,
            GrapherPayoutAmount = 135000m,
            CompletedAt = now.AddDays(2).AddHours(2),
            CreatedAt = now
        });

        // PaymentTransaction
        modelBuilder.Entity<PaymentTransaction>().HasData(new PaymentTransaction
        {
            Id = paymentId,
            BookingId = bookingId,
            Provider = PaymentProvider.VnPay,
            Status = PaymentStatus.Succeeded,
            EscrowStatus = EscrowStatus.Released,
            TransactionCode = "TXN_123456789",
            ProviderTransactionId = "VNPAY_987654321",
            Amount = 150000m,
            PlatformFeeAmount = 15000m,
            GrapherPayoutAmount = 135000m,
            PaidAt = now.AddDays(1),
            ReleasedAt = now.AddDays(2).AddHours(2),
            CreatedAt = now
        });

        // Review
        modelBuilder.Entity<Review>().HasData(new Review
        {
            Id = reviewId,
            BookingId = bookingId,
            CustomerId = customerUserId,
            GrapherProfileId = grapherProfileId,
            Rating = 5,
            Comment = "Nháy rất nhiệt tình, ảnh đẹp và gửi nhanh chóng. Sẽ ủng hộ lại!",
            CreatedAt = now.AddDays(2).AddHours(3)
        });

        // Message
        modelBuilder.Entity<Message>().HasData(new Message
        {
            Id = messageId,
            SenderId = customerUserId,
            ReceiverId = grapherUserId,
            Content = "Chào bạn, mình muốn book lịch chụp cuối tuần này",
            IsRead = true,
            CreatedAt = now.AddDays(-1)
        });

        // Preset
        modelBuilder.Entity<Preset>().HasData(new Preset
        {
            Id = Guid.Parse("60000000-0000-0000-0000-000000000001"),
            Name = "Golden Hour",
            Category = "Warm",
            ImageUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=300&h=300&fit=crop",
            DownloadUrl = "https://example.com/presets/golden-hour.dng",
            Price = 49000m,
            Rating = 4.9m,
            DownloadCount = 12500,
            CreatedAt = now
        });
    }
}
