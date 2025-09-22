using YourApp.Models;

var builder = WebApplication.CreateBuilder(args);

// Vincular la sección de configuración
builder.Services.Configure<DeployedAppsCatalogOptions>(
    builder.Configuration.GetSection("DeployedAppsCatalog"));

builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();