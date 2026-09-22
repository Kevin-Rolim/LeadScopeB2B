var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
// builder.Services.AddDbContext<LeadScopeB2BContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("LeadScopeB2B")));
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllerRoute("default", "{controller}/{action}");

app.Run();
