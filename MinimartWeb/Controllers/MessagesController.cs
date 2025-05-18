using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MinimartWeb.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Messages/CustomerMessages
        [HttpGet]
        public async Task<IActionResult> CustomerMessages()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || role != "Customer")
            {
                return Unauthorized(); // Proper 401
            }

            int customerId = int.Parse(userIdClaim);

            // 🔔 Step 1: Mark all 'New Message' notifications for this customer as read
            var unreadNotifications = await _context.Notifications
                .Where(n =>
                    n.CustomerID == customerId &&
                    n.NotificationType == NotificationType.NewMessage.GetDisplayName() &&
                    !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var noti in unreadNotifications)
                {
                    noti.IsRead = true;
                }

                await _context.SaveChangesAsync();
            }

            // 📨 Step 2: Load chat messages
            var messages = await _context.Messages
                .Where(m => m.CustomerID == customerId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserSend(string messageText)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || role != "Customer")
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(messageText))
                return RedirectToAction("CustomerMessages");

            int customerId = int.Parse(userIdClaim);
            var trimmedText = messageText.Trim();

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound();

            var message = new Message
            {
                CustomerID = customerId,
                IsFromCustomer = true,
                MessageText = System.Text.Encoding.UTF8.GetBytes(trimmedText),
                SentAt = DateTime.Now,
                IsRead = false,
                IsDeletedBySender = false,
                IsDeletedByReceiver = false
            };

            _context.Messages.Add(message);

            // 🔔 Create notifications for all employees
            var employees = await _context.EmployeeAccounts.ToListAsync();
            foreach (var emp in employees)
            {
                var notification = new Notification
                {
                    EmployeeAccountID = emp.AccountID,
                    Title = $"Tin nhắn mới từ {customer.Username}",
                    Message = trimmedText.Length > 100 ? trimmedText.Substring(0, 100) + "..." : trimmedText,
                    NotificationType = NotificationType.NewMessage.GetDisplayName(),
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    SaleID = null,
                    CustomerID = null,
                    MessageCustomerID = message.Customer.CustomerID
                };

                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("CustomerMessages");
        }

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> StaffMessages(int? customerId)
        {
            var employees = await _context.Customers
                .Select(c => new CustomerMessagePreview
                {
                    Customer = c,
                    LatestMessage = _context.Messages
                        .Where(m => m.CustomerID == c.CustomerID)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.MessageText)
                        .FirstOrDefault(),
                    LatestTime = _context.Messages
                        .Where(m => m.CustomerID == c.CustomerID)
                        .Max(m => (DateTime?)m.SentAt)
                })
                .OrderByDescending(c => c.LatestTime)
                .ToListAsync();

            List<Message> conversation = new();
            Customer? currentCustomer = null;

            if (customerId.HasValue)
            {
                currentCustomer = await _context.Customers.FindAsync(customerId);
                if (currentCustomer != null)
                {
                    // 🔔 Step 1: Mark notifications as read
                    var employeeIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(employeeIdClaim) && int.TryParse(employeeIdClaim, out int staffId))
                    {
                        var staffNotifs = await _context.Notifications
                            .Where(n =>
                                n.NotificationType == NotificationType.NewMessage.GetDisplayName() &&
                                !n.IsRead &&
                                n.MessageCustomer != null &&
                                n.MessageCustomer.CustomerID == customerId)
                            .ToListAsync();

                        foreach (var n in staffNotifs)
                            n.IsRead = true;

                        await _context.SaveChangesAsync();
                    }

                    // 📨 Step 2: Load messages
                    conversation = await _context.Messages
                        .Where(m => m.CustomerID == customerId)
                        .OrderBy(m => m.SentAt)
                        .ToListAsync();
                }
            }

            ViewBag.CurrentCustomer = currentCustomer;
            return View(Tuple.Create(employees, conversation));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> StaffSend(int customerId, string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return RedirectToAction("StaffMessages", new { customerId });

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound();

            var trimmedText = messageText.Trim();

            var message = new Message
            {
                CustomerID = customerId,
                IsFromCustomer = false, // Sent by staff
                MessageText = System.Text.Encoding.UTF8.GetBytes(trimmedText),
                SentAt = DateTime.Now,
                IsRead = false,
                IsDeletedBySender = false,
                IsDeletedByReceiver = false
            };

            _context.Messages.Add(message);

            // ✅ Create a new notification
            var notification = new Notification
            {
                CustomerID = customerId,
                Title = "Bạn có tin nhắn mới từ MiniMart",
                Message = trimmedText.Length > 100 ? trimmedText.Substring(0, 100) + "..." : trimmedText,
                NotificationType = NotificationType.NewMessage.GetDisplayName(),
                CreatedAt = DateTime.Now,
                IsRead = false,
                SaleID = null
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            return RedirectToAction("StaffMessages", new { customerId });
        }
    }
}
