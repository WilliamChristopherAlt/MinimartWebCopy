using Microsoft.EntityFrameworkCore;
using MinimartWeb.Model; // Đảm bảo namespace của các lớp Model là đúng

namespace MinimartWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // --- DbSets cho tất cả các bảng ---
        // Giữ nguyên tên DbSet là số nhiều để khớp với quy ước và tên bảng DB
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
        public DbSet<ViewHistory> ViewHistories { get; set; } = null!; // DbSet tên số nhiều
        public DbSet<SearchHistory> SearchHistories { get; set; } = null!; // DbSet tên số nhiều

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Cấu hình tên bảng thực tế trong Database ---
            // Nếu class C# của bạn là ViewHistory (số ít) nhưng bảng DB là ViewHistories (số nhiều),
            // và tên DbSet của bạn là ViewHistories (số nhiều), EF Core sẽ tự động ánh xạ đúng.
            // Bạn chỉ cần ToTable() nếu tên class C# khác với tên bảng DB VÀ tên DbSet cũng không giúp suy luận đúng.
            // Trong trường hợp này, với DbSet<ViewHistory> ViewHistories, EF Core sẽ tìm bảng "ViewHistories".
            // Nếu class C# của bạn là "ViewHistoryEntity" chẳng hạn, thì bạn mới cần:
            // modelBuilder.Entity<ViewHistoryEntity>().ToTable("ViewHistories");

            // Vì class model của bạn là ViewHistory (số ít) và DbSet là ViewHistories (số nhiều),
            // EF Core sẽ tự động ánh xạ class ViewHistory tới bảng ViewHistories.
            // Không cần ToTable() rõ ràng ở đây nếu bạn đã đặt tên DbSet là số nhiều.

            // --- Cấu hình kiểu dữ liệu DECIMAL (GIỮ NGUYÊN NHƯ CỦA BẠN) ---
            modelBuilder.Entity<ProductType>(entity => {
                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
                entity.Property(e => e.StockAmount).HasColumnType("decimal(18, 2)");
                // Bỏ OriginalPrice nếu không dùng
            });
            modelBuilder.Entity<SaleDetail>(entity => {
                entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
                entity.Property(e => e.ProductPriceAtPurchase).HasColumnType("decimal(18, 2)");
            });
            modelBuilder.Entity<Employee>(entity => {
                if (entity.Metadata.FindProperty(nameof(Employee.Salary)) != null)
                {
                    entity.Property(e => e.Salary).HasColumnType("decimal(18, 2)");
                }
            });

            // --- Cấu hình UNIQUE Constraints (GIỮ NGUYÊN NHƯ CỦA BẠN) ---
            modelBuilder.Entity<Category>().HasIndex(c => c.CategoryName).IsUnique();
            // ... (Các HasIndex().IsUnique() khác giữ nguyên) ...
            modelBuilder.Entity<Supplier>().HasIndex(s => s.SupplierName).IsUnique();
            modelBuilder.Entity<Supplier>().HasIndex(s => s.SupplierPhoneNumber).IsUnique();
            modelBuilder.Entity<Supplier>().HasIndex(s => s.SupplierEmail).IsUnique();
            modelBuilder.Entity<MeasurementUnit>().HasIndex(m => m.UnitName).IsUnique();
            modelBuilder.Entity<ProductType>().HasIndex(p => p.ProductName).IsUnique();
            modelBuilder.Entity<Customer>().HasIndex(c => c.Email).IsUnique();
            modelBuilder.Entity<Customer>().HasIndex(c => c.PhoneNumber).IsUnique();
            modelBuilder.Entity<Customer>().HasIndex(c => c.Username).IsUnique();
            modelBuilder.Entity<EmployeeRole>().HasIndex(e => e.RoleName).IsUnique();
            modelBuilder.Entity<Employee>().HasIndex(e => e.CitizenID).IsUnique();
            modelBuilder.Entity<Employee>().HasIndex(e => e.Email).IsUnique();
            modelBuilder.Entity<Employee>().HasIndex(e => e.PhoneNumber).IsUnique();
            modelBuilder.Entity<EmployeeAccount>().HasIndex(a => a.Username).IsUnique();
            modelBuilder.Entity<EmployeeAccount>().HasIndex(a => a.EmployeeID).IsUnique();
            modelBuilder.Entity<PaymentMethod>().HasIndex(p => p.MethodName).IsUnique();
            modelBuilder.Entity<Tag>().HasIndex(t => t.TagName).IsUnique();
            modelBuilder.Entity<OtpType>().HasIndex(ot => ot.OtpTypeName).IsUnique();
            // Khóa chính hỗn hợp cho ProductTag đã được bạn cấu hình
            modelBuilder.Entity<ProductTag>().HasKey(pt => new { pt.ProductTypeID, pt.TagID });


            // --- Cấu hình CHECK Constraints (GIỮ NGUYÊN NHƯ CỦA BẠN) ---
            // ... (Toàn bộ HasCheckConstraint của bạn giữ nguyên) ...
            modelBuilder.Entity<ProductType>()
                .HasCheckConstraint("CK_ProductTypes_Price", "[Price] >= 0")
                .HasCheckConstraint("CK_ProductTypes_StockAmount", "[StockAmount] >= 0");
            if (modelBuilder.Model.FindEntityType(typeof(ProductType))?.FindProperty(nameof(ProductType.ExpirationDurationDays)) != null)
            {
                modelBuilder.Entity<ProductType>().HasCheckConstraint("CK_ProductTypes_ExpirationDurationDays", "[ExpirationDurationDays] >= 0 OR [ExpirationDurationDays] IS NULL");
            }
            modelBuilder.Entity<Employee>()
                .HasCheckConstraint("CK_Employees_Gender", "[Gender] IN ('Male', 'Female', 'Non-Binary', 'Prefer not to say')");
            if (modelBuilder.Model.FindEntityType(typeof(Employee))?.FindProperty(nameof(Employee.Salary)) != null)
            {
                modelBuilder.Entity<Employee>().HasCheckConstraint("CK_Employees_Salary", "[Salary] >= 0 OR [Salary] IS NULL");
            }
            modelBuilder.Entity<Sale>()
                .HasCheckConstraint("CK_Sales_OrderStatus", "[OrderStatus] IN ('Pending', 'Confirmed', 'Processing', 'Completed', 'Cancelled')");
            modelBuilder.Entity<SaleDetail>()
                .HasCheckConstraint("CK_SaleDetails_Quantity", "[Quantity] > 0")
                .HasCheckConstraint("CK_SaleDetails_ProductPriceAtPurchase", "[ProductPriceAtPurchase] >= 0");
            modelBuilder.Entity<OtpRequest>()
               .HasCheckConstraint("CK_OtpRequests_OneActorOnly", "(CustomerID IS NOT NULL AND EmployeeAccountID IS NULL) OR (CustomerID IS NULL AND EmployeeAccountID IS NOT NULL)");


            // --- Cấu hình Mối quan hệ (Relationships) ---

            // Quan trọng: Cấu hình cho ViewHistory
            modelBuilder.Entity<ViewHistory>(entity =>
            {
                entity.HasOne(vh => vh.Customer)          // Navigation property trong ViewHistory (class C# số ít)
                    .WithMany(c => c.ViewHistories)       // Navigation property collection trong Customer (ICollection<ViewHistory> ViewHistories)
                    .HasForeignKey(vh => vh.CustomerID)   // Chỉ rõ cột khóa ngoại là 'CustomerID' trong class ViewHistory
                    .HasPrincipalKey(c => c.CustomerID)   // Chỉ rõ khóa chính của Customer là 'CustomerID'
                    .IsRequired(false)                    // Vì CustomerID trong ViewHistory là nullable
                    .OnDelete(DeleteBehavior.SetNull);    // Hành vi xóa

                entity.HasOne(vh => vh.ProductType)
                    .WithMany() // Giả sử ProductType có ICollection<ViewHistory> ViewHistories
                    .HasForeignKey(vh => vh.ProductTypeID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Quan trọng: Cấu hình cho SearchHistory
            modelBuilder.Entity<SearchHistory>(entity =>
            {
                entity.HasOne(sh => sh.Customer)
                    .WithMany(c => c.SearchHistories)    // Navigation property collection trong Customer (ICollection<SearchHistory> SearchHistories)
                    .HasForeignKey(sh => sh.CustomerID)  // Chỉ rõ cột khóa ngoại là 'CustomerID' trong class SearchHistory
                    .HasPrincipalKey(c => c.CustomerID)  // Chỉ rõ khóa chính của Customer là 'CustomerID'
                    .IsRequired(false)                   // Vì CustomerID là nullable
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ProductType Relationships (GIỮ NGUYÊN NHƯ CỦA BẠN)
            modelBuilder.Entity<ProductType>(pt => {
                pt.HasOne(p => p.Category)
                    .WithMany(c => c.ProductTypes)
                    .HasForeignKey(p => p.CategoryID)
                    .OnDelete(DeleteBehavior.Restrict);
                pt.HasOne(p => p.Supplier)
                    .WithMany(s => s.ProductTypes)
                    .HasForeignKey(p => p.SupplierID)
                    .OnDelete(DeleteBehavior.Restrict);
                pt.HasOne(p => p.MeasurementUnit)
                    .WithMany(m => m.ProductTypes)
                    .HasForeignKey(p => p.MeasurementUnitID)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // Employee Relationships (GIỮ NGUYÊN NHƯ CỦA BẠN)
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Role)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeeAccount>()
                .HasOne(ea => ea.Employee)
                .WithOne() // Giả sử Employee có navigation property EmployeeAccount
                .HasForeignKey<EmployeeAccount>(ea => ea.EmployeeID)
                .OnDelete(DeleteBehavior.Cascade);

            // Sale Relationships (GIỮ NGUYÊN NHƯ CỦA BẠN, BAO GỒM SET NULL CHO CUSTOMER)
            modelBuilder.Entity<Sale>(s => {
                s.HasOne(sl => sl.Customer)
                    .WithMany(c => c.Sales)
                    .HasForeignKey(sl => sl.CustomerID)
                    .OnDelete(DeleteBehavior.SetNull);
                s.HasOne(sl => sl.Employee)
                    .WithMany(e => e.Sales)
                    .HasForeignKey(sl => sl.EmployeeID)
                    .OnDelete(DeleteBehavior.Restrict);
                s.HasOne(sl => sl.PaymentMethod)
                    .WithMany(pm => pm.Sales)
                    .HasForeignKey(sl => sl.PaymentMethodID)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // SaleDetail Relationships (GIỮ NGUYÊN NHƯ CỦA BẠN)
            modelBuilder.Entity<SaleDetail>(sd => {
                sd.HasOne(d => d.Sale)
                    .WithMany(s => s.SaleDetails)
                    .HasForeignKey(d => d.SaleID)
                    .OnDelete(DeleteBehavior.Cascade);
                sd.HasOne(d => d.ProductType)
                    .WithMany(pt => pt.SaleDetails) // Giả sử ProductType có ICollection<SaleDetail> SaleDetails
                    .HasForeignKey(d => d.ProductTypeID)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // ProductTag (GIỮ NGUYÊN NHƯ CỦA BẠN)
            modelBuilder.Entity<ProductTag>(pt => {
                // Khóa chính hỗn hợp đã được cấu hình ở trên
                pt.HasOne(p => p.ProductType)
                    .WithMany(p => p.ProductTags)
                    .HasForeignKey(p => p.ProductTypeID);
                pt.HasOne(p => p.Tag)
                    .WithMany(t => t.ProductTags)
                    .HasForeignKey(p => p.TagID);
            });


            // OtpRequest Relationships (GIỮ NGUYÊN NHƯ CỦA BẠN)
            modelBuilder.Entity<OtpRequest>(or => {
                or.HasOne(r => r.Customer)
                    .WithMany(c => c.OtpRequests)
                    .HasForeignKey(r => r.CustomerID)
                    .OnDelete(DeleteBehavior.Cascade);
                or.HasOne(r => r.EmployeeAccount)
                    .WithMany(ea => ea.OtpRequests)
                    .HasForeignKey(r => r.EmployeeAccountID)
                    .OnDelete(DeleteBehavior.Cascade);
                or.HasOne(r => r.OtpType)
                    .WithMany(ot => ot.OtpRequests)
                    .HasForeignKey(r => r.OtpTypeID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}