// Trong Program.cs (ví dụ .NET 6+)

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// --- CẤU HÌNH XÁC THỰC COOKIE (Bạn có thể đã có phần này) ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Quan trọng: Chỉ định đường dẫn trang Login để hệ thống tự động chuyển hướng khi truy cập trang cần Authorize mà chưa đăng nhập
        // Mặc dù người dùng sẽ dùng panel AJAX, đường dẫn này vẫn cần để middleware hoạt động đúng.
        // Nó sẽ bị bắt bởi [HttpGet] Login và chuyển hướng về Home.
        options.LoginPath = "/Account/Login";
        // Chỉ định đường dẫn trang Access Denied
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Ví dụ thời gian hết hạn
        options.SlidingExpiration = true; // Gia hạn cookie nếu có hoạt động
    });

// --- THÊM CẤU HÌNH AUTHORIZATION TOÀN CỤC ---
builder.Services.AddAuthorization(options =>
{
    // Thiết lập FallbackPolicy: Chính sách này sẽ áp dụng cho tất cả các endpoint
    // không có thuộc tính [Authorize] hoặc [AllowAnonymous] nào được chỉ định rõ ràng.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser() // Yêu cầu người dùng phải được xác thực (đăng nhập)
        .Build();
});


// --- Các dịch vụ khác (DbContext, Logger, v.v.) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))); // Thay bằng chuỗi kết nối của bạn

// Thêm Logger (nếu dùng trong Controller)
builder.Services.AddLogging();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// --- KÍCH HOẠT MIDDLEWARE XÁC THỰC VÀ AUTHORIZATION ---
// Đặt UseAuthentication() TRƯỚC UseAuthorization()
app.UseAuthentication(); // Xác định người dùng là ai (dựa trên cookie)
app.UseAuthorization(); // Kiểm tra xem người dùng có quyền truy cập không


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();