using SageCommonLib.Models.Interfaces;

namespace SageCommonLib.Models
{
    /// <summary>Bound from "JwtIssuerOptions". SecurityKey must come from secrets, minimum 32 chars.</summary>
    public class JwtIssuerOptions : IJwtIssuerOptions
    {
        public const string SectionName = "JwtIssuerOptions";
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SecurityKey { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 7;
    }
}
