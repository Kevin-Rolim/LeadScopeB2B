using Microsoft.EntityFrameworkCore;
using LeadScopeB2B.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");
builder.Services.AddDbContext<LeadScopeB2BDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAntiforgery();
app.UseAuthorization();
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
