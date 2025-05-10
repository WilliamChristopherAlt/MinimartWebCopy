// Trong thư mục Model
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimartWeb.Model
{
    [Table("SearchHistories")]
    public class SearchHistory
    {
        [Key]
        public long SearchHistoryID { get; set; }
        public int? CustomerID { get; set; }

        public virtual Customer? Customer { get; set; }

        [MaxLength(255)]
        public string? SessionID { get; set; }

        [Required]
        [MaxLength(512)]
        public string SearchKeyword { get; set; } = string.Empty;

        public DateTime SearchTimestamp { get; set; } = DateTime.Now;

        public int? NumberOfResults { get; set; }
    }
}