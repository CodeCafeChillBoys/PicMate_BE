using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;

namespace PhoneGrapher.Infrastructure.Persistence;

public sealed class PhoneGrapherDbContext(DbContextOptions<PhoneGrapherDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<GrapherProfile> GrapherProfiles => Set<GrapherProfile>();
    public DbSet<StyleTag> StyleTags => Set<StyleTag>();
    public DbSet<GrapherStyleTag> GrapherStyleTags => Set<GrapherStyleTag>();
    public DbSet<GrapherPortfolioItem> GrapherPortfolioItems => Set<GrapherPortfolioItem>();
    public DbSet<GrapherActivityArea> GrapherActivityAreas => Set<GrapherActivityArea>();
    public DbSet<GrapherServicePackage> GrapherServicePackages => Set<GrapherServicePackage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Preset> Presets => Set<Preset>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserFavoriteGrapher> UserFavoriteGraphers => Set<UserFavoriteGrapher>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.AvatarUrl).HasMaxLength(1024);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32).HasDefaultValue(AuthProvider.Local);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(512).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GrapherProfile>(entity =>
        {
            entity.ToTable("grapher_profiles");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.Location);
            entity.HasIndex(x => new { x.IsVerified, x.AverageRating });
            entity.HasIndex(x => x.CccdNumber).IsUnique().HasFilter("\"CccdNumber\" IS NOT NULL");
            entity.Property(x => x.Bio).HasMaxLength(2000);
            entity.Property(x => x.Location).HasMaxLength(120).IsRequired();
            entity.Property(x => x.District).HasMaxLength(120);
            entity.Property(x => x.CccdNumber).HasMaxLength(32);
            entity.Property(x => x.CccdFrontImageUrl).HasMaxLength(1024);
            entity.Property(x => x.CccdBackImageUrl).HasMaxLength(1024);
            entity.Property(x => x.KycStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.AverageRating).HasPrecision(3, 2);
            entity.HasOne(x => x.User)
                .WithOne(x => x.GrapherProfile)
                .HasForeignKey<GrapherProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StyleTag>(entity =>
        {
            entity.ToTable("style_tags");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Emoji).HasMaxLength(16);
            entity.Property(x => x.Color).HasMaxLength(16);
        });

        modelBuilder.Entity<GrapherStyleTag>(entity =>
        {
            entity.ToTable("grapher_style_tags");
            entity.HasKey(x => new { x.GrapherProfileId, x.StyleTagId });
            entity.HasIndex(x => x.StyleTagId);
            entity.HasOne(x => x.GrapherProfile)
                .WithMany(x => x.StyleTags)
                .HasForeignKey(x => x.GrapherProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.StyleTag)
                .WithMany(x => x.Graphers)
                .HasForeignKey(x => x.StyleTagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GrapherPortfolioItem>(entity =>
        {
            entity.ToTable("grapher_portfolio_items");
            entity.HasIndex(x => new { x.GrapherProfileId, x.DisplayOrder });
            entity.Property(x => x.ImageUrl).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Caption).HasMaxLength(300);
        });

        modelBuilder.Entity<GrapherActivityArea>(entity =>
        {
            entity.ToTable("grapher_activity_areas");
            entity.HasIndex(x => new { x.City, x.District });
            entity.Property(x => x.City).HasMaxLength(120).IsRequired();
            entity.Property(x => x.District).HasMaxLength(120);
        });

        modelBuilder.Entity<GrapherServicePackage>(entity =>
        {
            entity.ToTable("grapher_service_packages");
            entity.HasIndex(x => new { x.GrapherProfileId, x.Price });
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasIndex(x => new { x.CustomerId, x.Status });
            entity.HasIndex(x => new { x.GrapherProfileId, x.ScheduledAt });
            entity.Property(x => x.Location).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.PlatformFeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrapherPayoutAmount).HasPrecision(18, 2);
            entity.Property(x => x.CancellationReason).HasMaxLength(1000);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.CustomerBookings)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.GrapherProfile)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.GrapherProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ServicePackage)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.ServicePackageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");
            entity.HasIndex(x => x.BookingId).IsUnique();
            entity.HasIndex(x => x.TransactionCode).IsUnique();
            entity.HasIndex(x => x.ProviderTransactionId);
            entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.EscrowStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.TransactionCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(128);
            entity.Property(x => x.ProviderResponseCode).HasMaxLength(32);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PlatformFeeAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrapherPayoutAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Booking)
                .WithOne(x => x.PaymentTransaction)
                .HasForeignKey<PaymentTransaction>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews", table => table.HasCheckConstraint("ck_reviews_rating_range", "\"Rating\" >= 1 AND \"Rating\" <= 5"));
            entity.HasIndex(x => x.BookingId).IsUnique();
            entity.HasIndex(x => new { x.GrapherProfileId, x.Rating });
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.HasOne(x => x.Booking)
                .WithOne(x => x.Review)
                .HasForeignKey<Review>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Preset>(entity =>
        {
            entity.ToTable("presets");
            entity.HasIndex(x => new { x.Category, x.Price });
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.ImageUrl).HasMaxLength(1024);
            entity.Property(x => x.DownloadUrl).HasMaxLength(1024);
            entity.Property(x => x.Rating).HasPrecision(3, 2);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Receiver)
                .WithMany()
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("disputes");
            entity.HasIndex(x => new { x.Status, x.Priority });
            entity.HasIndex(x => x.BookingId);
            entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.AdminNote).HasMaxLength(2000);
            entity.Property(x => x.Resolution).HasMaxLength(64);
            entity.HasOne(x => x.Booking)
                .WithMany()
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Reporter)
                .WithMany()
                .HasForeignKey(x => x.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Respondent)
                .WithMany()
                .HasForeignKey(x => x.RespondentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.ToTable("system_settings");
            entity.Property(x => x.PlatformFeePercent).HasPrecision(5, 2);
            entity.Property(x => x.MinWithdrawalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasIndex(x => new { x.UserId, x.IsRead });
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFavoriteGrapher>(entity =>
        {
            entity.ToTable("user_favorite_graphers");
            entity.HasIndex(x => new { x.UserId, x.GrapherProfileId }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(u => u.FavoriteGraphers)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.GrapherProfile)
                .WithMany(g => g.FavoritedByUsers)
                .HasForeignKey(x => x.GrapherProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        Seed(modelBuilder);
    }

    private static void Seed(ModelBuilder modelBuilder)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var vintageId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var koreanId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var minimalId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var streetId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var grapherUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var grapherProfileId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var packageId = Guid.Parse("40000000-0000-0000-0000-000000000001");

        modelBuilder.Entity<StyleTag>().HasData(
            new StyleTag { Id = vintageId, Name = "Vintage", Emoji = "📸", Color = "#C44569", CreatedAt = now },
            new StyleTag { Id = koreanId, Name = "Hàn Quốc", Emoji = "🎌", Color = "#FF6B9D", CreatedAt = now },
            new StyleTag { Id = minimalId, Name = "Minimal", Emoji = "⚪", Color = "#4A90E2", CreatedAt = now },
            new StyleTag { Id = streetId, Name = "Street", Emoji = "🏙️", Color = "#F5A623", CreatedAt = now });

        modelBuilder.Entity<User>().HasData(new User
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
        });

        modelBuilder.Entity<GrapherProfile>().HasData(new GrapherProfile
        {
            Id = grapherProfileId,
            UserId = grapherUserId,
            Bio = "Sinh viên đam mê chụp ảnh bằng điện thoại, chuyên ảnh lifestyle và vintage.",
            Location = "TP.HCM",
            KycStatus = KycStatus.Approved,
            IsVerified = true,
            IsOnline = true,
            AverageRating = 4.9m,
            ReviewCount = 234,
            CreatedAt = now
        });

        modelBuilder.Entity<GrapherStyleTag>().HasData(
            new GrapherStyleTag { GrapherProfileId = grapherProfileId, StyleTagId = vintageId },
            new GrapherStyleTag { GrapherProfileId = grapherProfileId, StyleTagId = koreanId });

        modelBuilder.Entity<GrapherPortfolioItem>().HasData(
            new GrapherPortfolioItem
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000001"),
                GrapherProfileId = grapherProfileId,
                ImageUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=400&h=400&fit=crop",
                DisplayOrder = 1,
                CreatedAt = now
            });

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
