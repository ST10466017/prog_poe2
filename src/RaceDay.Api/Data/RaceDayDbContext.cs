using System.Text.RegularExpressions;

namespace RaceDay.Api.Data;

/// <summary>
/// Code-first model. Table and column names, lengths, defaults, keys and CHECK / UNIQUE constraints
/// are configured to match the Part 1 ERD and docs/RaceDay_Database_Schema.sql.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[role] IN ('Organiser','Participant')"));
            e.HasKey(x => x.UserId).HasName("PK_Users");
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired().HasDefaultValue(Roles.Participant);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Status", "[status] IN ('Upcoming','Completed','Cancelled')");
                t.HasCheckConstraint("CK_Events_Type", "[event_type] IN ('Run','Walk','Cycle')");
                t.HasCheckConstraint("CK_Events_Distance", "[distance_km] > 0");
            });
            e.HasKey(x => x.EventId).HasName("PK_Events");
            e.Property(x => x.EventName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
            e.Property(x => x.EventType).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EventStatuses.Upcoming);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Distance", "[distance_km] IS NULL OR [distance_km] > 0");
                t.HasCheckConstraint("CK_Categories_Type", "[category_type] IN ('Age','Distance')");
                t.HasCheckConstraint("CK_Categories_MaxParticipants", "[max_participants] > 0");
            });
            e.HasKey(x => x.CategoryId).HasName("PK_Categories");
            e.Property(x => x.CategoryName).HasMaxLength(50).IsRequired();
            e.Property(x => x.CategoryType).HasMaxLength(10).IsRequired().HasDefaultValue(CategoryTypes.Distance);
            e.Property(x => x.DistanceKm).HasColumnType("decimal(5,2)");
            e.Property(x => x.EntryFee).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            e.Property(x => x.MaxParticipants).HasDefaultValue(100);
            e.HasIndex(x => new { x.EventId, x.CategoryName }).IsUnique().HasDatabaseName("UQ_Categories_Event_Name");
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId).HasConstraintName("FK_Categories_Event")
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[status] IN ('Pending','Confirmed','Cancelled')"));
            e.HasKey(x => x.EnrolmentId).HasName("PK_Enrolments");
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(x => x.RaceNumber).HasMaxLength(10);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EnrolmentStatuses.Confirmed);
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique().HasDatabaseName("UQ_Enrolments_Participant_Category");
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).HasConstraintName("FK_Enrolments_Category")
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[status] IN ('Not Started','DNS','DNF','Finished')"));
            e.HasKey(x => x.ResultId).HasName("PK_Results");
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue(ResultStatuses.NotStarted);
            e.Property(x => x.RecordedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            // One-to-one: UNIQUE foreign key
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId).HasConstraintName("FK_Results_Enrolment")
                .OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Payment>(e =>
        {
            e.ToTable("Payments", t =>
            {
                t.HasCheckConstraint("CK_Payments_Method", "[payment_method] IN ('Card','EFT','Cash')");
                t.HasCheckConstraint("CK_Payments_Status", "[payment_status] IN ('Pending','Paid','Refunded')");
                t.HasCheckConstraint("CK_Payments_Amount", "[amount] >= 0");
            });
            e.HasKey(x => x.PaymentId).HasName("PK_Payments");
            e.Property(x => x.Amount).HasColumnType("decimal(8,2)");
            e.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
            e.Property(x => x.PaymentStatus).HasMaxLength(20).IsRequired().HasDefaultValue(PaymentStatuses.Pending);
            e.Property(x => x.PaidAt).HasColumnType("datetime");
            e.HasOne(x => x.Enrolment).WithOne(en => en.Payment)
                .HasForeignKey<Payment>(x => x.EnrolmentId).HasConstraintName("FK_Payments_Enrolment")
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Column naming: PascalCase property -> snake_case column (UserId -> user_id) to match the SQL script.
        foreach (var entityType in mb.Model.GetEntityTypes())
            foreach (var property in entityType.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
