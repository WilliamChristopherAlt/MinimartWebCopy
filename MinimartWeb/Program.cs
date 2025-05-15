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
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(); // Cần thiết để các attribute [Authorize] hoạt động

// Session
builder.Services.AddDistributedMemoryCache(); // Cần thiết cho session lưu trong bộ nhớ
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Logger
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

// Email Service
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Custom Services
builder.Services.AddScoped<IRecommendationService, RecommendationService>();


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

// app.UseDeveloperExceptionPage(); // Nếu bạn muốn luôn thấy lỗi chi tiết (tạm thời khi debug)

app.UseHttpsRedirection(); // Chuyển hướng HTTP sang HTTPS
app.UseStaticFiles(); // Cho phép phục vụ các file tĩnh (CSS, JS, hình ảnh) từ wwwroot

app.UseRouting(); // Thêm middleware định tuyến (phải trước UseAuthentication/Authorization/Session và Map...)

// Thứ tự quan trọng của các middleware xác thực, phân quyền và session:
app.UseAuthentication(); // Middleware xác thực (kiểm tra cookie, thiết lập ClaimsPrincipal)
app.UseAuthorization();  // Middleware phân quyền (kiểm tra vai trò, policy dựa trên ClaimsPrincipal)

app.UseSession();        // Middleware Session (cho TempData và các tính năng session khác)

// Map Endpoints (Controllers, Razor Pages, etc.)
app.MapControllerRoute(
    name: "areas", // Hoặc "AdminArea"
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// app.MapRazorPages(); // Bỏ comment nếu bạn có sử dụng Razor Pages

app.Run();