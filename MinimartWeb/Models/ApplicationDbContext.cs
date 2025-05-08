using Microsoft.EntityFrameworkCore;
using MinimartWeb.Model;
using MinimartWeb.Models;

namespace MinimartWeb.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<MeasurementUnit> MeasurementUnits { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ProductTag> ProductTags { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<EmployeeRole> EmployeeRoles { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<EmployeeAccount> EmployeeAccounts { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleDetail> SaleDetails { get; set; }
        public DbSet<OtpType> OtpTypes { get; set; }
        public DbSet<OtpRequest> OtpRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Categories
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategoryName)
                .IsUnique();

            // Suppliers
            modelBuilder.Entity<Supplier>()
                .HasIndex(s => s.SupplierPhoneNumber)
                .IsUnique();
            modelBuilder.Entity<Supplier>()
                .HasIndex(s => s.SupplierEmail)
                .IsUnique();

            // MeasurementUnits
            modelBuilder.Entity<MeasurementUnit>()
                .HasIndex(m => m.UnitName)
                .IsUnique();

            // ProductTypes
            modelBuilder.Entity<ProductType>()
                .HasIndex(p => p.ProductName)
                .IsUnique();

            modelBuilder.Entity<ProductType>()
                .HasCheckConstraint("CK_ProductTypes_Price", "[Price] >= 0")
                .HasCheckConstraint("CK_ProductTypes_StockAmount", "[StockAmount] >= 0")
                .HasCheckConstraint("CK_ProductTypes_ExpirationDurationDays", "[ExpirationDurationDays] >= 0");

            // Customers
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Email)
                .IsUnique();
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.PhoneNumber)
                .IsUnique();
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Username)
                .IsUnique();

            // EmployeeRoles
            modelBuilder.Entity<EmployeeRole>()
                .HasIndex(e => e.RoleName)
                .IsUnique();

            // Employees
            modelBuilder.Entity<Employee>()
                .HasCheckConstraint("CK_Employees_Gender", "[Gender] IN ('Male', 'Female', 'Non-Binary', 'Prefer not to say')")
                .HasCheckConstraint("CK_Employees_Salary", "[Salary] >= 0");

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.CitizenID)
                .IsUnique();
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.Email)
                .IsUnique();
            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.PhoneNumber)
                .IsUnique();

            // Admins
            modelBuilder.Entity<EmployeeAccount>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // PaymentMethods
            modelBuilder.Entity<PaymentMethod>()
                .HasIndex(p => p.MethodName)
                .IsUnique();

            // Sales
            modelBuilder.Entity<Sale>()
                .HasCheckConstraint("CK_Sales_OrderStatus", "[OrderStatus] IN ('Pending', 'Confirmed', 'Processing', 'Completed', 'Cancelled')");

            // SaleDetails
            modelBuilder.Entity<SaleDetail>()
                .HasCheckConstraint("CK_SaleDetails_Quantity", "[Quantity] > 0")
                .HasCheckConstraint("CK_SaleDetails_ProductPriceAtPurchase", "[ProductPriceAtPurchase] >= 0");

            // OtpTypes
            modelBuilder.Entity<OtpType>()
                .HasIndex(ot => ot.OtpTypeName)
                .IsUnique();

            // Relationships

            // Sale - Customer relationship
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerID)
                .OnDelete(DeleteBehavior.Cascade);

            // Sale - Employee relationship
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.Sales)
                .HasForeignKey(s => s.EmployeeID)
                .OnDelete(DeleteBehavior.Cascade);

            // Sale - PaymentMethod relationship
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.PaymentMethod)
                .WithMany(p => p.Sales)
                .HasForeignKey(s => s.PaymentMethodID)
                .OnDelete(DeleteBehavior.Cascade);

            // SaleDetail - Sale relationship
            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.Sale)
                .WithMany(s => s.SaleDetails)
                .HasForeignKey(sd => sd.SaleID)
                .OnDelete(DeleteBehavior.Cascade);

            // SaleDetail - ProductType relationship
            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.ProductType)
                .WithMany(p => p.SaleDetails)
                .HasForeignKey(sd => sd.ProductTypeID)
                .OnDelete(DeleteBehavior.Cascade);

            // ProductTags - ProductType relationship
            modelBuilder.Entity<ProductTag>()
                .HasOne(pt => pt.ProductType)
                .WithMany(p => p.ProductTags)
                .HasForeignKey(pt => pt.ProductTypeID)
                .OnDelete(DeleteBehavior.Cascade);

            // ProductTags - Tag relationship
            modelBuilder.Entity<ProductTag>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.ProductTags)
                .HasForeignKey(pt => pt.TagID)
                .OnDelete(DeleteBehavior.Cascade);

            // OtpTypes
            modelBuilder.Entity<OtpType>()
                .HasIndex(ot => ot.OtpTypeName)
                .IsUnique();

            // OtpRequests
            modelBuilder.Entity<OtpRequest>()
                .HasCheckConstraint("CK_OtpRequests_OneActorOnly",
                    "(CustomerID IS NOT NULL AND EmployeeAccountID IS NULL) OR " +
                    "(CustomerID IS NULL AND EmployeeAccountID IS NOT NULL)");

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
