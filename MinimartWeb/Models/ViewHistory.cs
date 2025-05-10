// Trong thư mục Model (hoặc nơi bạn đặt các Entity)
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model // Đảm bảo namespace đúng
{
    public class ViewHistory
    {
        [Key]
        public long ViewHistoryID { get; set; }

        public int? CustomerID { get; set; } // Nullable
        [ForeignKey("CustomerID")]
        public virtual Customer? Customer { get; set; } // Navigation property, nullable

        [MaxLength(255)]
        public string? SessionID { get; set; } // Nullable

        public int ProductTypeID { get; set; }
        [ForeignKey("ProductTypeID")]
        public virtual ProductType ProductType { get; set; } = null!; // Non-nullable navigation

        public DateTime ViewTimestamp { get; set; } = DateTime.Now;

        public int? ViewDurationSeconds { get; set; } // Optional
    }
}