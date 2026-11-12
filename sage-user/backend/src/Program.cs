using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SageUserApi
{
    public class Program
    {
        private const string DefaultPort = "5090";

        /// <summary>
        /// Entry point. Optional argument: --port=NNNNN (same convention as the Admin API, used by run.sh / Docker).
        /// </summary>
        public static void Main(string[] _args)
        {
            string _serverPort = DefaultPort;
            if (_args.Length > 0)
            {
                string[] _split = _args[0].Split('=');
                if (_split.Length == 2 && _split[0].Trim().ToLower() == "--port")
                {
                    string _port = _split[1].Trim();
                    if (_port.Length > 0 && _port.Length < 6 && _port.All(char.IsDigit)) _serverPort = _port;
                }
            }
            BuildHostBuilder(_args, _serverPort).Build().Run();
        }

        /// <summary>Host builder discovered by WebApplicationFactory in integration tests.</summary>
        public static IHostBuilder CreateHostBuilder(string[] _args) => BuildHostBuilder(_args, DefaultPort);

        public static IHostBuilder BuildHostBuilder(string[] _args, string _serverPort)
        {
            return Host.CreateDefaultBuilder(_args)
                .ConfigureAppConfiguration((_context, _config) =>
                {
                    // Optional, git-ignored file for machine-specific secrets (connection string, JWT key).
                    _config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                    _config.AddEnvironmentVariables();
                })
                .UseSerilog()
                .ConfigureWebHostDefaults(_webBuilder =>
                {
                    _webBuilder.UseKestrel().UseStartup<Startup>().UseUrls($"http://*:{_serverPort}");
                });
        }
    }
}
