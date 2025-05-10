// Trong Program.cs

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MinimartWeb.Data;
using MinimartWeb.Services; // Cho RecommendationService VÀ IEmailSender, EmailSender

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication & Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.LoginPath = "/Account/Login"; // Trang sẽ redirect đến nếu chưa đăng nhập và cố truy cập trang yêu cầu AuthN
        options.AccessDeniedPath = "/Account/AccessDenied"; // Trang sẽ redirect đến nếu bị từ chối quyền (AuthZ)
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // Thời gian cookie tồn tại (ví dụ 7 ngày)
        options.SlidingExpiration = true; // Gia hạn cookie nếu người dùng hoạt động
    });
// Bỏ FallbackPolicy nếu muốn trang chủ và các trang khác công khai theo mặc định
//builder.Services.AddAuthorization(options => {
//     options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
// });
builder.Services.AddAuthorization(); // Cần thiết để các attribute [Authorize] hoạt động

// Session
builder.Services.AddDistributedMemoryCache(); // Cần thiết cho session lưu trong bộ nhớ
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian session không hoạt động trước khi hết hạn
    options.Cookie.HttpOnly = true; // Giúp bảo vệ cookie khỏi truy cập bằng JavaScript phía client
    options.Cookie.IsEssential = true; // Đảm bảo cookie session được tạo ngay cả khi người dùng chưa đồng ý với cookie policy (quan trọng cho GDPR)
});

// Logger (thường đã được đăng ký mặc định, nhưng để rõ ràng thì có thể thêm)
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    // Thêm các provider logging khác nếu cần (ví dụ: file, Application Insights)
});

// === ĐĂNG KÝ DỊCH VỤ EMAIL ===
// Đăng ký IEmailSender và triển khai EmailSender của nó.
// AddTransient: Một instance mới của EmailSender sẽ được tạo mỗi khi IEmailSender được yêu cầu.
// Điều này phù hợp cho EmailSender vì nó không thường giữ trạng thái phức tạp giữa các lần gửi.
builder.Services.AddTransient<IEmailSender, EmailSender>();


// Custom Services
builder.Services.AddScoped<IRecommendationService, RecommendationService>(); // Đăng ký service gợi ý


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error"); // Middleware xử lý lỗi cho môi trường production
    app.UseHsts(); // Thêm header Strict-Transport-Security
}
else
{
    app.UseDeveloperExceptionPage(); // Hiển thị trang lỗi chi tiết cho môi trường development
    // app.UseMigrationsEndPoint(); // Nếu bạn dùng EF Core Migrations và Identity
}

app.UseHttpsRedirection(); // Chuyển hướng HTTP sang HTTPS
app.UseStaticFiles(); // Cho phép phục vụ các file tĩnh (CSS, JS, hình ảnh) từ wwwroot

app.UseRouting(); // Thêm middleware định tuyến

app.UseSession(); // QUAN TRỌNG: Đặt UseSession() trước UseAuthentication và UseAuthorization nếu session được dùng trong logic xác thực/phân quyền

app.UseAuthentication(); // Thêm middleware xác thực (kiểm tra cookie, v.v.)
app.UseAuthorization(); // Thêm middleware phân quyền (kiểm tra vai trò, policy)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();