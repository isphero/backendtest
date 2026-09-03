// Data/AppDbContext.cs
// HIGH-PERFORMANCE DbContext for large-scale gaming database
// Key optimizations:
//   - QueryTrackingBehavior.NoTracking by default (huge read perf boost)
//   - AsTracking() only when writing
//   - Strategic composite indexes on hot query paths
//   - Compiled queries for critical hot paths (login, market listing)
//   - Connection resiliency + command timeout configured
//   - No lazy loading (prevents N+1 silently)
//   - Table/column naming conventions enforced
//   - Proper precision on decimals to avoid silent rounding

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GameRealmAPI.Models;

namespace GameRealmAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // PERF: Disable change tracking by default — all reads are NoTracking
        // Services that need to write call .AsTracking() explicitly
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    // ── DbSets ────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<StoreItem> StoreItems => Set<StoreItem>();
    public DbSet<CoinPackage> CoinPackages => Set<CoinPackage>();
    public DbSet<PlayerWallet> PlayerWallets => Set<PlayerWallet>();
    public DbSet<PurchaseLog> PurchaseLogs => Set<PurchaseLog>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SeedVersion> SeedVersions => Set<SeedVersion>();
    public DbSet<MarketListing> MarketListings => Set<MarketListing>();
    public DbSet<SellerStall> SellerStalls => Set<SellerStall>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<WikiCategory> WikiCategories => Set<WikiCategory>();
    public DbSet<WikiRevision> WikiRevisions => Set<WikiRevision>();
    public DbSet<WikiComment> WikiComments => Set<WikiComment>();
    public DbSet<WikiReview> WikiReviews => Set<WikiReview>();
    public DbSet<WikiReaction> WikiReactions => Set<WikiReaction>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        // ── USER ────────────────────────────────────────────────────
        m.Entity<User>(e =>
        {
            e.ToTable("accounts"); // Table name from screenshot
            e.HasKey(u => u.Id);
            e.Property(u => u.Id)
             .HasColumnName("EntityID")
             .ValueGeneratedOnAdd(); // Let MySQL handle the ID generation
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.Email).HasMaxLength(120).IsRequired();
            e.Property(u => u.Password).HasColumnName("Password").HasMaxLength(255).IsRequired();
            e.Property(u => u.Role)
             .HasColumnName("Role")
             .HasConversion<byte>()
             .HasDefaultValue(AccountRole.Player)
             .IsRequired();
            e.Property(u => u.IP).HasMaxLength(50);
            e.Property(u => u.HWID).HasMaxLength(100);
            e.Property(u => u.ResetToken).HasMaxLength(100);
            // Indexes
            e.HasIndex(u => u.Username).IsUnique().HasDatabaseName("IX_accounts_Username");
            e.HasIndex(u => u.Email).HasDatabaseName("IX_accounts_Email");
            e.HasIndex(u => new { u.IsActive, u.IsBanned }).HasDatabaseName("IX_accounts_Status");
        });

        // ── PLAYER ──────────────────────────────────────────────────
        m.Entity<Player>(e =>
        {
            e.ToTable("Players");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedOnAdd();
            e.Property(p => p.Name).HasMaxLength(50).IsRequired();
            e.Property(p => p.Class).HasMaxLength(30).IsRequired();
            // Indexes — rankings hot path
            e.HasIndex(p => p.Name).IsUnique().HasDatabaseName("IX_Players_Name");
            e.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("IX_Players_UserId");
            e.HasIndex(p => p.Kills).HasDatabaseName("IX_Players_Kills");
            e.HasIndex(p => new { p.Level, p.Kills }).HasDatabaseName("IX_Players_Ranking");
            e.HasIndex(p => p.IsOnline).HasDatabaseName("IX_Players_Online");
            // Relations
            e.HasOne(p => p.User).WithOne(u => u.Player)
             .HasForeignKey<Player>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Guild).WithMany(g => g.Members)
             .HasForeignKey(p => p.GuildId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ── GUILD ───────────────────────────────────────────────────
        m.Entity<Guild>(e =>
        {
            e.ToTable("Guilds");
            e.HasKey(g => g.Id);
            e.Property(g => g.Id).ValueGeneratedOnAdd();
            e.Property(g => g.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(g => g.Name).IsUnique().HasDatabaseName("IX_Guilds_Name");
            e.HasOne(g => g.Leader).WithMany()
             .HasForeignKey(g => g.LeaderPlayerId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── STORE ITEM ──────────────────────────────────────────────
        m.Entity<StoreItem>(e =>
        {
            e.ToTable("StoreItems");
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).HasMaxLength(100).IsRequired();
            e.Property(i => i.Category).HasMaxLength(30).IsRequired();
            e.Property(i => i.Icon).HasMaxLength(10);
            e.Property(i => i.Badge).HasMaxLength(20);
            e.Property(i => i.BadgeType).HasMaxLength(20);
            e.HasIndex(i => new { i.IsActive, i.Category }).HasDatabaseName("IX_StoreItems_Active_Category");
        });

        // ── COIN PACKAGE ────────────────────────────────────────────
        m.Entity<CoinPackage>(e =>
        {
            e.ToTable("CoinPackages");
            e.HasKey(p => p.Id);
            e.Property(p => p.Price).HasPrecision(10, 2).IsRequired();
            e.HasIndex(p => p.IsActive).HasDatabaseName("IX_CoinPackages_Active");
        });

        // ── PLAYER WALLET ───────────────────────────────────────────
        m.Entity<PlayerWallet>(e =>
        {
            e.ToTable("PlayerWallets");
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.UserId).IsUnique().HasDatabaseName("IX_Wallets_UserId");
            e.HasOne(w => w.User).WithMany()
             .HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── PURCHASE LOG ────────────────────────────────────────────
        m.Entity<PurchaseLog>(e =>
        {
            e.ToTable("PurchaseLogs");
            e.HasKey(l => l.Id);
            e.Property(l => l.Type).HasMaxLength(20).IsRequired();
            e.Property(l => l.Status).HasMaxLength(20).IsRequired();
            // Composite index — user history query
            e.HasIndex(l => new { l.UserId, l.CreatedAt }).HasDatabaseName("IX_PurchaseLogs_User_Date");
            e.HasOne(l => l.User).WithMany()
             .HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.StoreItem).WithMany()
             .HasForeignKey(l => l.StoreItemId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        // ── PAYMENT ─────────────────────────────────────────────────
        m.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasPrecision(10, 2).IsRequired();
            e.Property(p => p.Method).HasMaxLength(20).IsRequired();
            e.Property(p => p.Currency).HasMaxLength(5).HasDefaultValue("USD");
            e.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.ExternalTransactionId).HasMaxLength(100);
            e.HasIndex(p => p.ExternalTransactionId).HasDatabaseName("IX_Payments_ExternalId");
            e.HasIndex(p => new { p.UserId, p.Status }).HasDatabaseName("IX_Payments_User_Status");
            e.HasOne(p => p.User).WithMany()
             .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.CoinPackage).WithMany()
             .HasForeignKey(p => p.CoinPackageId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── SEED VERSION ────────────────────────────────────────────
        m.Entity<SeedVersion>(e =>
        {
            e.ToTable("SeedVersions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Version).HasMaxLength(20).IsRequired();
        });

        // ── MARKET LISTING ──────────────────────────────────────────
        m.Entity<MarketListing>(e =>
        {
            e.ToTable("MarketListings");
            e.HasKey(ml => ml.Id);
            e.Property(ml => ml.ItemName).HasMaxLength(100).IsRequired();
            e.Property(ml => ml.Category).HasMaxLength(30).IsRequired();
            e.Property(ml => ml.Quality).HasMaxLength(20).HasDefaultValue("Normal");
            e.Property(ml => ml.SellerName).HasMaxLength(50).IsRequired();
            e.Property(ml => ml.Gem1).HasMaxLength(50).HasDefaultValue("NoSocket");
            e.Property(ml => ml.Gem2).HasMaxLength(50).HasDefaultValue("NoSocket");
            e.Property(ml => ml.Currency).HasMaxLength(10).HasDefaultValue("gold");
            // Composite indexes — market filter hot paths
            e.HasIndex(ml => new { ml.IsActive, ml.Category }).HasDatabaseName("IX_Market_Active_Category");
            e.HasIndex(ml => new { ml.IsActive, ml.Quality }).HasDatabaseName("IX_Market_Active_Quality");
            e.HasIndex(ml => new { ml.IsActive, ml.Price }).HasDatabaseName("IX_Market_Active_Price");
            e.HasIndex(ml => new { ml.IsActive, ml.Plus }).HasDatabaseName("IX_Market_Active_Plus");
            e.HasOne(ml => ml.Seller).WithMany()
             .HasForeignKey(ml => ml.SellerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── SELLER STALL ────────────────────────────────────────────
        m.Entity<SellerStall>(e =>
        {
            e.ToTable("SellerStalls");
            e.HasKey(s => s.Id);
            e.Property(s => s.SellerName).HasMaxLength(50).IsRequired();
            e.HasIndex(s => s.SellerName).IsUnique().HasDatabaseName("IX_SellerStalls_Name");
            e.HasIndex(s => s.StallNumber).IsUnique().HasDatabaseName("IX_SellerStalls_Number");
        });

        // ── NEWS ARTICLE ────────────────────────────────────────────
        m.Entity<NewsArticle>(e =>
        {
            e.ToTable("NewsArticles");
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(200).IsRequired();
            e.Property(n => n.Category).HasMaxLength(20).IsRequired();
            e.Property(n => n.Author).HasMaxLength(50).IsRequired();
            e.Property(n => n.Excerpt).HasMaxLength(500);
            // Composite index — public feed query
            e.HasIndex(n => new { n.IsPublished, n.CreatedAt }).HasDatabaseName("IX_News_Published_Date");
            e.HasIndex(n => new { n.IsPublished, n.Category }).HasDatabaseName("IX_News_Published_Category");
            e.HasOne(n => n.AuthorUser).WithMany()
             .HasForeignKey(n => n.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── WIKI CATEGORY ───────────────────────────────────────────
        m.Entity<WikiCategory>(e =>
        {
            e.ToTable("WikiCategories");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(80).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(80).IsRequired();
            e.Property(c => c.Icon).HasMaxLength(10);
            e.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("IX_WikiCategories_Slug");
            e.HasIndex(c => c.SortOrder).HasDatabaseName("IX_WikiCategories_Sort");
            // Self-referencing parent
            e.HasOne(c => c.Parent).WithMany(c => c.Children)
             .HasForeignKey(c => c.ParentId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        // ── WIKI PAGE ───────────────────────────────────────────────
        m.Entity<WikiPage>(e =>
        {
            e.ToTable("WikiPages");
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).HasMaxLength(200).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(200).IsRequired();
            e.Property(p => p.LastEditedBy).HasMaxLength(50);
            // Indexes — slug lookup + category listing
            e.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("IX_WikiPages_Slug");
            e.HasIndex(p => p.SortOrder).HasDatabaseName("IX_WikiPages_Sort");
            e.HasIndex(p => new { p.CategoryId, p.IsPublished }).HasDatabaseName("IX_WikiPages_Category");
            e.HasIndex(p => new { p.IsPublished, p.UpdatedAt }).HasDatabaseName("IX_WikiPages_Published_Date");
            // Full-text index hint (applied via SQL, EF doesn't support FTS natively)
            e.HasOne(p => p.Category).WithMany(c => c.Pages)
             .HasForeignKey(p => p.CategoryId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.ParentPage).WithMany(p => p.ChildPages)
             .HasForeignKey(p => p.ParentPageId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.AuthorUser).WithMany()
             .HasForeignKey(p => p.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── WIKI REVISION ───────────────────────────────────────────
        m.Entity<WikiRevision>(e =>
        {
            e.ToTable("WikiRevisions");
            e.HasKey(r => r.Id);
            e.Property(r => r.EditedBy).HasMaxLength(50).IsRequired();
            e.Property(r => r.EditSummary).HasMaxLength(300);
            e.HasIndex(r => new { r.PageId, r.CreatedAt }).HasDatabaseName("IX_WikiRevisions_Page_Date");
            e.HasOne(r => r.Page).WithMany(p => p.Revisions)
             .HasForeignKey(r => r.PageId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── WIKI COMMENT ───────────────────────────────────────────
        m.Entity<WikiComment>(e =>
        {
            e.ToTable("WikiComments");
            e.HasKey(c => c.Id);
            e.Property(c => c.AuthorName).HasMaxLength(100);
            e.Property(c => c.Content).IsRequired();
            e.HasIndex(c => new { c.PageId, c.CreatedAt });
            e.HasOne(c => c.Page).WithMany().HasForeignKey(c => c.PageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Parent).WithMany(c => c.Replies).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── WIKI REVIEW ────────────────────────────────────────────
        m.Entity<WikiReview>(e =>
        {
            e.ToTable("WikiReviews");
            e.HasKey(r => r.Id);
            e.Property(r => r.AuthorName).HasMaxLength(100);
            e.Property(r => r.Content).IsRequired();
            e.HasIndex(r => new { r.PageId, r.CreatedAt });
            e.HasOne(r => r.Page).WithMany().HasForeignKey(r => r.PageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── WIKI REACTION ──────────────────────────────────────────
        m.Entity<WikiReaction>(e =>
        {
            e.ToTable("WikiReactions");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedOnAdd();
            e.Property(r => r.ReactionType).HasMaxLength(20).IsRequired();
            e.HasIndex(r => new { r.PageId, r.ReactionType });
            e.HasIndex(r => new { r.PageId, r.UserId, r.ReactionType }).IsUnique();
            e.HasOne(r => r.Page).WithMany().HasForeignKey(r => r.PageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    // ── COMPILED QUERIES (hot paths — skip EF query compilation overhead) ──
    // Used directly in services for the most frequent queries

    public static readonly Func<AppDbContext, string, Task<User?>> GetUserByUsername =
        EF.CompileAsyncQuery((AppDbContext db, string username) =>
            db.Users.FirstOrDefault(u => u.Username == username && u.IsActive == true));

    public static readonly Func<AppDbContext, string, Task<User?>> GetUserByEmailOrUsername =
        EF.CompileAsyncQuery((AppDbContext db, string value) =>
            db.Users.FirstOrDefault(u =>
                (u.Username == value || u.Email == value) && u.IsActive == true));

    public static readonly Func<AppDbContext, int, Task<PlayerWallet?>> GetWalletByUserId =
        EF.CompileAsyncQuery((AppDbContext db, int userId) =>
            db.PlayerWallets.FirstOrDefault(w => w.UserId == userId));

    public static readonly Func<AppDbContext, int, Task<int>> GetOnlinePlayerCount =
        EF.CompileAsyncQuery((AppDbContext db, int _) =>
            db.Players.Count(p => p.IsOnline == true));
}
