using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace SampleWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                // Needed for using scoped services (for example DbContext) in validators
                //.UseDefaultServiceProvider(options => options.ValidateScopes = false)
                .UseStartup<Startup>()
                .Build();
    }
}
