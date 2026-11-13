using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using SageUserApi.AppFunctions;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Globals;
using SageUserApi.Models;
using Serilog.Events;

namespace SageUserApi
{
    /// <summary>User Panel API. Same building blocks as the Admin API (common lib, envelope, JWT checks), its own identity.</summary>
    public class Startup
    {
        private readonly IWebHostEnvironment _environment;

        public Startup(IConfiguration __configuration, IWebHostEnvironment __env)
        {
            Configuration = __configuration;
            _environment = __env;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            AddSeriLog();

            string[] _origins = Configuration.GetSection(ApplicationConstants.ConfigSettingNames.CorsAllowedOrigins).Get<string[]>() ?? Array.Empty<string>();
            services.AddCors(o => o.AddPolicy(Constants.Api.CorsPolicy, b =>
            {
                if (_origins.Length > 0) b.WithOrigins(_origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }));

            services.AddControllers().ConfigureApiBehaviorOptions(_apiOptions =>
            {
                // Model binding failures use the common error envelope, not ProblemDetails.
                _apiOptions.InvalidModelStateResponseFactory = _actionContext =>
                {
                    var _messages = _actionContext.ModelState.Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                        .Select(kv => $"Invalid value for '{kv.Key.TrimStart('$', '.')}'.").DefaultIfEmpty("Invalid request.").ToList();
                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ApiResponse().Error(Constants.ReturnMessageTypes.ValidationError, _messages, Constants.HttpStatusCodes.BadRequest));
                };
            });

            if (_environment.IsDevelopment())
            {
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc(Constants.Api.Version, new OpenApiInfo { Title = typeof(Startup).Namespace, Version = Constants.Api.Version });
                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
                });
            }

            // Every endpoint requires a signed-in sales user unless marked [AllowAnonymous].
            services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build());

            IConfigurationSection _jwt = Configuration.GetSection(JwtIssuerOptions.SectionName);
            string _securityKey = _jwt[nameof(JwtIssuerOptions.SecurityKey)] ?? string.Empty;
            if (_securityKey.Length < 32)
                throw new InvalidOperationException("JwtIssuerOptions:SecurityKey is missing or shorter than 32 characters. Set it via appsettings.Local.json or an environment variable (use a different key from the Admin API).");
            var _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securityKey));

            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(o => ConfigureJwt(o, _signingKey, _jwt));

            services.AddHsts(o => { o.IncludeSubDomains = true; o.MaxAge = TimeSpan.FromDays(365); });

            // Rate limit the anonymous sign-in endpoints per client IP.
            int _permit = Configuration.GetValue<int?>(ApplicationConstants.ConfigSettingNames.RateLimitAuthPermit) ?? 30;
            services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = 429;
                o.AddPolicy(ApplicationConstants.Auth.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = _permit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                o.OnRejected = (ctx, _) => new ValueTask(WriteEnvelopeAsync(ctx.HttpContext, 429, Constants.ReturnMessageTypes.AppError, ApplicationConstants.Auth.RateLimited, Constants.HttpStatusCodes.TooManyRequests));
            });

            services.Configure<JwtIssuerOptions>(_jwt);
            services.Configure<DatabaseSettings>(Configuration.GetSection(ApplicationConstants.ConfigSettingNames.DatabaseSettings));
            services.Configure<AuthSettings>(Configuration.GetSection(ApplicationConstants.ConfigSettingNames.AuthSettings));
            services.AddScoped<IDatabaseSettings, DatabaseSettings>(sp => sp.GetRequiredService<IOptionsSnapshot<DatabaseSettings>>().Value);
            services.AddScoped<ILoggerFunctions, LoggerFunctions>();
            services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddScoped<IDatabaseFunctions, DatabaseFunctions>();
            services.AddScoped<IApiResponse, ApiResponse>();
            services.AddScoped<ITokenFunctions, TokenFunctions>();
            services.AddScoped<IAuthFunctions, AuthFunctions>();
            services.AddScoped<ISalesUserContextFunctions, SalesUserContextFunctions>();
            services.AddScoped<ICatalogFunctions, CatalogFunctions>();
            services.AddScoped<ICustomerFunctions, CustomerFunctions>();
            services.AddScoped<ISaleFunctions, SaleFunctions>();
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseMiddleware<SecurityHeadersMiddleware>();
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/" + Constants.Api.Version + "/swagger.json", Constants.Api.Version + typeof(Startup).Namespace));
            }
            else
            {
                app.UseHsts();
            }
            app.UseRouting();
            app.UseCors(Constants.Api.CorsPolicy);
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(e => e.MapControllers());
        }

        /// <summary>401 for a missing/invalid token or an inactive/out-of-date user, in the common envelope.</summary>
        private static void ConfigureJwt(JwtBearerOptions options, SymmetricSecurityKey signingKey, IConfigurationSection jwt)
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt[nameof(JwtIssuerOptions.Issuer)],
                ValidateAudience = true,
                ValidAudience = jwt[nameof(JwtIssuerOptions.Audience)],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.Events = new JwtBearerEvents
            {
                // Every request: the sales user must still be active and the token's version current, so deactivation,
                // password changes and role/manager changes take effect immediately.
                OnTokenValidated = async ctx =>
                {
                    string _sub = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    string _pv = ctx.Principal?.FindFirst(Constants.TokenValues.PermissionVersion)?.Value;
                    if (!Guid.TryParse(_sub, out Guid _userId) || !int.TryParse(_pv, out int _version))
                    {
                        ctx.Fail("Invalid token claims.");
                        return;
                    }
                    var _auth = ctx.HttpContext.RequestServices.GetRequiredService<IAuthFunctions>();
                    if (!await _auth.IsAccessTokenUserValidAsync(_userId, _version, ctx.HttpContext.RequestAborted))
                        ctx.Fail("User is no longer active or the token is out of date.");
                },
                OnChallenge = ctx =>
                {
                    ctx.HandleResponse();
                    string _message = string.IsNullOrEmpty(ctx.Error) ? Constants.TokenMessages.UnAuthorizedAccess : Constants.TokenMessages.InvalidToken;
                    return WriteEnvelopeAsync(ctx.HttpContext, 401, Constants.ReturnMessageTypes.TokenError, _message, Constants.HttpStatusCodes.Unauthorized);
                },
                OnForbidden = ctx => WriteEnvelopeAsync(ctx.HttpContext, 403, Constants.ReturnMessageTypes.PermissionDenied, Constants.GenericMessages.PermissionDenied, Constants.HttpStatusCodes.Forbidden)
            };
        }

        private static Task WriteEnvelopeAsync(HttpContext context, int statusCode, string messageType, string message, string httpStatus)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = Constants.Api.ContentType;
            var _body = new ApiResponse().Error(messageType, new List<string> { message }, httpStatus);
            return context.Response.WriteAsync(JsonSerializer.Serialize(_body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        private void AddSeriLog()
        {
            IConfigurationSection _section = Configuration.GetSection(ApplicationConstants.SeriLog.ConfigSectionName);
            LogEventLevel _level = Enum.TryParse(_section[ApplicationConstants.SeriLog.MinimumLevel], out LogEventLevel _parsed) ? _parsed : LogEventLevel.Information;
            Serilog.Log.Logger = SerilogBootstrap.CreateLogger(_section[ApplicationConstants.SeriLog.FilePath] ?? "logs", _section[ApplicationConstants.SeriLog.FileName] ?? "SageUserApi", _level);
        }
    }
}
