using Microsoft.EntityFrameworkCore;
using MinimartWeb.Model; // Đảm bảo namespace của các lớp Model là đúng

namespace MinimartWeb.Data
{
    public class ApplicationDbContext : DbContext // Hoặc IdentityDbContext nếu bạn dùng ASP.NET Core Identity
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // --- DbSets cho tất cả các bảng ---
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
        public DbSet<ViewHistory> ViewHistories { get; set; } = null!; // EF Core sẽ tìm bảng "ViewHistories"
        public DbSet<SearchHistory> SearchHistories { get; set; } = null!; // EF Core sẽ tìm bảng "SearchHistories"

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Cần thiết nếu kế thừa từ IdentityDbContext

            // --- Cấu hình tên bảng thực tế trong Database (NẾU KHÁC QUY ƯỚC SỐ NHIỀU) ---
            // Nếu tên bảng trong SQL của bạn là số ít (ví dụ: "ViewHistory"), hãy bỏ comment dòng dưới
            // và sửa tên cho đúng. Nếu tên bảng trong SQL đã là số nhiều thì không cần.
            // modelBuilder.Entity<ViewHistory>().ToTable("ViewHistory");
            // modelBuilder.Entity<SearchHistory>().ToTable("SearchHistory");
            // Tương tự cho các bảng khác nếu tên class C# và tên bảng SQL không theo quy ước số nhiều.

