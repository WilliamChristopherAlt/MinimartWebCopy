using System.ComponentModel.DataAnnotations;

namespace MinimartWeb.Models // HOẶC MinimartWeb.Models
{
    // Optional: Custom validation attribute to ensure NewEmail is not equal to CurrentEmail
    public class NotEqualToAttribute : ValidationAttribute
    {
        private readonly string _comparisonProperty;

        public NotEqualToAttribute(string comparisonProperty)
        {
            _comparisonProperty = comparisonProperty;
        }

        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            ErrorMessage = ErrorMessageString;
            var currentValue = value as string;

            var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
            if (property == null)
            {
                return new ValidationResult($"Thuộc tính không xác định: {_comparisonProperty}");
            }

            var comparisonValue = property.GetValue(validationContext.ObjectInstance) as string;

            if (string.Equals(currentValue, comparisonValue, System.StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} phải khác với {_comparisonProperty}.");
            }

            return ValidationResult.Success!;
        }
    }

    public class RequestChangeEmailViewModel
    {
        [Display(Name = "Email hiện tại")]
        public string CurrentEmail { get; set; } = string.Empty; // Sẽ được điền từ controller

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ email mới.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email mới không hợp lệ.")]
        [Display(Name = "Email mới")]
        [NotEqualTo("CurrentEmail", ErrorMessage = "Email mới phải khác email hiện tại.")]
        public string NewEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại để xác nhận.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;
    }
}