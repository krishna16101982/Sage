using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SageAdminApi.AppFunctions.Interfaces;
using SageAdminApi.Models;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;

namespace SageAdminApi.AppFunctions
{
    public class TokenFunctions : ITokenFunctions
    {
        private readonly JwtIssuerOptions _jwtIssuerOptions;

        public TokenFunctions(IOptions<JwtIssuerOptions> __jwtIssuerOptions)
        {
            _jwtIssuerOptions = __jwtIssuerOptions.Value;
        }

        /// <summary>
        /// Create a short-lived access token. It carries identity and the permission version only:
        /// permissions are looked up server-side so changes apply without waiting for token expiry.
        /// </summary>
        public (string accessToken, int expiresInSeconds) CreateAccessToken(AdminUser _user)
        {
            DateTime _now = DateTime.UtcNow;
            DateTime _expires = _now.AddMinutes(_jwtIssuerOptions.AccessTokenMinutes);

            var _claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, _user.PublicId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, CommonFunctions.ToUnixEpochDate(_now).ToString(), ClaimValueTypes.Integer64),
                new Claim("uname", _user.Username),
                new Claim("sa", _user.IsSuperAdmin ? "1" : "0"),
                new Claim(Constants.TokenValues.PermissionVersion, _user.PermissionVersion.ToString())
            };

            var _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtIssuerOptions.SecurityKey));
            var _jwt = new JwtSecurityToken(
                issuer: _jwtIssuerOptions.Issuer,
                audience: _jwtIssuerOptions.Audience,
                claims: _claims,
                notBefore: _now,
                expires: _expires,
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(_jwt), (int)(_expires - _now).TotalSeconds);
        }
    }
}
