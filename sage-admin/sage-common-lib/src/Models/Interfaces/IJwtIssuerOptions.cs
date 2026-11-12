namespace SageCommonLib.Models.Interfaces
{
    public interface IJwtIssuerOptions
    {
        string Issuer { get; set; }
        string Audience { get; set; }
        string SecurityKey { get; set; }
        int AccessTokenMinutes { get; set; }
        int RefreshTokenDays { get; set; }
    }
}
