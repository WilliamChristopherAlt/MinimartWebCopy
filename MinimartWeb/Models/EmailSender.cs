// Trong file: Services/EmailSender.cs
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging; // Thêm logger nếu muốn log chi tiết hơn

namespace MinimartWeb.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailSender> _logger; // (Tùy chọn) Thêm logger

        // Sửa constructor để nhận ILogger<EmailSender> nếu bạn muốn log từ đây
        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            // Đọc cấu hình SMTP từ appsettings.json và map vào đối tượng SmtpSettings
            _smtpSettings = configuration.GetSection("SmtpSettings").Get<SmtpSettings>();
            _logger = logger; // Gán logger

            if (_smtpSettings == null)
            {
                _logger.LogError("CRITICAL: Khối cấu hình SmtpSettings không tìm thấy trong appsettings.json hoặc không thể map.");
                // Bạn có thể throw exception ở đây nếu muốn ứng dụng dừng lại khi thiếu cấu hình quan trọng
                // throw new InvalidOperationException("SMTP settings are not configured in appsettings.json");
                // Hoặc gán một đối tượng SmtpSettings rỗng để tránh NullReferenceException sau đó,
                // và logic trong SendEmailAsync sẽ xử lý việc không gửi mail.
                _smtpSettings = new SmtpSettings(); // Để tránh NullReferenceException ở các kiểm tra sau
            }
            else if (string.IsNullOrEmpty(_smtpSettings.Server) ||
                     string.IsNullOrEmpty(_smtpSettings.Username) ||
                     string.IsNullOrEmpty(_smtpSettings.Password) ||
                     string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                _logger.LogWarning("Một số cấu hình SMTP quan trọng bị thiếu trong SmtpSettings. Email có thể không được gửi.");
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Kiểm tra lại lần nữa trước khi gửi, phòng trường hợp _smtpSettings được khởi tạo rỗng
            if (string.IsNullOrEmpty(_smtpSettings.Server) ||
                string.IsNullOrEmpty(_smtpSettings.Username) ||
                string.IsNullOrEmpty(_smtpSettings.Password) ||
                string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                _logger.LogError("Cấu hình SMTP không đầy đủ. Email KHÔNG được gửi tới {RecipientEmail} với chủ đề '{Subject}'. Vui lòng kiểm tra appsettings.json và class SmtpSettings.", email, subject);
                Console.WriteLine($"LỖI: Cấu hình SMTP không đầy đủ. Email KHÔNG được gửi tới {email}. Subject: {subject}");
                Console.WriteLine($"NỘI DUNG EMAIL (DEBUG): {htmlMessage}");
                return;
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress, _smtpSettings.FromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(email);

            using (var smtpClient = new SmtpClient(_smtpSettings.Server, _smtpSettings.Port))
            {
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;

                try
                {
                    await smtpClient.SendMailAsync(mailMessage);
                    _logger.LogInformation("Email đã được gửi thành công tới {RecipientEmail} với chủ đề '{Subject}'.", email, subject);
                    Console.WriteLine($"Email đã được gửi (hoặc cố gắng gửi) tới {email}. Subject: {subject}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gửi email tới {RecipientEmail} với chủ đề '{Subject}'.", email, subject);
                    Console.WriteLine($"LỖI khi gửi email tới {email}: {ex.Message}");
                    // Bạn có thể muốn throw lại ex ở đây hoặc một custom exception để controller biết và xử lý
                    // throw; // Hoặc throw new EmailSendingException("Failed to send email.", ex);
                }
            }
        }
    }
}