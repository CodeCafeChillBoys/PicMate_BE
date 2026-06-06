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

        modelBuilder.SeedData();
    }
}
