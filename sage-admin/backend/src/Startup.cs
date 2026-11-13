using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SageAdminApi.Models;
using SageAdminApi.AppFunctions;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Globals;
using SageCommonLib.AppFunctions;
using SageCommonLib.AppFunctions.Interfaces;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;
using Serilog.Events;

namespace SageAdminApi
{
    public class Startup
    {
        private IWebHostEnvironment _currentEnvironment { get; set; }

        /// <summary>
        /// Startup constructor. Configuration is injected by the host so that User Secrets (Development),
        /// environment variables and appsettings.{Environment}.json are all honoured.
        /// </summary>
        public Startup(IConfiguration __configuration, IWebHostEnvironment __env)
        {
            Configuration = __configuration;
            _currentEnvironment = __env;
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            //Add Seri Log
            AddSeriLog();

            //add cors policy
            services.AddCors(options =>
            {
                AddCors(options);
            });

            services.AddControllers().ConfigureApiBehaviorOptions(_apiOptions =>
            {
                // Model binding failures (malformed JSON, wrong types) use the common error envelope, not ProblemDetails.
                _apiOptions.InvalidModelStateResponseFactory = _actionContext =>
                {
                    IApiResponse _response = new ApiResponse();
                    var _messages = _actionContext.ModelState
                        .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                        .Select(kv => $"Invalid value for '{kv.Key.TrimStart('$', '.')}'.")
                        .DefaultIfEmpty("Invalid request.")
                        .ToList();
                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(_response.Error(Constants.ReturnMessageTypes.ValidationError, _messages, Constants.HttpStatusCodes.BadRequest));
                };
            });

            // Swagger (Development only)
            if (_currentEnvironment.IsDevelopment())
            {
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen(config =>
                {
                    config.SwaggerDoc(Constants.Api.Version, new OpenApiInfo { Title = typeof(Startup).Namespace, Version = Constants.Api.Version });
                    config.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "Authorization Format : Bearer {token}",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
                    });
                });
            }

            // Add Authorization
            services.AddAuthorization(_options =>
            {
                AddPolicies(_options);
            });

