using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using System.Security.Claims;

namespace MinimartWeb.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");

            var username = User.Identity.Name;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null) return NotFound();

            var notifications = await _context.Notifications
                .Where(n => n.CustomerID == customer.CustomerID)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }
        [HttpGet]
        public async Task<IActionResult> GetLatestNotifications()
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            var username = User.Identity.Name;

            // Check if the user is a Customer
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
            var employeeAccount = await _context.EmployeeAccounts.FirstOrDefaultAsync(ea => ea.Username == username);

            // Determine user type
            if (customer != null)
            {
                // 🧑‍💼 Customer Notifications
                var notifications = await _context.Notifications
                    .Where(n => n.CustomerID == customer.CustomerID)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .Select(n => new
                    {
                        n.NotificationID,
                        n.Title,
                        n.Message,
                        n.IsRead,
                        NotificationType = n.NotificationType.ToString().Replace('_', ' '),
                        n.SaleID
                    })
                    .ToListAsync();

                var unreadCount = await _context.Notifications
                    .CountAsync(n => n.CustomerID == customer.CustomerID && !n.IsRead);

                return Json(new { notifications, unreadCount });
            }
            else if (employeeAccount != null)
            {
                // 👨‍💼 Admin or Staff Notifications
                var notifications = await _context.Notifications
                    .Where(n => n.EmployeeAccountID == employeeAccount.AccountID)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .Select(n => new
                    {
                        n.NotificationID,
                        n.Title,
                        n.Message,
                        n.IsRead,
                        NotificationType = n.NotificationType.ToString().Replace('_', ' ')
                    })
                    .ToListAsync();

                var unreadCount = await _context.Notifications
                    .CountAsync(n => n.EmployeeAccountID == employeeAccount.AccountID && !n.IsRead);

                return Json(new { notifications, unreadCount });
            }

            // ❌ If neither, return 404
            return NotFound();
        }


        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { success = false, message = "Unauthorized access." });
            }

            var username = User.Identity.Name;
            var notification = await _context.Notifications.FindAsync(notificationId);

            if (notification == null)
            {
                return NotFound(new { success = false, message = "Notification not found." });
            }

            // Check if the user is the owner of the notification
            if (User.IsInRole("Customer"))
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
                if (notification.CustomerID != customer?.CustomerID)
                {
                    // ❌ FIXED: Forbid() does not accept an object, only a string.
                    return Forbid("You are not authorized to mark this notification.");
                }
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                var employeeAccount = await _context.EmployeeAccounts.FirstOrDefaultAsync(ea => ea.Username == username);
                if (notification.EmployeeAccountID != employeeAccount?.AccountID)
                {
                    // ❌ FIXED: Forbid() does not accept an object, only a string.
                    return Forbid("You are not authorized to mark this notification.");
                }
            }
            else
            {
                return Unauthorized(new { success = false, message = "Unauthorized access." });
            }

            // Mark as read if not already
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Notification marked as read." });
            }
            else
            {
                return Ok(new { success = true, message = "Notification was already read." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { unreadCount = 0 });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            int unreadCount = 0;

            if (User.IsInRole("Customer"))
            {
                // 🔎 Get unread notifications for Customer
                unreadCount = await _context.Notifications
                    .Where(n => n.CustomerID == userId && !n.IsRead)
                    .CountAsync();
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                // 🔎 Get unread notifications for EmployeeAccount
                var employeeAccount = await _context.EmployeeAccounts
                    .FirstOrDefaultAsync(ea => ea.EmployeeID == userId);

                if (employeeAccount != null)
                {
                    unreadCount = await _context.Notifications
                        .Where(n => n.EmployeeAccountID == employeeAccount.AccountID && !n.IsRead)
                        .CountAsync();
                }
            }

            return Json(new { unreadCount = unreadCount });
        }


    }
}
