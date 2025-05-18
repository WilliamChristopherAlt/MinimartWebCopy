using Microsoft.EntityFrameworkCore;
using MinimartWeb.Model;
using MinimartWeb.Models; // Đảm bảo namespace của các lớp Model là đúng

namespace MinimartWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<MeasurementUnit> MeasurementUnits { get; set; } = null!;
        public DbSet<ProductType> ProductTypes { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<ProductTag> ProductTags { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<EmployeeRole> EmployeeRoles { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<EmployeeAccount> EmployeeAccounts { get; set; } = null!;
        public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<SaleDetail> SaleDetails { get; set; } = null!;
        public DbSet<OtpType> OtpTypes { get; set; } = null!;
        public DbSet<OtpRequest> OtpRequests { get; set; } = null!;
        public DbSet<ViewHistory> ViewHistories { get; set; } = null!;
        public DbSet<SearchHistory> SearchHistories { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<LoginAttempt> LoginAttempts { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === Cấu hình cho Entity: Category ===
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.CategoryID);
                entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.CategoryName).IsUnique();
                // Property CategoryDescription dùng kiểu TEXT mặc định, không cần cấu hình trừ khi muốn đổi
            });

            // === Cấu hình cho Entity: Supplier ===
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasKey(e => e.SupplierID);
                entity.Property(e => e.SupplierName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.SupplierPhoneNumber).IsRequired().HasMaxLength(10).IsFixedLength(); // CHAR(10)
                entity.Property(e => e.SupplierAddress).IsRequired().HasMaxLength(255);
                entity.Property(e => e.SupplierEmail).IsRequired().HasMaxLength(255);

                entity.HasIndex(e => e.SupplierName).IsUnique();
                entity.HasIndex(e => e.SupplierPhoneNumber).IsUnique();
                entity.HasIndex(e => e.SupplierEmail).IsUnique();
            });

            // === Cấu hình cho Entity: MeasurementUnit ===
            modelBuilder.Entity<MeasurementUnit>(entity =>
            {
                entity.HasKey(e => e.MeasurementUnitID);
                entity.Property(e => e.UnitName).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.UnitName).IsUnique();
                // IsContinuous BIT NOT NULL (mặc định của bool)
                // UnitDescription TEXT NULL
            });

            // === Cấu hình cho Entity: Customer ===
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.CustomerID);
                // ... (các property FirstName, LastName, etc. với [Required] và [StringLength] đã đủ)
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(64); // VARBINARY(64)
                entity.Property(e => e.Salt).IsRequired().HasMaxLength(64);       // VARBINARY(64)
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(10).IsFixedLength();

                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.PhoneNumber).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();

                // Navigation properties (config bên dưới nếu cần cho ViewHistory, SearchHistory, Sales, OtpRequests)
            });

            // === Cấu hình cho Entity: ProductType ===
            modelBuilder.Entity<ProductType>(entity =>
            {
                entity.HasKey(e => e.ProductTypeID);
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ProductDescription).IsRequired(); // NVARCHAR(MAX) không cần HasMaxLength
                entity.Property(e => e.Price).IsRequired().HasColumnType("decimal(10, 2)");
                entity.Property(e => e.StockAmount).IsRequired().HasColumnType("decimal(10, 2)").HasDefaultValue(0);
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(1);
                entity.Property(e => e.DateAdded).IsRequired().HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.ProductName).IsUnique();

                entity.HasCheckConstraint("CK_ProductTypes_Price", "[Price] >= 0");
                entity.HasCheckConstraint("CK_ProductTypes_StockAmount", "[StockAmount] >= 0");
                if (modelBuilder.Model.FindEntityType(typeof(ProductType))?.FindProperty(nameof(ProductType.ExpirationDurationDays)) != null)
                {
                    entity.HasCheckConstraint("CK_ProductTypes_ExpirationDurationDays", "[ExpirationDurationDays] >= 0 OR [ExpirationDurationDays] IS NULL");
                }
                entity.HasCheckConstraint("CK_ProductTypes_OriginalPrice", "[OriginalPrice] >= 0 OR [OriginalPrice] IS NULL");

                // Relationships
                entity.HasOne(d => d.Category)
                    .WithMany(p => p.ProductTypes)
                    .HasForeignKey(d => d.CategoryID)
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict nếu bạn muốn

                entity.HasOne(d => d.Supplier)
                    .WithMany(p => p.ProductTypes)
                    .HasForeignKey(d => d.SupplierID)
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict

                entity.HasOne(d => d.MeasurementUnit)
                    .WithMany(p => p.ProductTypes)
                    .HasForeignKey(d => d.MeasurementUnitID)
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict
            });

            // === Cấu hình cho Entity: EmployeeRole ===
            modelBuilder.Entity<EmployeeRole>(entity =>
            {
                entity.HasKey(e => e.RoleID);
                entity.Property(e => e.RoleName).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.RoleName).IsUnique();
            });

            // === Cấu hình cho Entity: Employee ===
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeID);
                // ... (các property cơ bản)
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(10).IsFixedLength();
                if (entity.Metadata.FindProperty(nameof(Employee.Salary)) != null)
                {
                    entity.Property(e => e.Salary).HasColumnType("decimal(10, 2)"); // DECIMAL(10,2) trong SQL
                    entity.HasCheckConstraint("CK_Employees_Salary", "[Salary] >= 0 OR [Salary] IS NULL");
                }
                entity.Property(e => e.HireDate).IsRequired().HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Gender).IsRequired().HasMaxLength(20);
                entity.HasCheckConstraint("CK_Employees_Gender", "[Gender] IN ('Male', 'Female', 'Non-Binary', 'Prefer not to say')");

                entity.HasIndex(e => e.CitizenID).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.PhoneNumber).IsUnique();

                // Relationship với EmployeeRole
                entity.HasOne(d => d.Role)
                    .WithMany(p => p.Employees)
                    .HasForeignKey(d => d.RoleID)
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict

                // Relationship Một-Một với EmployeeAccount (khai báo ở đây hoặc từ EmployeeAccount)
                entity.HasOne(e => e.EmployeeAccount)
                      .WithOne(ea => ea.Employee)
                      .HasForeignKey<EmployeeAccount>(ea => ea.EmployeeID) // Khóa ngoại là EmployeeID trong EmployeeAccount
                      .IsRequired() // Vì mối quan hệ 1-1 dựa trên FK not null
                      .OnDelete(DeleteBehavior.Cascade); // Nếu Employee bị xóa, EmployeeAccount cũng bị xóa
            });

            // === Cấu hình cho Entity: EmployeeAccount ===
            modelBuilder.Entity<EmployeeAccount>(entity =>
            {
                entity.HasKey(e => e.AccountID);
                // EmployeeID là khóa ngoại và cũng là unique (vì là 1-1 với Employee)
                entity.Property(e => e.EmployeeID).IsRequired();
                entity.HasIndex(e => e.EmployeeID).IsUnique();

                entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Username).IsUnique();

                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Salt).IsRequired().HasMaxLength(64);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                // IsActive, IsAdmin, IsEmailVerified, EmailVerifiedAt sẽ dùng default của kiểu bool/DateTime?

                // Relationship với Employee đã được định nghĩa từ phía Employee
                // Hoặc bạn có thể định nghĩa ở đây:
                // entity.HasOne(d => d.Employee)
                //       .WithOne(p => p.EmployeeAccount) // p là đối tượng Employee
                //       .HasForeignKey<EmployeeAccount>(d => d.EmployeeID)
                //       .IsRequired()
                //       .OnDelete(DeleteBehavior.Cascade);
            });

            // === Cấu hình cho Entity: PaymentMethod ===
            modelBuilder.Entity<PaymentMethod>(entity =>
            {
                entity.HasKey(e => e.PaymentMethodID);
                entity.Property(e => e.MethodName).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.MethodName).IsUnique();
            });

            // === Cấu hình cho Entity: Sale ===
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasKey(e => e.SaleID);
                entity.Property(e => e.SaleDate).IsRequired().HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.OrderStatus).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
                entity.HasCheckConstraint("CK_Sales_OrderStatus", "[OrderStatus] IN ('Pending', 'Confirmed', 'Processing', 'Completed', 'Cancelled')");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.Sales)
                    .HasForeignKey(d => d.CustomerID)
                    .OnDelete(DeleteBehavior.SetNull); // CustomerID có thể NULL

                entity.HasOne(d => d.Employee)
                    .WithMany(p => p.Sales)
                    .HasForeignKey(d => d.EmployeeID)
                    .IsRequired() // EmployeeID là NOT NULL
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict

                entity.HasOne(d => d.PaymentMethod)
                    .WithMany(p => p.Sales)
                    .HasForeignKey(d => d.PaymentMethodID)
                    .IsRequired() // PaymentMethodID là NOT NULL
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict
            });

            // === Cấu hình cho Entity: SaleDetail ===
            modelBuilder.Entity<SaleDetail>(entity =>
            {
                entity.HasKey(e => e.SaleDetailID);
                entity.Property(e => e.Quantity).IsRequired().HasColumnType("decimal(10, 2)");
                entity.Property(e => e.ProductPriceAtPurchase).IsRequired().HasColumnType("decimal(10, 2)");

                entity.HasCheckConstraint("CK_SaleDetails_Quantity", "[Quantity] > 0");
                entity.HasCheckConstraint("CK_SaleDetails_ProductPriceAtPurchase", "[ProductPriceAtPurchase] >= 0");

                entity.HasOne(d => d.Sale)
                    .WithMany(p => p.SaleDetails)
                    .HasForeignKey(d => d.SaleID)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade); // Nếu Sale bị xóa, SaleDetails cũng xóa

                entity.HasOne(d => d.ProductType)
                    .WithMany(p => p.SaleDetails) // Giả sử ProductType có ICollection<SaleDetail> SaleDetails
                    .HasForeignKey(d => d.ProductTypeID)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade); // Hoặc Restrict nếu không muốn xóa SP khi còn trong đơn hàng
            });

            // === Cấu hình cho Entity: Tag ===
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasKey(e => e.TagID);
                entity.Property(e => e.TagName).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.TagName).IsUnique();
            });

            // === Cấu hình cho Entity: ProductTag (Bảng trung gian Many-to-Many) ===
            modelBuilder.Entity<ProductTag>(entity =>
            {
                entity.HasKey(pt => new { pt.ProductTypeID, pt.TagID }); // Khóa chính hỗn hợp

                entity.HasOne(pt => pt.ProductType)
                    .WithMany(p => p.ProductTags) // Giả sử ProductType có ICollection<ProductTag> ProductTags
                    .HasForeignKey(pt => pt.ProductTypeID)
                    .OnDelete(DeleteBehavior.Cascade); // Khi xóa ProductType, xóa ProductTags liên quan

                entity.HasOne(pt => pt.Tag)
                    .WithMany(t => t.ProductTags) // Giả sử Tag có ICollection<ProductTag> ProductTags
                    .HasForeignKey(pt => pt.TagID)
                    .OnDelete(DeleteBehavior.Cascade); // Khi xóa Tag, xóa ProductTags liên quan
            });

            // === Cấu hình cho Entity: OtpType ===
            modelBuilder.Entity<OtpType>(entity => {
                entity.HasKey(e => e.OtpTypeID);
                entity.Property(e => e.OtpTypeName).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.OtpTypeName).IsUnique();
            });

            // === Cấu hình cho Entity: OtpRequest ===
            modelBuilder.Entity<OtpRequest>(entity => {
                entity.HasKey(e => e.OtpRequestID);
                entity.Property(e => e.OtpCode).IsRequired().HasMaxLength(6);
                entity.Property(e => e.RequestTime).HasDefaultValueSql("GETDATE()");
                entity.HasCheckConstraint("CK_OtpRequests_OneActorOnly", "(CustomerID IS NOT NULL AND EmployeeAccountID IS NULL) OR (CustomerID IS NULL AND EmployeeAccountID IS NOT NULL)");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.OtpRequests)
                    .HasForeignKey(d => d.CustomerID)
                    .IsRequired(false) // CustomerID có thể NULL
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.EmployeeAccount)
                    .WithMany(p => p.OtpRequests)
                    .HasForeignKey(d => d.EmployeeAccountID)
                    .IsRequired(false) // EmployeeAccountID có thể NULL
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.OtpType)
                    .WithMany(p => p.OtpRequests) // Giả sử OtpType có ICollection<OtpRequest> OtpRequests
                    .HasForeignKey(d => d.OtpTypeID)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // === Cấu hình cho Entity: ViewHistory ===
            modelBuilder.Entity<ViewHistory>(entity =>
            {
                entity.HasKey(e => e.ViewHistoryID);
                entity.Property(e => e.ViewTimestamp).HasDefaultValueSql("GETDATE()");
                entity.HasIndex(e => new { e.CustomerID, e.ProductTypeID }); // Index thường dùng
                entity.HasIndex(e => new { e.SessionID, e.ProductTypeID });
                entity.HasIndex(e => new { e.ProductTypeID, e.ViewTimestamp });


                entity.HasOne(vh => vh.Customer)
                    .WithMany(c => c.ViewHistories)
                    .HasForeignKey(vh => vh.CustomerID)
                    .IsRequired(false) // CustomerID có thể NULL
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(vh => vh.ProductType)
                    .WithMany() // Nếu ProductType không có ICollection<ViewHistory> thì để trống WithMany()
                    .HasForeignKey(vh => vh.ProductTypeID)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // === Cấu hình cho Entity: SearchHistory ===
            modelBuilder.Entity<SearchHistory>(entity =>
            {
                entity.HasKey(e => e.SearchHistoryID);
                entity.Property(e => e.SearchKeyword).IsRequired().HasMaxLength(512);
                entity.Property(e => e.SearchTimestamp).HasDefaultValueSql("GETDATE()");
                entity.HasIndex(e => e.CustomerID);
                entity.HasIndex(e => e.SessionID);
                entity.HasIndex(e => e.SearchKeyword);


                entity.HasOne(sh => sh.Customer)
                    .WithMany(c => c.SearchHistories)
                    .HasForeignKey(sh => sh.CustomerID)
                    .IsRequired(false) // CustomerID có thể NULL
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}