            var _jwtAppSettingOptions = Configuration.GetSection(JwtIssuerOptions.SectionName);
            string _securityKey = _jwtAppSettingOptions[nameof(JwtIssuerOptions.SecurityKey)] ?? string.Empty;
            if (_securityKey.Length < 32)
            {
                throw new InvalidOperationException("JwtIssuerOptions:SecurityKey is missing or shorter than 32 characters. Set it via User Secrets or an environment variable.");
            }
            SymmetricSecurityKey _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securityKey));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(o =>
            {
                AuthenticateToken(o, _signingKey, _jwtAppSettingOptions);
            });

            services.AddHsts(options =>
            {
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            // Rate limit the anonymous auth endpoints per client IP (login, refresh, forgot/reset password).
            int _authPermitPerMinute = Configuration.GetValue<int?>(ApplicationConstants.ConfigSettingNames.RateLimitAuthPermit) ?? 30;
            services.AddRateLimiter(_options =>
            {
                _options.RejectionStatusCode = 429;
                _options.AddPolicy(ApplicationConstants.Auth.RateLimitPolicy, _httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        _httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = _authPermitPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                _options.OnRejected = async (_context, _token) =>
                    await WriteEnvelopeAsync(_context.HttpContext, 429, Constants.ReturnMessageTypes.AppError, ApplicationConstants.Auth.RateLimited, Constants.HttpStatusCodes.TooManyRequests);
            });

            AddServices(services);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseMiddleware<SecurityHeadersMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/" + Constants.Api.Version + "/swagger.json", Constants.Api.Version + typeof(Startup).Namespace);
                });
            }
            else
            {
                app.UseHsts();
            }

            app.UseRouting();
            //use cors policy
            app.UseCors(Constants.Api.CorsPolicy);
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void AddServices(IServiceCollection services)
        {
            services.Configure<JwtIssuerOptions>(Configuration.GetSection(JwtIssuerOptions.SectionName));
            services.Configure<DatabaseSettings>(Configuration.GetSection(ApplicationConstants.ConfigSettingNames.DatabaseSettings));
            services.AddScoped<IDatabaseSettings, DatabaseSettings>(_cfg => _cfg.GetRequiredService<IOptionsSnapshot<DatabaseSettings>>().Value);

            services.AddScoped<ILoggerFunctions, LoggerFunctions>();
            services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddScoped<IDatabaseFunctions, DatabaseFunctions>();
            services.AddScoped<IApiResponse, ApiResponse>();

            services.Configure<AuthSettings>(Configuration.GetSection(ApplicationConstants.ConfigSettingNames.AuthSettings));

            services.AddScoped<IStatusFunctions, StatusFunctions>();
            services.AddScoped<ITokenFunctions, TokenFunctions>();
            services.AddScoped<IAuditFunctions, AuditFunctions>();
            services.AddScoped<IPasswordResetNotifier, LogPasswordResetNotifier>();
            services.AddScoped<IAuthFunctions, AuthFunctions>();
            services.AddScoped<IAuthorizationFunctions, AuthorizationFunctions>();
            services.AddScoped<IPermissionFunctions, PermissionFunctions>();
            services.AddScoped<IGroupFunctions, GroupFunctions>();
            services.AddScoped<IProductFunctions, ProductFunctions>();
            services.AddScoped<ISalesUserFunctions, SalesUserFunctions>();
            services.AddScoped<IAdminUserFunctions, AdminUserFunctions>();
            services.AddScoped<ICustomerFunctions, CustomerFunctions>();
            services.AddScoped<ISalesReportFunctions, SalesReportFunctions>();
            services.AddScoped<IAuditLogFunctions, AuditLogFunctions>();
            services.AddScoped<ISettingFunctions, SettingFunctions>();
        }

        /// <summary>
        /// Add Cors. Origins come from configuration (Cors:AllowedOrigins); none configured means no cross-origin access.
        /// </summary>
        private void AddCors(CorsOptions options)
        {
            string[] _origins = Configuration.GetSection(ApplicationConstants.ConfigSettingNames.CorsAllowedOrigins).Get<string[]>() ?? Array.Empty<string>();
            options.AddPolicy(Constants.Api.CorsPolicy, builder =>
            {
                if (_origins.Length > 0)
                {
                    builder.WithOrigins(_origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                }
            });
        }

        /// <summary>
        /// Add Policies. Every endpoint requires an authenticated user unless marked [AllowAnonymous].
        /// </summary>
        private void AddPolicies(AuthorizationOptions options)
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        }

        /// <summary>
        /// Build Jwt Token Parameter
        /// </summary>
        private TokenValidationParameters BuildJwtTokenParameter(SymmetricSecurityKey signingKey, IConfigurationSection jwtAppSettingOptions)
        {
            var _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)],

                ValidateAudience = true,
                ValidAudience = jwtAppSettingOptions[nameof(JwtIssuerOptions.Audience)],

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                RequireExpirationTime = true,
                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
            return _tokenValidationParameters;
        }

        /// <summary>
        /// Authenticate Token. 401 for invalid/missing token, 403 for insufficient permission, both in the common envelope.
        /// </summary>
        private void AuthenticateToken(JwtBearerOptions options, SymmetricSecurityKey signingKey, IConfigurationSection jwtAppSettingOptions)
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = BuildJwtTokenParameter(signingKey, jwtAppSettingOptions);
            options.Events = new JwtBearerEvents()
            {
                // Every authenticated request: the user must still be active and the token's permission version current,
                // so deactivation, password changes and permission changes take effect immediately.
                OnTokenValidated = async _context =>
                {
                    string _sub = _context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    string _pv = _context.Principal?.FindFirst(Constants.TokenValues.PermissionVersion)?.Value;
                    if (!Guid.TryParse(_sub, out Guid _userId) || !int.TryParse(_pv, out int _version))
                    {
                        _context.Fail("Invalid token claims.");
                        return;
                    }
                    IAuthFunctions _authFunctions = _context.HttpContext.RequestServices.GetRequiredService<IAuthFunctions>();
                    if (!await _authFunctions.IsAccessTokenUserValidAsync(_userId, _version, _context.HttpContext.RequestAborted))
                    {
                        _context.Fail("User is no longer active or the token is out of date.");
                    }
                },
                OnChallenge = _context =>
                {
                    _context.HandleResponse();
                    bool _invalidToken = !string.IsNullOrEmpty(_context.Error);
                    string _message = _invalidToken ? Constants.TokenMessages.InvalidToken : Constants.TokenMessages.UnAuthorizedAccess;
                    return WriteEnvelopeAsync(_context.HttpContext, 401, Constants.ReturnMessageTypes.TokenError, _message, Constants.HttpStatusCodes.Unauthorized);
                },
                OnForbidden = _context =>
                {
                    return WriteEnvelopeAsync(_context.HttpContext, 403, Constants.ReturnMessageTypes.PermissionDenied, Constants.GenericMessages.PermissionDenied, Constants.HttpStatusCodes.Forbidden);
                }
            };
        }

        private static Task WriteEnvelopeAsync(Microsoft.AspNetCore.Http.HttpContext _context, int _statusCode, string _messageType, string _message, string _httpStatus)
        {
            IApiResponse _apiResponse = new ApiResponse();
            _context.Response.StatusCode = _statusCode;
            _context.Response.ContentType = Constants.Api.ContentType;
            var _body = _apiResponse.Error(_messageType, new List<string> { _message }, _httpStatus);
            return _context.Response.WriteAsync(JsonSerializer.Serialize(_body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        /// <summary>
        /// Add SeriLog
        /// </summary>
        private void AddSeriLog()
        {
            IConfigurationSection _seriLogconfigurationSection = Configuration.GetSection(ApplicationConstants.SeriLog.ConfigSectionName);
            LogEventLevel _level = Enum.TryParse<LogEventLevel>(_seriLogconfigurationSection[ApplicationConstants.SeriLog.MinimumLevel], out var _parsed) ? _parsed : LogEventLevel.Information;
            Serilog.Log.Logger = SerilogBootstrap.CreateLogger(
                _seriLogconfigurationSection[ApplicationConstants.SeriLog.FilePath] ?? "logs",
                _seriLogconfigurationSection[ApplicationConstants.SeriLog.FileName] ?? "SageAdminApi",
                _level);
        }
    }
}
