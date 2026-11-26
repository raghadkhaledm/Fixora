using Fixora.Models;
using Microsoft.EntityFrameworkCore;

namespace Fixora.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Admin> Admins => Set<Admin>();

    public DbSet<HomeOwner> HomeOwners => Set<HomeOwner>();

    public DbSet<Technician> Technicians => Set<Technician>();

    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<TechnicianService> TechnicianServices => Set<TechnicianService>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingService> BookingServices => Set<BookingService>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<DisputeTicket> DisputeTickets => Set<DisputeTicket>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<FailedJob> FailedJobs => Set<FailedJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Admin>(e =>
        {
            e.ToTable("Admin", t =>
            {
                t.HasCheckConstraint("CK_Admin_Phone", "Phone_Number LIKE '05[034-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
            });
            e.HasKey(x => x.Admin_ID);
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Admin_Email");
            e.HasMany(x => x.Tickets).WithOne(t => t.Admin).HasForeignKey(t => t.Admin_ID).HasConstraintName("FK_Ticket_Admin");
        });

        modelBuilder.Entity<HomeOwner>(e =>
        {
            e.ToTable("Home_Owner", t =>
            {
                t.HasCheckConstraint("CK_HO_Phone", "Phone_Number LIKE '05[034-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
                t.HasCheckConstraint("CK_HO_City", "City IN ('Riyadh', 'Jeddah', 'Dammam')");
                t.HasCheckConstraint("CK_HO_Points", "Loyalty_Points >= 0");
            });
            e.HasKey(x => x.Home_owner_ID);
            e.Property(x => x.Loyalty_Points).HasDefaultValue(0);
            e.Property(x => x.Created_At).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_HO_Email");
            e.HasIndex(x => x.Phone_Number).IsUnique().HasDatabaseName("UQ_HO_Phone");
            e.HasMany(x => x.Bookings).WithOne(b => b.Homeowner).HasForeignKey(b => b.Home_owner_ID).HasConstraintName("FK_Booking_HO");
            e.HasMany(x => x.Reviews).WithOne(r => r.Homeowner).HasForeignKey(r => r.Home_owner_ID).HasConstraintName("FK_Review_HO");
            e.HasMany(x => x.Loyalty).WithOne(l => l.Homeowner).HasForeignKey(l => l.Home_owner_ID).HasConstraintName("FK_LT_HO");
        });

        modelBuilder.Entity<Technician>(e =>
        {
            e.ToTable("Technician", t =>
            {
                t.HasCheckConstraint("CK_Tech_Phone", "Phone_Number LIKE '05[034-9]-[0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
                t.HasCheckConstraint("CK_Tech_NationalID", "National_ID LIKE '[12][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
                t.HasCheckConstraint("CK_Tech_Verif", "Verification_Status IN ('Pending', 'Verified', 'Rejected')");
                t.HasCheckConstraint("CK_Tech_Rate", "Hourly_Rate BETWEEN 30 AND 500");
                t.HasCheckConstraint("CK_Tech_Rating", "Average_Rating BETWEEN 1 AND 5");
                t.HasCheckConstraint("CK_Tech_City", "City IN ('Riyadh', 'Jeddah', 'Dammam')");
                t.HasCheckConstraint("CK_Tech_Avail", "Is_Available = 0 OR Verification_Status = 'Verified'");
                t.HasCheckConstraint("CK_Tech_Verified", "(Verification_Status = 'Verified' AND Verified_At IS NOT NULL) OR Verification_Status <> 'Verified'");
            });
            e.HasKey(x => x.Technician_ID);
            e.Property(x => x.Verification_Status).HasDefaultValue("Pending");
            e.Property(x => x.Is_Available).HasDefaultValue(false);
            e.Property(x => x.Created_At).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_Tech_Email");
            e.HasIndex(x => x.Phone_Number).IsUnique().HasDatabaseName("UQ_Tech_Phone");
            e.HasIndex(x => x.National_ID).IsUnique().HasDatabaseName("UQ_Tech_NationalID");
            e.HasMany(x => x.Bookings).WithOne(b => b.Technician).HasForeignKey(b => b.Technician_ID).HasConstraintName("FK_Booking_Tech");
            e.HasMany(x => x.Reviews).WithOne(r => r.Technician).HasForeignKey(r => r.Technician_ID).HasConstraintName("FK_Review_Tech");
            e.HasMany(x => x.Services).WithMany(s => s.Technicians).UsingEntity<TechnicianService>(
                j => j.HasOne(ts => ts.Service).WithMany().HasForeignKey(ts => ts.Service_ID).HasConstraintName("FK_TS_Service"),
                j => j.HasOne(ts => ts.Technician).WithMany().HasForeignKey(ts => ts.Technician_ID).HasConstraintName("FK_TS_Technician"));
        });

        modelBuilder.Entity<ServiceType>(e =>
        {
            e.ToTable("Service_Type");
            e.HasKey(x => x.Service_Type_ID);
            e.HasIndex(x => x.Type_Name).IsUnique().HasDatabaseName("UQ_Service_Type");
            e.HasMany(x => x.Services).WithOne(s => s.Type).HasForeignKey(s => s.Service_Type_ID).HasConstraintName("FK_Service_Type");
        });

        modelBuilder.Entity<Service>(e =>
        {
            e.ToTable("Service", t =>
            {
                t.HasCheckConstraint("CK_Service_Category", "Service_Category IN ('Repair', 'Installation', 'Inspection', 'Delivery', 'Assembly', 'Painting')");
                t.HasCheckConstraint("CK_Service_Price", "Price BETWEEN 10 AND 20000");
                t.HasCheckConstraint("CK_Service_Fee", "Service_Commission_Fee BETWEEN 0 AND 500");
                t.HasCheckConstraint("CK_Service_Unit", "Unit IN ('job', 'hr', 'cylinder', 'room')");
                t.HasCheckConstraint("CK_Service_FlexUnit", "(Flexible_Rate = 1 AND Unit = 'hr') OR (Flexible_Rate = 0 AND Unit <> 'hr')");
            });
            e.HasKey(x => x.Service_ID);
            e.Property(x => x.Flexible_Rate).HasDefaultValue(false);
            e.Property(x => x.Service_Commission_Fee).HasDefaultValue(15.0);
            e.Property(x => x.Unit).HasDefaultValue("job");
            e.Property(x => x.Is_Active).HasDefaultValue(true);
            e.HasIndex(x => new { x.Service_Type_ID, x.Service_Name }).IsUnique().HasDatabaseName("UQ_Service");
            e.HasMany(x => x.BookingServices).WithOne(b => b.Service).HasForeignKey(b => b.Service_ID).HasConstraintName("FK_BS_Service");
        });

        modelBuilder.Entity<TechnicianService>(e =>
        {
            e.ToTable("Technician_Service");
            e.HasKey(x => x.Technician_Service_ID);
            e.HasIndex(x => new { x.Technician_ID, x.Service_ID }).IsUnique().HasDatabaseName("UQ_Technician_Service");
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Booking", t =>
            {
                t.HasCheckConstraint("CK_Booking_Type", "Booking_Type IN ('Express', 'Scheduled')");
                t.HasCheckConstraint("CK_Booking_Status", "Status IN ('Pending', 'Accepted', 'Rejected', 'Cancelled', 'Completed')");
                t.HasCheckConstraint("CK_Booking_Progress", "Job_Progress_Status IN ('Requested', 'Accepted', 'On the way', 'In progress', 'Completed')");
                t.HasCheckConstraint("CK_Booking_Consistency", "(Status IN ('Pending', 'Rejected', 'Cancelled') AND Job_Progress_Status IN ('Requested', 'Accepted', 'On the way')) OR (Status = 'Accepted' AND Job_Progress_Status IN ('Accepted', 'On the way', 'In progress')) OR (Status = 'Completed' AND Job_Progress_Status = 'Completed' AND Completed_At IS NOT NULL)");
                t.HasCheckConstraint("CK_Booking_Slot", "Booking_Type = 'Express' OR (DATEPART(MINUTE, Scheduled_Date_Time) = 0 AND DATEPART(HOUR, Scheduled_Date_Time) IN (8, 10, 12, 14, 17, 19))");
                t.HasCheckConstraint("CK_Booking_Times", "Accepted_At IS NULL OR Accepted_At >= Created_At");
            });
            e.HasKey(x => x.Booking_ID);
            e.Property(x => x.Status).HasDefaultValue("Pending");
            e.Property(x => x.Job_Progress_Status).HasDefaultValue("Requested");
            e.Property(x => x.Created_At).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => new { x.Home_owner_ID, x.Created_At }).IsDescending(false, true).HasDatabaseName("IX_Booking_HO");
            e.HasIndex(x => new { x.Technician_ID, x.Scheduled_Date_Time }).HasDatabaseName("IX_Booking_Tech");
            e.HasMany(x => x.Items).WithOne(i => i.Booking).HasForeignKey(i => i.Booking_ID).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_BS_Booking");
            e.HasOne(x => x.Invoice).WithOne(i => i.Booking).HasForeignKey<Invoice>(i => i.Booking_ID).HasConstraintName("FK_Invoice_Booking");
            e.HasOne(x => x.Review).WithOne(r => r.Booking).HasForeignKey<Review>(r => r.Booking_ID).HasConstraintName("FK_Review_Booking");
            e.HasOne(x => x.Dispute).WithOne(d => d.Booking).HasForeignKey<DisputeTicket>(d => d.Booking_ID).HasConstraintName("FK_Ticket_Booking");
        });

        modelBuilder.Entity<BookingService>(e =>
        {
            e.ToTable("Booking_Service", t =>
            {
                t.HasCheckConstraint("CK_BS_Qty", "Quantity BETWEEN 1 AND 10");
                t.HasCheckConstraint("CK_BS_Price", "Agreed_Price > 0");
            });
            e.HasKey(x => x.Booking_Service_ID);
            e.HasIndex(x => new { x.Booking_ID, x.Service_ID }).IsUnique().HasDatabaseName("UQ_Booking_Service");
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoice", t =>
            {
                t.HasCheckConstraint("CK_Inv_Amounts", "Labor_Cost >= 0 AND Material_Cost >= 0 AND Service_Commission_Fee >= 0 AND Express_Fee IN (0, 10) AND Points_Discount >= 0");
                t.HasCheckConstraint("CK_Inv_Total", "Total_Amount = Labor_Cost + Material_Cost + Service_Commission_Fee + Express_Fee - Points_Discount AND Total_Amount > 0");
                t.HasCheckConstraint("CK_Inv_Method", "Payment_Method IN ('Mada', 'STC Pay', 'Card', 'Cash')");
                t.HasCheckConstraint("CK_Inv_Status", "Payment_Status IN ('Unpaid', 'Paid', 'Refunded', 'Partially Refunded')");
                t.HasCheckConstraint("CK_Inv_Paid", "(Payment_Status = 'Unpaid' AND Paid_At IS NULL) OR (Payment_Status <> 'Unpaid' AND Paid_At IS NOT NULL AND Payment_Method IS NOT NULL)");
                t.HasCheckConstraint("CK_Inv_Otp", "Cash_OTP_Code IS NULL OR Cash_OTP_Code LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]'");
                t.HasCheckConstraint("CK_Inv_OtpAtt", "OTP_Attempts BETWEEN 0 AND 3");
                t.HasCheckConstraint("CK_Inv_Ref", "Payment_Status = 'Unpaid' OR Payment_Method = 'Cash' OR Transaction_Ref IS NOT NULL");
            });
            e.HasKey(x => x.Invoice_ID);
            e.Property(x => x.Material_Cost).HasDefaultValue(0.0);
            e.Property(x => x.Express_Fee).HasDefaultValue(0.0);
            e.Property(x => x.Payment_Status).HasDefaultValue("Unpaid");
            e.Property(x => x.Points_Discount).HasDefaultValue(0.0);
            e.Property(x => x.Issued_At).HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.OTP_Attempts).HasDefaultValue(0);
            e.HasIndex(x => x.Booking_ID).IsUnique().HasDatabaseName("UQ_Invoice_Booking");
            e.HasIndex(x => x.Transaction_Ref).IsUnique().HasFilter("[Transaction_Ref] IS NOT NULL").HasDatabaseName("UX_Invoice_Ref");
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("Review", t =>
            {
                t.HasCheckConstraint("CK_Review_Score", "Rating_Score BETWEEN 1 AND 5");
            });
            e.HasKey(x => x.Review_ID);
            e.Property(x => x.Created_At).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.Booking_ID).IsUnique().HasDatabaseName("UQ_Review_Booking");
            e.HasIndex(x => new { x.Technician_ID, x.Created_At }).IsDescending(false, true).HasDatabaseName("IX_Review_Tech");
        });

        modelBuilder.Entity<DisputeTicket>(e =>
        {
            e.ToTable("Dispute_Ticket", t =>
            {
                t.HasCheckConstraint("CK_Ticket_Status", "Ticket_Status IN ('Open', 'Under Review', 'Resolved', 'Rejected')");
                t.HasCheckConstraint("CK_Ticket_Type", "Problem_Type IN ('Problem came back', 'Charged more than agreed', 'Damage to my home', 'Technician behavior', 'Other')");
                t.HasCheckConstraint("CK_Ticket_Refund", "Refund_Amount IS NULL OR Refund_Amount >= 0");
                t.HasCheckConstraint("CK_Ticket_Admin", "Ticket_Status = 'Open' OR Admin_ID IS NOT NULL");
                t.HasCheckConstraint("CK_Ticket_Resolved", "(Ticket_Status IN ('Resolved', 'Rejected') AND Resolved_At IS NOT NULL) OR (Ticket_Status IN ('Open', 'Under Review') AND Resolved_At IS NULL)");
            });
            e.HasKey(x => x.Ticket_ID);
            e.Property(x => x.Ticket_Status).HasDefaultValue("Open");
            e.Property(x => x.Created_At).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.Booking_ID).IsUnique().HasDatabaseName("UQ_Ticket_Booking");
        });

        modelBuilder.Entity<LoyaltyTransaction>(e =>
        {
            e.ToTable("Loyalty_Transaction", t =>
            {
                t.HasCheckConstraint("CK_LT_Type", "Transaction_Type IN ('Earned', 'Redeemed', 'Reversed')");
                t.HasCheckConstraint("CK_LT_Points", "(Transaction_Type = 'Earned' AND Points_Earned > 0 AND Points_Redeemed = 0) OR (Transaction_Type IN ('Redeemed', 'Reversed') AND Points_Redeemed > 0 AND Points_Earned = 0)");
            });
            e.HasKey(x => x.Transaction_ID);
            e.Property(x => x.Points_Earned).HasDefaultValue(0);
            e.Property(x => x.Points_Redeemed).HasDefaultValue(0);
            e.Property(x => x.Transaction_Date).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => new { x.Home_owner_ID, x.Transaction_Date }).IsDescending(false, true).HasDatabaseName("IX_LT_HO");
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.Booking_ID).HasConstraintName("FK_LT_Booking");
        });

        modelBuilder.Entity<FailedJob>(e =>
        {
            e.ToTable("failed_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Failed_At).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.Uuid).IsUnique().HasDatabaseName("UQ_failed_jobs_uuid");
        });
    }
}
