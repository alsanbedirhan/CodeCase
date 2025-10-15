using CodeCase;
using DBConnection;
public static class Program
{
    public static string mongoConnectionString;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllersWithViews();

        mongoConnectionString = builder.Configuration["MongoDBConnectionString"];
        builder.Services.AddSingleton(new MongoDbContext(mongoConnectionString!));

        var app = builder.Build();

        app.UseMiddleware<ExceptionMiddleware>();

        app.UseRouting();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Work}/{action=Index}/{id?}");

        app.Run();
    }
}