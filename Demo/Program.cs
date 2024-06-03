using System.Reflection;
using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Demo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var dbId = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.UseInMemoryDatabase(dbId);
                }
            );

            builder.Services.AddLogging();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddAsyncMonolith<ApplicationDbContext>(Assembly.GetExecutingAssembly());
            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
