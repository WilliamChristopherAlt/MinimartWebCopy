using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
//using MinimartWeb.BOs;
//using MinimartWeb.DAOs;
using MinimartWeb;
using MinimartWeb.Data;

var builder = WebApplication.CreateBuilder(args);

//// Register DAOs
//builder.Services.AddScoped<CategoryDAO>();
//builder.Services.AddScoped<SupplierDAO>();
//builder.Services.AddScoped<MeasurementUnitDAO>();
//builder.Services.AddScoped<ProductTypeDAO>();
//builder.Services.AddScoped<CustomerDAO>();
//builder.Services.AddScoped<EmployeeRoleDAO>();
//builder.Services.AddScoped<EmployeeDAO>();
//builder.Services.AddScoped<AdminDAO>();
//builder.Services.AddScoped<PaymentMethodDAO>();
//builder.Services.AddScoped<SaleDAO>();
//builder.Services.AddScoped<SaleDetailDAO>();

//// Register BOs
//builder.Services.AddScoped<CategoryBO>();
//builder.Services.AddScoped<SupplierBO>();
//builder.Services.AddScoped<MeasurementUnitBO>();
//builder.Services.AddScoped<ProductTypeBO>();
//builder.Services.AddScoped<CustomerBO>();
//builder.Services.AddScoped<EmployeeRoleBO>();
//builder.Services.AddScoped<EmployeeBO>();
//builder.Services.AddScoped<AdminBO>();
//builder.Services.AddScoped<PaymentMethodBO>();
//builder.Services.AddScoped<SaleBO>();
//builder.Services.AddScoped<SaleDetailBO>();


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
