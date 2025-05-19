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

        // Trong CartController.cs

        // ... (using statements và constructor giữ nguyên) ...

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(int saleId, Dictionary<int, decimal> updatedQuantities, int? removeProductId)
        {
            // 1. Xác thực người dùng và quyền truy cập giỏ hàng
            if (!User.Identity.IsAuthenticated || string.IsNullOrEmpty(User.Identity.Name))
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để quản lý giỏ hàng.";
                return RedirectToAction("Login", "Account"); // Hoặc trả về Unauthorized() nếu là API
            }

            var username = User.Identity.Name;
            var customer = await _context.Customers.AsNoTracking() // AsNoTracking vì có thể không cần update Customer
                                     .FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
                return RedirectToAction("Login", "Account"); // Hoặc Unauthorized()
            }

            // 2. Lấy giỏ hàng (Sale "Chờ xử lý") của khách hàng
            var sale = await _context.Sales
                .Include(s => s.SaleDetails) // Cần SaleDetails để cập nhật và kiểm tra
                .FirstOrDefaultAsync(s => s.SaleID == saleId &&
                                         s.CustomerID == customer.CustomerID && // Đảm bảo đúng giỏ hàng của user
                                         s.OrderStatus == "Chờ xử lý");

            if (sale == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy giỏ hàng hoặc giỏ hàng không hợp lệ.";
                return RedirectToAction("Index", "Home"); // Hoặc trang giỏ hàng rỗng nếu muốn
            }

            bool cartWasModified = false;

            // 3. Xử lý xóa sản phẩm nếu có removeProductId (thường từ nút "Xóa" riêng)
            if (removeProductId.HasValue)
            {
                var detailToRemove = sale.SaleDetails.FirstOrDefault(d => d.ProductTypeID == removeProductId.Value);
                if (detailToRemove != null)
                {
                    _context.SaleDetails.Remove(detailToRemove);
                    sale.SaleDetails.Remove(detailToRemove); // Cũng xóa khỏi collection trong bộ nhớ của sale
                    cartWasModified = true;
                    TempData["SuccessMessage"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
                }
                else
                {
                    TempData["WarningMessage"] = "Không tìm thấy sản phẩm cần xóa trong giỏ hàng.";
                }
            }
            // 4. Nếu không phải xóa trực tiếp, thì xử lý cập nhật số lượng từ updatedQuantities
            else if (updatedQuantities != null && updatedQuantities.Any())
            {
                List<SaleDetail> itemsToRemoveDueToZeroQuantity = new List<SaleDetail>();

                foreach (var item in sale.SaleDetails.ToList()) // ToList() để có thể xóa item khỏi collection gốc khi duyệt
                {
                    if (updatedQuantities.TryGetValue(item.ProductTypeID, out var newQuantity))
                    {
                        var product = await _context.ProductTypes.AsNoTracking()
                                                .Include(pt => pt.MeasurementUnit)
                                                .FirstOrDefaultAsync(pt => pt.ProductTypeID == item.ProductTypeID);

                        if (product == null) continue; // Bỏ qua nếu sản phẩm không còn tồn tại (hiếm khi)

                        bool isContinuous = product.MeasurementUnit?.IsContinuous ?? false;
                        decimal minAllowedQuantity = isContinuous ? 0.01m : 1m;

                        if (newQuantity < (isContinuous ? 0 : minAllowedQuantity)) // Nếu số lượng mới <= 0 cho sản phẩm rời, hoặc < 0 cho liên tục
                        {
                            itemsToRemoveDueToZeroQuantity.Add(item);
                        }
                        else if (newQuantity > product.StockAmount)
                        {
                            TempData["WarningMessage"] = $"Số lượng cập nhật cho '{product.ProductName}' ({newQuantity}) vượt quá tồn kho ({product.StockAmount}). Số lượng không được thay đổi.";
                            // Không thay đổi số lượng của item này
                        }
                        else if (item.Quantity != newQuantity)
                        {
                            item.Quantity = newQuantity;
                            cartWasModified = true;
                        }
                    }
                }

                if (itemsToRemoveDueToZeroQuantity.Any())
                {
                    _context.SaleDetails.RemoveRange(itemsToRemoveDueToZeroQuantity);
                    foreach (var itemToRemove in itemsToRemoveDueToZeroQuantity)
                    {
                        sale.SaleDetails.Remove(itemToRemove); // Xóa khỏi collection trong bộ nhớ
                    }
                    cartWasModified = true;
                    if (TempData["SuccessMessage"] == null) // Chỉ set nếu chưa có thông báo thành công nào khác
                    {
                        TempData["SuccessMessage"] = "Đã cập nhật giỏ hàng và xóa các sản phẩm có số lượng bằng 0.";
                    }
                }
                else if (cartWasModified && TempData["SuccessMessage"] == null)
                {
                    TempData["SuccessMessage"] = "Đã cập nhật số lượng sản phẩm trong giỏ hàng.";
                }
            }

            // 5. Lưu thay đổi nếu có
            if (cartWasModified)
            {
                try
                {
                    await _context.SaveChangesAsync();

                    // Nếu sau khi cập nhật/xóa mà giỏ hàng hoàn toàn trống, xóa luôn Sale "Chờ xử lý"
                    if (!sale.SaleDetails.Any())
                    {
                        // Double check trong DB để chắc chắn
                        var remainingDetailsCount = await _context.SaleDetails.CountAsync(sd => sd.SaleID == sale.SaleID);
                        if (remainingDetailsCount == 0)
                        {
                            _context.Sales.Remove(sale);
                            await _context.SaveChangesAsync();
                            TempData["InfoMessage"] = "Giỏ hàng của bạn hiện đã trống.";
                            // Client sẽ tự gọi GetCartItemCount khi trang giỏ hàng tải lại,
                            // hoặc bạn có thể trigger custom event ở đây nếu cần cập nhật badge ngay lập tức
                            // trước khi redirect (nhưng redirect là đủ).
                        }
                    }
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // _logger.LogError(ex, "Lỗi tương tranh khi cập nhật giỏ hàng SaleID {SaleID}", sale.SaleID);
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật giỏ hàng do dữ liệu đã thay đổi. Vui lòng thử lại.";
                }
                catch (Exception ex)
                {
                    // _logger.LogError(ex, "Lỗi khi lưu thay đổi giỏ hàng SaleID {SaleID}", sale.SaleID);
                    TempData["ErrorMessage"] = "Có lỗi không xác định xảy ra khi cập nhật giỏ hàng.";
                }
            }
            else if (!removeProductId.HasValue && (updatedQuantities == null || !updatedQuantities.Any()))
            {
                // Không có hành động nào được thực hiện (không xóa, không có số lượng cập nhật)
                // Có thể không cần thông báo gì, hoặc một thông báo "Không có gì để cập nhật"
            }


            return RedirectToAction(nameof(Index)); // Luôn redirect về trang giỏ hàng để hiển thị trạng thái mới nhất
        }

        // ... (Các action AddToCart, ConfirmOrder, BuyNow, GetCartItemCount, OrderHistory của bạn giữ nguyên hoặc đã được cập nhật) ...



        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity)
        {
            // 1. Nếu số lượng truyền vào nhỏ hơn 1, gán mặc định là 1
            if (quantity == 0)
            {
                quantity = 1;
            }

            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });
            }

            var username = User.Identity.Name;
            var customer = await _context.Customers
                                     .Include(c => c.Sales) // Include Sales để có thể truy cập địa chỉ từ đơn hàng gần nhất nếu cần
                                     .FirstOrDefaultAsync(c => c.Username == username);
            if (customer == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng." });
            }

            var sale = await _context.Sales
                .Include(s => s.SaleDetails)
                .FirstOrDefaultAsync(s => s.CustomerID == customer.CustomerID && s.OrderStatus == "Chờ xử lý");

            if (sale == null)
            {
                sale = new Sale
                {
                    CustomerID = customer.CustomerID,
                    EmployeeID = 1, // Tạm thời: Nên có logic để gán EmployeeID quản lý đơn hàng này
                    PaymentMethodID = 1, // Tạm thời: Phương thức thanh toán mặc định
                    DeliveryAddress = " ", // Lấy địa chỉ từ thông tin khách hàng nếu có
                    DeliveryTime = DateTime.Now.AddDays(2), // Thời gian giao hàng dự kiến
                    IsPickup = false,
                    OrderStatus = "Chờ xử lý",
                    SaleDate = DateTime.Now
                };
                _context.Sales.Add(sale);
                // Cần SaveChanges ở đây để sale có SaleID trước khi thêm SaleDetails
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // _logger.LogError(ex, "Lỗi khi tạo Sale mới cho CustomerID {CustomerId}", customer.CustomerID);
                    Console.WriteLine($"Lỗi khi tạo Sale mới cho CustomerID {customer.CustomerID}: {ex.Message}");
                    return Json(new { success = false, message = "Có lỗi xảy ra khi tạo giỏ hàng mới." });
                }
            }

            var product = await _context.ProductTypes
                                    .Include(p => p.MeasurementUnit) // Include MeasurementUnit để lấy UnitName
                                    .FirstOrDefaultAsync(p => p.ProductTypeID == productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm." });
            }

            // 2. Kiểm tra tồn kho
            var existingDetail = sale.SaleDetails.FirstOrDefault(sd => sd.ProductTypeID == productId);
            decimal quantityNeededInCart = quantity; // Số lượng cần cho sản phẩm này trong giỏ hàng
            if (existingDetail != null)
            {
                quantityNeededInCart = existingDetail.Quantity + quantity;
            }

            if (product.StockAmount < quantityNeededInCart)
            {
                return Json(new
                {
                    success = false,
                    message = $"Xin lỗi, số lượng tồn kho của sản phẩm '{product.ProductName}' không đủ. Hiện còn {product.StockAmount} {(product.MeasurementUnit?.UnitName ?? "đơn vị")}."
                });
            }

            // Thêm hoặc cập nhật chi tiết đơn hàng
            if (existingDetail != null)
            {
                existingDetail.Quantity += quantity; // quantity đã được đảm bảo >= 1
            }
            else
            {
                var newDetail = new SaleDetail
                {
                    // SaleID sẽ được EF Core tự động gán nếu thêm vào collection sale.SaleDetails
                    // và sale đã được tracked (hoặc bạn có thể gán sale.SaleID)
                    ProductTypeID = productId,
                    Quantity = quantity, // quantity đã được đảm bảo >= 1
                    ProductPriceAtPurchase = product.Price,
                    Sale = sale // Thiết lập quan hệ
                };
                //_context.SaleDetails.Add(newDetail); // Không cần thiết nếu bạn dùng sale.SaleDetails.Add
                sale.SaleDetails.Add(newDetail);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) // Bắt lỗi cụ thể hơn nếu có thể
            {
                // _logger.LogError(ex, "Lỗi DbUpdateException khi cập nhật SaleDetails cho SaleID {SaleID}", sale.SaleID);
                Console.WriteLine($"Lỗi DbUpdateException khi cập nhật SaleDetails cho SaleID {sale.SaleID}: {ex.InnerException?.Message ?? ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật chi tiết giỏ hàng." });
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Lỗi không xác định khi lưu giỏ hàng cho SaleID {SaleID}", sale.SaleID);
                Console.WriteLine($"Lỗi không xác định khi lưu giỏ hàng cho SaleID {sale.SaleID}: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi không xác định xảy ra khi cập nhật giỏ hàng." });
            }

            // 3. Cập nhật thông báo thành công
            return Json(new
            {
                success = true,
                message = $"✅ Thêm thành công {quantity} {(product.MeasurementUnit?.UnitName ?? "sản phẩm")} <strong>{product.ProductName}</strong> vào giỏ hàng.",
                productName = product.ProductName
                // Bạn có thể cân nhắc trả về tổng số lượng item trong giỏ hàng hiện tại
                // để client cập nhật badge ngay mà không cần gọi API GetCartItemCount nữa.
                // currentCartTotalItems = sale.SaleDetails.Sum(sd => (int)sd.Quantity)
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

        [HttpGet]
        public async Task<IActionResult> GetCartItemCount()
        {
            if (!User.Identity.IsAuthenticated)
            {
                // Nếu người dùng chưa đăng nhập, giỏ hàng trống
                return Json(new { count = 0 });
            }

            var username = User.Identity.Name;
            var customer = await _context.Customers.AsNoTracking() // AsNoTracking vì chỉ đọc
                                     .FirstOrDefaultAsync(c => c.Username == username);

            if (customer == null)
            {
                // Không tìm thấy khách hàng, coi như giỏ hàng trống
                return Json(new { count = 0 });
            }

            // Tìm đơn hàng "Chờ xử lý" của khách hàng
            var pendingSale = await _context.Sales
                .AsNoTracking() // Chỉ đọc
                .Include(s => s.SaleDetails) // Cần SaleDetails để đếm
                .FirstOrDefaultAsync(s => s.CustomerID == customer.CustomerID && s.OrderStatus == "Chờ xử lý");

            int itemCount = 0;
            if (pendingSale != null && pendingSale.SaleDetails != null)
            {
                // Cách 1: Đếm số loại sản phẩm khác nhau trong giỏ
                // itemCount = pendingSale.SaleDetails.Count;

                // Cách 2: Đếm tổng số lượng của tất cả các sản phẩm (phổ biến hơn cho hiển thị badge)
                itemCount = pendingSale.SaleDetails.Sum(sd => (int)sd.Quantity); // Ép kiểu Quantity sang int nếu nó là decimal
                                                                                 // hoặc dùng Convert.ToInt32(sd.Quantity)
            }

            return Json(new { count = itemCount });
        }
    }

}