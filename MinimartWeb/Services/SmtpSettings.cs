// Trong file: Services/SmtpSettings.cs (hoặc một namespace phù hợp)
namespace MinimartWeb.Services // Hoặc namespace của bạn
{
    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 587; // Cổng phổ biến cho SMTP (TLS)
        public string FromAddress { get; set; } = string.Empty; // Email sẽ hiển thị là người gửi
        public string FromName { get; set; } = "Minimart"; // Tên sẽ hiển thị là người gửi
        public string Username { get; set; } = string.Empty; // Tên đăng nhập tài khoản email dùng để gửi
        public string Password { get; set; } = string.Empty; // Mật khẩu (hoặc mật khẩu ứng dụng)
        public bool EnableSsl { get; set; } = true; // Bật SSL/TLS
    }
}