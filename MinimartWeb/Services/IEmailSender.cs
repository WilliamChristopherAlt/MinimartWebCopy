// Trong file: Services/IEmailSender.cs
using System.Threading.Tasks;

namespace MinimartWeb.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
}