            // --- Cấu hình kiểu dữ liệu DECIMAL ---
            modelBuilder.Entity<ProductType>(entity => {
                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
                entity.Property(e => e.StockAmount).HasColumnType("decimal(18, 2)");
                // Kiểm tra xem thuộc tính OriginalPrice có tồn tại trong Model không trước khi cấu hình
                //if (entity.Metadata.FindProperty(nameof(ProductType.OriginalPrice)) != null)
                //{
                  //  entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18, 2)");
                //}
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

            // --- Cấu hình UNIQUE Constraints ---
            modelBuilder.Entity<Category>().HasIndex(c => c.CategoryName).IsUnique();
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
            modelBuilder.Entity<ProductTag>().HasKey(pt => new { pt.ProductTypeID, pt.TagID }); // Khóa chính hỗn hợp cho bảng nối
            modelBuilder.Entity<OtpType>().HasIndex(ot => ot.OtpTypeName).IsUnique();

            // --- Cấu hình CHECK Constraints (EF Core 6+ hỗ trợ tốt hơn) ---
            // EF Core sẽ đọc các CHECK constraints từ database nếu chúng đã được tạo bằng SQL.
            // Việc thêm ở đây giúp EF Core "biết" về chúng và có thể validate phía client (tùy thư viện validation).
            modelBuilder.Entity<ProductType>()
                .HasCheckConstraint("CK_ProductTypes_Price", "[Price] >= 0")
                .HasCheckConstraint("CK_ProductTypes_StockAmount", "[StockAmount] >= 0");
            if (modelBuilder.Model.FindEntityType(typeof(ProductType))?.FindProperty(nameof(ProductType.ExpirationDurationDays)) != null)
            {
                modelBuilder.Entity<ProductType>().HasCheckConstraint("CK_ProductTypes_ExpirationDurationDays", "[ExpirationDurationDays] >= 0 OR [ExpirationDurationDays] IS NULL");
            }
           // if (modelBuilder.Model.FindEntityType(typeof(ProductType))?.FindProperty(nameof(ProductType.OriginalPrice)) != null)
           // {
            //    modelBuilder.Entity<ProductType>().HasCheckConstraint("CK_ProductTypes_OriginalPrice", "[OriginalPrice] >= 0 OR [OriginalPrice] IS NULL");
           // }

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
            // (Giữ lại các cấu hình mối quan hệ bạn đã có, chúng rất chi tiết và tốt)

            // ViewHistory Relationships
            modelBuilder.Entity<ViewHistory>()
                .HasOne(vh => vh.Customer)
                .WithMany() // Nếu Customer không có ICollection<ViewHistory>
                .HasForeignKey(vh => vh.CustomerID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ViewHistory>()
                .HasOne(vh => vh.ProductType)
                .WithMany() // Nếu ProductType không có ICollection<ViewHistory>
                .HasForeignKey(vh => vh.ProductTypeID)
                .OnDelete(DeleteBehavior.Cascade);

            // SearchHistory Relationships
            modelBuilder.Entity<SearchHistory>()
                .HasOne(sh => sh.Customer)
                .WithMany() // Nếu Customer không có ICollection<SearchHistory>
                .HasForeignKey(sh => sh.CustomerID)
                .OnDelete(DeleteBehavior.SetNull);

            // ProductType Relationships
            modelBuilder.Entity<ProductType>()
                .HasOne(pt => pt.Category)
                .WithMany(c => c.ProductTypes)
                .HasForeignKey(pt => pt.CategoryID)
                .OnDelete(DeleteBehavior.Restrict); // Đổi thành Restrict để không xóa Category nếu có ProductType

            modelBuilder.Entity<ProductType>()
                .HasOne(pt => pt.Supplier)
                .WithMany(s => s.ProductTypes)
                .HasForeignKey(pt => pt.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductType>()
                .HasOne(pt => pt.MeasurementUnit)
                .WithMany(mu => mu.ProductTypes)
                .HasForeignKey(pt => pt.MeasurementUnitID)
                .OnDelete(DeleteBehavior.Restrict);

            // Employee Relationships
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Role)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeeAccount>()
                .HasOne(ea => ea.Employee)
                .WithOne()
                .HasForeignKey<EmployeeAccount>(ea => ea.EmployeeID)
                .OnDelete(DeleteBehavior.Cascade);

            // Sale Relationships
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerID)
                .OnDelete(DeleteBehavior.SetNull); // Giữ lại Sales nếu Customer bị xóa (gán CustomerID = NULL)

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.Sales)
                .HasForeignKey(s => s.EmployeeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.PaymentMethod)
                .WithMany(pm => pm.Sales)
                .HasForeignKey(s => s.PaymentMethodID)
                .OnDelete(DeleteBehavior.Restrict);

            // SaleDetail Relationships
            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.Sale)
                .WithMany(s => s.SaleDetails)
                .HasForeignKey(sd => sd.SaleID)
                .OnDelete(DeleteBehavior.Cascade); // Xóa SaleDetails nếu Sale bị xóa

            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.ProductType)
                .WithMany(pt => pt.SaleDetails)
                .HasForeignKey(sd => sd.ProductTypeID)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa ProductType nếu đã có trong SaleDetail

            // ProductTag (Many-to-Many through join entity)
            // Khóa chính hỗn hợp đã được định nghĩa bằng [Key] trong ProductTag nếu ProductTagID là PK
            // Nếu ProductTagID không phải PK mà (ProductTypeID, TagID) là PK thì cấu hình:
            // modelBuilder.Entity<ProductTag>()
            //     .HasKey(pt => new { pt.ProductTypeID, pt.TagID });

            modelBuilder.Entity<ProductTag>()
                .HasOne(pt => pt.ProductType)
                .WithMany(p => p.ProductTags)
                .HasForeignKey(pt => pt.ProductTypeID);
            // .OnDelete(DeleteBehavior.Cascade) // Mặc định thường là Cascade

            modelBuilder.Entity<ProductTag>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.ProductTags)
                .HasForeignKey(pt => pt.TagID);
            // .OnDelete(DeleteBehavior.Cascade)

            // OtpRequest Relationships
            modelBuilder.Entity<OtpRequest>()
                .HasOne(or => or.Customer)
                .WithMany(c => c.OtpRequests)
                .HasForeignKey(or => or.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OtpRequest>()
                .HasOne(or => or.EmployeeAccount)
                .WithMany(ea => ea.OtpRequests)
                .HasForeignKey(or => or.EmployeeAccountID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OtpRequest>()
                .HasOne(or => or.OtpType)
                .WithMany(ot => ot.OtpRequests)
                .HasForeignKey(or => or.OtpTypeID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}