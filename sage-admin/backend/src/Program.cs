using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SageAdminApi
{
    public class Program
    {
        private const string DefaultPort = "5080";

        /// <summary>
        /// Entry point. Optional argument: --port=NNNNN (same convention as ixcheck APIs, used by run.sh / Docker).
        /// </summary>
        public static void Main(string[] _args)
        {
            string _serverPort = DefaultPort;

            if (_args.Count() > 0)
            {
                string[] _splitFirstArg = _args[0].Split('=');
                if (_splitFirstArg.Count() == 2 && _splitFirstArg[0].ToLower().Trim() == "--port")
                {
                    string _portNumber = _splitFirstArg[1].ToLower().Trim();
                    if (_portNumber.Length > 0 && _portNumber.Length < 6 && _portNumber.All(char.IsDigit))
                    {
                        _serverPort = _portNumber;
                    }
                }
            }

            BuildHostBuilder(_args, _serverPort).Build().Run();
        }

        /// <summary>
        /// Host builder discovered by WebApplicationFactory in integration tests.
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] _args) => BuildHostBuilder(_args, DefaultPort);

        public static IHostBuilder BuildHostBuilder(string[] _args, string _serverPort)
        {
            return Host.CreateDefaultBuilder(_args)
                .ConfigureAppConfiguration((_context, _config) =>
                {
                    // Optional, git-ignored file for machine-specific secrets (connection string, JWT key).
                    // Loaded after appsettings.*.json and User Secrets; environment variables still override it.
                    _config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                    _config.AddEnvironmentVariables();
                })
                .UseSerilog()
                .ConfigureWebHostDefaults(_webBuilder =>
                {
                    _webBuilder
                        .UseKestrel()
                        .UseStartup<Startup>()
                        .UseUrls($"http://*:{_serverPort}");
                });
        }
    }
}
