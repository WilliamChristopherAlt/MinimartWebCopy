using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // Quan trọng
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model;
using System.Security.Claims; // Cho ClaimsPrincipal
using MinimartWeb.Models; // Cho OrderHistoryViewModel, OrderViewModel, OrderItemViewModel, OrderDetailViewModel
using Microsoft.Extensions.Logging; // Cho ILogger

namespace MinimartWeb.Controllers
{
    // Bỏ Authorize ở cấp class để có thể đặt riêng cho từng action
    // [Authorize(Roles = "Admin")]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SalesController> _logger; // Thêm ILogger

        // Cập nhật constructor
        public SalesController(ApplicationDbContext context, ILogger<SalesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Index()
        {
            var sales = await _context.Sales
                .Include(s => s.SaleDetails)
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.PaymentMethod)
                .ToListAsync();

            var currentYear = DateTime.Now.Year;
            var grouped = sales
                .Where(s => s.SaleDate.Year == currentYear)
                .GroupBy(s => s.SaleDate.Month)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Month = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(s => s.SaleDetails.Sum(d => d.Quantity * d.ProductPriceAtPurchase))
                })
                .ToList();

            var chartData = grouped.Select(g => new { g.Month, g.Count, g.Revenue }).ToList();
            ViewBag.ChartJson = Newtonsoft.Json.JsonConvert.SerializeObject(chartData);
            ViewBag.TotalRevenue = chartData.Sum(g => g.Revenue).ToString("N0") + " đ";
            ViewBag.TotalOrders = chartData.Sum(g => g.Count);
            ViewBag.PeakMonth = "Tháng " + chartData.OrderByDescending(g => g.Revenue).First().Month;

            return View(sales);
        }


        // GET: Sales/Details/5 (ADMIN VIEW)
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            _logger.LogInformation("Admin/Staff accessing Sale Details for SaleID: {SaleID}", id);
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.PaymentMethod)
                 .Include(s => s.SaleDetails) // Thêm Include SaleDetails cho Admin view nếu cần
                    .ThenInclude(sd => sd.ProductType)
                .FirstOrDefaultAsync(m => m.SaleID == id);
            if (sale == null)
            {
                return NotFound();
            }

            return View(sale); // View này có thể là một view admin/details khác với customer/orderdetail
        }

        // GET: Sales/Create (ADMIN VIEW)
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public IActionResult Create()
        {
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "Username"); // Hiển thị Username cho dễ chọn
            ViewData["EmployeeID"] = new SelectList(_context.Employees.Select(e => new { e.EmployeeID, FullName = e.FirstName + " " + e.LastName }), "EmployeeID", "FullName");
            ViewData["PaymentMethodID"] = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName");
            return View();
        }

        // POST: Sales/Create (ADMIN VIEW)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public async Task<IActionResult> Create([Bind("SaleDate,CustomerID,EmployeeID,PaymentMethodID,DeliveryAddress,DeliveryTime,IsPickup,OrderStatus")] Sale sale)
        // Bỏ SaleID khỏi Bind khi Create vì nó là IDENTITY
        {
            if (ModelState.IsValid)
            {
                _context.Add(sale);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New Sale created by Admin/Staff. SaleID: {SaleID}", sale.SaleID);
                return RedirectToAction(nameof(Index));
            }
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "Username", sale.CustomerID);
            ViewData["EmployeeID"] = new SelectList(_context.Employees.Select(e => new { e.EmployeeID, FullName = e.FirstName + " " + e.LastName }), "EmployeeID", "FullName", sale.EmployeeID);
            ViewData["PaymentMethodID"] = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName", sale.PaymentMethodID);
            return View(sale);
        }

        // GET: Sales/Edit/5 (ADMIN VIEW)
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public async Task<IActionResult> Edit(int? id)
        {
            // ... (Code Edit GET của bạn giữ nguyên, có thể chỉnh SelectList tương tự Create)
            if (id == null) { return NotFound(); }
            var sale = await _context.Sales.FindAsync(id);
            if (sale == null) { return NotFound(); }
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "Username", sale.CustomerID);
            ViewData["EmployeeID"] = new SelectList(_context.Employees.Select(e => new { e.EmployeeID, FullName = e.FirstName + " " + e.LastName }), "EmployeeID", "FullName", sale.EmployeeID);
            ViewData["PaymentMethodID"] = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName", sale.PaymentMethodID);
            return View(sale);
        }

        // POST: Sales/Edit/5 (ADMIN VIEW)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public async Task<IActionResult> Edit(int id, [Bind("SaleID,SaleDate,CustomerID,EmployeeID,PaymentMethodID,DeliveryAddress,DeliveryTime,IsPickup,OrderStatus")] Sale sale)
        {
            // ... (Code Edit POST của bạn giữ nguyên)
            if (id != sale.SaleID) { return NotFound(); }
            if (ModelState.IsValid) { /* ... try-catch ... */ _context.Update(sale); await _context.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
            ViewData["CustomerID"] = new SelectList(_context.Customers, "CustomerID", "Username", sale.CustomerID);
            ViewData["EmployeeID"] = new SelectList(_context.Employees.Select(e => new { e.EmployeeID, FullName = e.FirstName + " " + e.LastName }), "EmployeeID", "FullName", sale.EmployeeID);
            ViewData["PaymentMethodID"] = new SelectList(_context.PaymentMethods, "PaymentMethodID", "MethodName", sale.PaymentMethodID);
            return View(sale);
        }

        // GET: Sales/Delete/5 (ADMIN VIEW)
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public async Task<IActionResult> Delete(int? id)
        {
            // ... (Code Delete GET của bạn giữ nguyên)
            if (id == null) { return NotFound(); }
            var sale = await _context.Sales.Include(s => s.Customer).Include(s => s.Employee).Include(s => s.PaymentMethod).FirstOrDefaultAsync(m => m.SaleID == id);
            if (sale == null) { return NotFound(); }
            return View(sale);
        }

        // POST: Sales/Delete/5 (ADMIN VIEW)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")] // Hoặc chỉ "Admin"
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // ... (Code Delete POST của bạn giữ nguyên)
            var sale = await _context.Sales.FindAsync(id);
            if (sale != null) { _context.Sales.Remove(sale); }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SaleExists(int id)
        {
            return _context.Sales.Any(e => e.SaleID == id);
        }

        // GET: /Sales/OrderHistory
        [Authorize(Roles = "Customer")] // Chỉ Customer mới được truy cập
        public async Task<IActionResult> OrderHistory(int page = 1)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int customerId))
            {
                _logger.LogWarning("OrderHistory: User not authenticated or CustomerID claim is missing/invalid.");
                return Challenge();
            }

            _logger.LogInformation("Fetching order history for CustomerID: {CustomerId}, Page: {Page}", customerId, page);

            int pageSize = 10;

            var query = _context.Sales
                .Where(s => s.CustomerID == customerId)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.ProductType)
                        .ThenInclude(pt => pt.MeasurementUnit)
                .Include(s => s.PaymentMethod)
                .OrderByDescending(s => s.SaleDate);

            var totalOrders = await query.CountAsync();
            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var orderViewModels = orders.Select(s => new OrderViewModel
            {
                SaleId = s.SaleID,
                SaleDate = s.SaleDate,
                OrderStatus = s.OrderStatus,
                PaymentMethodName = s.PaymentMethod?.MethodName ?? "N/A",
                DeliveryAddress = s.DeliveryAddress,
                IsPickup = s.IsPickup,
                DeliveryTime = s.DeliveryTime,
                TotalAmount = s.SaleDetails.Sum(sd => sd.Quantity * sd.ProductPriceAtPurchase),
                Items = s.SaleDetails.Select(sd => new OrderItemViewModel
                {
                    ProductName = sd.ProductType?.ProductName ?? "Sản phẩm không xác định",
                    Quantity = sd.Quantity,
                    PriceAtPurchase = sd.ProductPriceAtPurchase,
                    MeasurementUnit = sd.ProductType?.MeasurementUnit?.UnitName ?? "",
                    ImagePath = sd.ProductType?.ImagePath,
                    Subtotal = sd.Quantity * sd.ProductPriceAtPurchase
                }).ToList()
            }).ToList();

            var viewModel = new OrderHistoryViewModel
            {
                Orders = orderViewModels,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize), // Ép kiểu (double)
                TotalOrders = totalOrders
            };

            _logger.LogInformation("Returning {OrderCount} orders for CustomerID: {CustomerId}, Page: {Page}", orderViewModels.Count, customerId, page);
            return View(viewModel); // Sẽ tìm View tên "OrderHistory.cshtml"
        }

        // GET: Sales/OrderDetail/5
        [HttpGet("Sales/OrderDetail/{id:int}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> OrderDetail(int id)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdString, out int customerId))
                {
                    _logger.LogWarning("OrderDetail: User not authenticated or CustomerID claim for SaleID {SaleID}.", id);
                    TempData["ErrorMessage"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Account");
                }

                _logger.LogInformation("Fetching order detail for SaleID: {SaleID}, CustomerID: {CustomerId}", id, customerId);

                var sale = await _context.Sales
                    .Where(s => s.SaleID == id)
                    .Include(s => s.Customer)
                    .Include(s => s.Employee)
                    .Include(s => s.PaymentMethod)
                    .Include(s => s.SaleDetails)
                        .ThenInclude(sd => sd.ProductType)
                            .ThenInclude(pt => pt.MeasurementUnit)
                    .FirstOrDefaultAsync();

                if (sale == null)
                {
                    _logger.LogWarning("OrderDetail: Order with SaleID {SaleID} not found.", id);
                    TempData["ErrorMessage"] = "Đơn hàng không tồn tại hoặc đã bị xóa.";
                    return RedirectToAction("OrderHistory", "Sales");
                }

                if (sale.CustomerID != customerId)
                {
                    _logger.LogWarning("OrderDetail: Unauthorized access attempt. SaleID: {SaleID}, CustomerID: {CustomerId}.", id, customerId);
                    TempData["ErrorMessage"] = "Bạn không có quyền xem đơn hàng này.";
                    return RedirectToAction("OrderHistory", "Sales");
                }

                // ✅ Get cancellation reason from notification (if exists)
                string? cancellationMessage = null;
                var fullMessage = await _context.Notifications
                    .Where(n => n.SaleID == id &&
                                n.CustomerID == customerId &&
                                n.NotificationType == NotificationType.OrderStatusUpdate.GetDisplayName() &&
                                n.Message.Contains("đã bị từ chối"))
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => n.Message)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(fullMessage))
                {
                    const string reasonPrefix = "Lý do từ nhân viên: ";
                    var index = fullMessage.IndexOf(reasonPrefix, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        cancellationMessage = fullMessage.Substring(index + reasonPrefix.Length).Trim();
                    }
                    else
                    {
                        cancellationMessage = fullMessage; // fallback: show full message if no match
                    }
                }


                var viewModel = new OrderDetailViewModel
                {
                    SaleId = sale.SaleID,
                    SaleDate = sale.SaleDate,
                    OrderStatus = sale.OrderStatus,
                    CustomerName = $"{sale.Customer?.FirstName} {sale.Customer?.LastName}",
                    CustomerEmail = sale.Customer?.Email,
                    CustomerPhone = sale.Customer?.PhoneNumber,
                    EmployeeName = sale.Employee != null ? $"{sale.Employee.FirstName} {sale.Employee.LastName}" : "N/A",
                    PaymentMethodName = sale.PaymentMethod?.MethodName ?? "N/A",
                    DeliveryAddress = sale.DeliveryAddress,
                    DeliveryTime = sale.DeliveryTime,
                    IsPickup = sale.IsPickup,
                    TotalAmount = sale.SaleDetails.Sum(sd => sd.Quantity * sd.ProductPriceAtPurchase),
                    Items = sale.SaleDetails.Select(sd => new OrderItemViewModel
                    {
                        ProductName = sd.ProductType?.ProductName ?? "Sản phẩm không xác định",
                        Quantity = sd.Quantity,
                        PriceAtPurchase = sd.ProductPriceAtPurchase,
                        MeasurementUnit = sd.ProductType?.MeasurementUnit?.UnitName ?? "",
                        ImagePath = sd.ProductType?.ImagePath,
                        Subtotal = sd.Quantity * sd.ProductPriceAtPurchase
                    }).ToList(),
                    CancellationReason = cancellationMessage
                };

                _logger.LogInformation("Order detail loaded for SaleID: {SaleID}", id);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while loading order details.");
                TempData["ErrorMessage"] = "Đã có lỗi xảy ra khi tải thông tin đơn hàng. Vui lòng thử lại sau.";
                return RedirectToAction("OrderHistory", "Sales");
            }
        }



        // ➡️ GET: Sales/CustomerSales
        [HttpGet]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> CustomerSales(string statusFilter, string sortBy, string searchQuery)
        {
            // Populate ViewBag with status options and the currently selected filter
            ViewBag.Statuses = new List<string> { "Đã xác nhận", "Đang xử lý", "Hoàn thành", "Đã hủy" };
            ViewBag.SelectedStatus = statusFilter;

            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.PaymentMethod)
                .Include(s => s.SaleDetails)
                .ThenInclude(sd => sd.ProductType)
                .AsQueryable();

            // 🔍 Apply Search
            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(s =>
                    s.Customer.FirstName.Contains(searchQuery) ||
                    s.Customer.LastName.Contains(searchQuery) ||
                    s.SaleID.ToString().Contains(searchQuery));
            }

            // 🔍 Apply Status Filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(s => s.OrderStatus == statusFilter);
            }

            // 🔄 Apply Sorting
            query = sortBy switch
            {
                "DateAsc" => query.OrderBy(s => s.SaleDate),
                "DateDesc" => query.OrderByDescending(s => s.SaleDate),
                "TotalAsc" => query.OrderBy(s => s.SaleDetails.Sum(sd => sd.Quantity * sd.ProductPriceAtPurchase)),
                "TotalDesc" => query.OrderByDescending(s => s.SaleDetails.Sum(sd => sd.Quantity * sd.ProductPriceAtPurchase)),
                _ => query.OrderByDescending(s => s.SaleDate)
            };

            var sales = await query.ToListAsync();
            return View(sales);
        }


        // GET: Sales/OrderDetail/5
        [HttpGet("Sales/StaffOrderDetail/{id:int}")]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> StaffOrderDetail(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out int customerId))
            {
                _logger.LogWarning("OrderDetail: User not authenticated or CustomerID claim for SaleID {SaleID}.", id);
                return Challenge();
            }

            _logger.LogInformation("Fetching order detail for SaleID: {SaleID}, CustomerID: {CustomerId}", id, customerId);

            var sale = await _context.Sales
                .Where(s => s.SaleID == id)
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.PaymentMethod)
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.ProductType)
                        .ThenInclude(pt => pt.MeasurementUnit)
                .FirstOrDefaultAsync();

            if (sale == null)
            {
                _logger.LogWarning("OrderDetail: Order with SaleID {SaleID} not found for CustomerID {CustomerId}.", id, customerId);
                return NotFound("Không tìm thấy đơn hàng.");
            }

            var viewModel = new OrderDetailViewModel
            {
                SaleId = sale.SaleID,
                SaleDate = sale.SaleDate,
                OrderStatus = sale.OrderStatus,
                CustomerName = $"{sale.Customer?.FirstName} {sale.Customer?.LastName}",
                CustomerEmail = sale.Customer?.Email,
                CustomerPhone = sale.Customer?.PhoneNumber,
                EmployeeName = sale.Employee != null ? $"{sale.Employee.FirstName} {sale.Employee.LastName}" : "N/A",
                PaymentMethodName = sale.PaymentMethod?.MethodName ?? "N/A",
                DeliveryAddress = sale.DeliveryAddress,
                DeliveryTime = sale.DeliveryTime,
                IsPickup = sale.IsPickup,
                TotalAmount = sale.SaleDetails.Sum(sd => sd.Quantity * sd.ProductPriceAtPurchase),
                Items = sale.SaleDetails.Select(sd => new OrderItemViewModel
                {
                    ProductName = sd.ProductType?.ProductName ?? "Sản phẩm không xác định",
                    Quantity = sd.Quantity,
                    PriceAtPurchase = sd.ProductPriceAtPurchase,
                    MeasurementUnit = sd.ProductType?.MeasurementUnit?.UnitName ?? "",
                    ImagePath = sd.ProductType?.ImagePath,
                    Subtotal = sd.Quantity * sd.ProductPriceAtPurchase
                }).ToList()
            };

            _logger.LogInformation("Order detail loaded for SaleID: {SaleID}", id);
            return View(viewModel);
        }

        // ➡️ POST: Sales/UpdateOrderStatus
        [HttpPost]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> UpdateOrderStatus(int saleId, string newStatus)
        {
            // 🔍 Find the sale and include customer information
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.SaleID == saleId);

            if (sale == null)
            {
                return NotFound("Đơn hàng không tồn tại.");
            }

            if (sale.OrderStatus == "Đã hủy")
            {
                return BadRequest("Không thể cập nhật trạng thái cho đơn hàng đã bị hủy.");
            }

            var current = sale.OrderStatus;
            var validTransitions = new Dictionary<string, string>
            {
                { "Đã xác nhận", "Đang xử lý" },
                { "Đang xử lý", "Hoàn thành" }
            };

            if (!validTransitions.ContainsKey(current) || validTransitions[current] != newStatus)
            {
                return BadRequest("Chỉ được chuyển tiếp sang trạng thái hợp lệ.");
            }

            // ✅ Update the order status
            sale.OrderStatus = newStatus;
            await _context.SaveChangesAsync();

            // 🔔 Send Notification to Customer with SaleID
            var notification = new Notification
            {
                CustomerID = sale.CustomerID,
                SaleID = sale.SaleID,     // ✅ Added SaleID to the notification
                Title = "Trạng thái đơn hàng đã thay đổi",
                Message = $"Đơn hàng #{saleId} của bạn đã được cập nhật sang trạng thái: '{newStatus}'.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                NotificationType = NotificationType.OrderStatusUpdate.GetDisplayName()
            };

            // ✅ Save the notification
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(StaffOrderDetail), new { id = saleId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CancelOrder(int saleId)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.SaleID == saleId);

            if (sale == null)
            {
                return NotFound("Đơn hàng không tồn tại.");
            }

            // Only allow cancellation from specific statuses
            if (sale.OrderStatus == "Đã xác nhận" || sale.OrderStatus == "Đang xử lý")
            {
                sale.OrderStatus = "Đã hủy";
                await _context.SaveChangesAsync();

                // 🔔 Send notification to the customer
                var notification = new Notification
                {
                    CustomerID = sale.CustomerID,
                    SaleID = sale.SaleID,
                    Title = "Đơn hàng đã bị hủy",
                    Message = $"Đơn hàng #{saleId} của bạn đã được hủy theo yêu cầu.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    NotificationType = NotificationType.OrderStatusUpdate.GetDisplayName()
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(OrderHistory));
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> CancelOrderFromStaff(int saleId, string reason)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.SaleID == saleId);

            if (sale == null)
                return NotFound("Đơn hàng không tồn tại.");

            if (sale.OrderStatus != "Đã xác nhận" && sale.OrderStatus != "Đang xử lý")
                return BadRequest("Chỉ có thể hủy đơn hàng chưa hoàn thành.");

            // Cập nhật trạng thái
            sale.OrderStatus = "Bị từ chối";
            await _context.SaveChangesAsync();

            // Gửi thông báo cho khách hàng
            var notification = new Notification
            {
                CustomerID = sale.CustomerID,
                SaleID = sale.SaleID,
                Title = "Đơn hàng đã bị hủy",
                Message = $"Rất tiếc, đơn hàng #{saleId} của bạn đã bị từ chối. Lý do từ nhân viên: {reason}",
                CreatedAt = DateTime.Now,
                IsRead = false,
                NotificationType = NotificationType.OrderStatusUpdate.GetDisplayName()
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(StaffOrderDetail), new { id = saleId });
        }

    }
}