using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Model;
using MinimartWeb.Models;
using System.Security.Claims;

namespace MinimartWeb.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            var username = User.Identity.Name;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null)
                return RedirectToAction("Login", "Account");

            var pendingSales = await _context.Sales
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.ProductType)
                        .ThenInclude(pt => pt.MeasurementUnit)
                .Where(s => s.CustomerID == customer.CustomerID && s.OrderStatus == "Chờ xử lý")
                .ToListAsync();


            if (pendingSales.Count > 1)
            {
                return View("Error", new ErrorViewModel
                {
                    RequestId = HttpContext.TraceIdentifier,
                    ErrorMessage = "Dữ liệu giỏ hàng bị lỗi: có nhiều hơn 1 đơn hàng đang chờ xử lý cho người dùng hiện tại."
                });
            }

            var sale = pendingSales.FirstOrDefault();
            return View(sale); // can be null
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(int saleId, Dictionary<int, decimal> updatedQuantities, int? removeProductId)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleDetails)
                .FirstOrDefaultAsync(s => s.SaleID == saleId && s.OrderStatus == "Chờ xử lý");

            if (sale == null)
            {
                return NotFound();
            }

            // ✅ Remove product if requested
            if (removeProductId.HasValue)
            {
                var detailToRemove = sale.SaleDetails.FirstOrDefault(d => d.ProductTypeID == removeProductId.Value);
                if (detailToRemove != null)
                {
                    _context.SaleDetails.Remove(detailToRemove);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }

            // ✅ Otherwise, update quantities
            foreach (var item in sale.SaleDetails)
            {
                if (updatedQuantities.TryGetValue(item.ProductTypeID, out var newQty))
                {
                    item.Quantity = newQty;
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }



        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });
            }

            var username = User.Identity.Name;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng." });
            }

            // ✅ Dùng đúng trạng thái đơn hàng "Chờ xử lý"
            var sale = await _context.Sales
                .Include(s => s.SaleDetails)
                .FirstOrDefaultAsync(s => s.CustomerID == customer.CustomerID && s.OrderStatus == "Chờ xử lý");

            if (sale == null)
            {
                sale = new Sale
                {
                    CustomerID = customer.CustomerID,
                    EmployeeID = 1, // tạm thời
                    PaymentMethodID = 1,
                    DeliveryAddress = "",
                    DeliveryTime = DateTime.Now.AddDays(1),
                    IsPickup = false,
                    OrderStatus = "Chờ xử lý",
                    SaleDate = DateTime.Now
                };
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
            }

            var product = await _context.ProductTypes.FindAsync(productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm." });
            }

            var existingDetail = sale.SaleDetails.FirstOrDefault(sd => sd.ProductTypeID == productId);
            if (existingDetail != null)
            {
                existingDetail.Quantity += quantity;
            }
            else
            {
                var detail = new SaleDetail
                {
                    SaleID = sale.SaleID,
                    ProductTypeID = productId,
                    Quantity = quantity,
                    ProductPriceAtPurchase = product.Price
                };
                _context.SaleDetails.Add(detail);
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"✅ <strong>{product.ProductName}</strong> đã được thêm vào giỏ hàng.",
                productName = product.ProductName
            });
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmOrder(int saleId)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.ProductType)
                        .ThenInclude(pt => pt.MeasurementUnit)
                .Include(s => s.PaymentMethod)
                .FirstOrDefaultAsync(s => s.SaleID == saleId && s.OrderStatus == "Chờ xử lý");

            if (sale == null)
                return NotFound();

            ViewBag.PaymentMethods = await _context.PaymentMethods.ToListAsync();
            return View(sale);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder(int saleId, int paymentMethodId, string DeliveryAddress)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.SaleID == saleId && s.OrderStatus == "Chờ xử lý");

            if (sale == null)
                return NotFound();

            sale.PaymentMethodID = paymentMethodId;
            sale.DeliveryAddress = DeliveryAddress;
            sale.OrderStatus = "Đã xác nhận";
            sale.SaleDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // ✅ Create Notification
            var notification = new Notification
            {
                CustomerID = sale.CustomerID.GetValueOrDefault(), // This will default to 0 if it's null
                SaleID = sale.SaleID,
                Title = "Đơn hàng đã được xác nhận",
                Message = $"Đơn hàng #{sale.SaleID} của bạn đã được xác nhận. Chúng tôi sẽ sớm giao hàng cho bạn.",
                CreatedAt = DateTime.Now,
                IsRead = false,
                NotificationType = NotificationType.OrderStatusUpdate.GetDisplayName()
            };


            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đơn hàng đã được xác nhận!";
            return RedirectToAction("OrderDetail", "Sales", new { id = sale.SaleID });
        }


        [HttpPost]
        public async Task<IActionResult> BuyNow(int productId, int quantity)
        {
            Console.WriteLine($"\n[DEBUG] Entered BuyNow with productId={productId}, quantity={quantity}\n");

            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để mua sản phẩm này." });
            }

            var username = User.Identity.Name;
            Console.WriteLine($"\n[DEBUG] Authenticated user: {username}\n");

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null)
            {
                Console.WriteLine("\n[DEBUG] Customer not found. Redirecting to login.\n");
                return RedirectToAction("Login", "Account");
            }

            var sale = await _context.Sales
                .Include(s => s.SaleDetails)
                .FirstOrDefaultAsync(s => s.CustomerID == customer.CustomerID && s.OrderStatus == "Chờ xử lý");

            if (sale == null)
            {
                Console.WriteLine("\n[DEBUG] No existing pending sale. Creating new sale.\n");

                sale = new Sale
                {
                    CustomerID = customer.CustomerID,
                    EmployeeID = 1,
                    PaymentMethodID = 1,
                    DeliveryAddress = "",
                    DeliveryTime = DateTime.Now.AddDays(1),
                    IsPickup = false,
                    OrderStatus = "Chờ xử lý",
                    SaleDate = DateTime.Now
                };
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
            }
            else
            {
                Console.WriteLine($"\n[DEBUG] Found existing sale: SaleID={sale.SaleID}. Clearing SaleDetails.\n");
            }

            // Remove other SaleDetails and replace with the current product
            _context.SaleDetails.RemoveRange(sale.SaleDetails);

            var product = await _context.ProductTypes.FindAsync(productId);
            if (product == null)
            {
                Console.WriteLine($"\n[DEBUG] Product not found: productId={productId}\n");
                return NotFound();
            }

            _context.SaleDetails.Add(new SaleDetail
            {
                SaleID = sale.SaleID,
                ProductTypeID = productId,
                Quantity = quantity,
                ProductPriceAtPurchase = product.Price
            });

            await _context.SaveChangesAsync();

            Console.WriteLine($"\n[DEBUG] Sale updated successfully. Redirecting to ConfirmOrder. SaleID={sale.SaleID}\n");

            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("OrderDetail", "Sales", new { id = sale.SaleID }),
                message = $"⚡ <strong>{product.ProductName}</strong> has been redirected to confirmation."
            });

        }

        [HttpGet]
        public async Task<IActionResult> OrderHistory()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            var username = User.Identity.Name;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null)
                return RedirectToAction("Login", "Account");

            var orders = await _context.Sales
                .Where(s => s.CustomerID == customer.CustomerID && s.OrderStatus != "Chờ xử lý")
                .Include(s => s.SaleDetails)
                    .ThenInclude(sd => sd.ProductType)
                .Include(s => s.PaymentMethod)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            return View(orders);
        }

    }
}