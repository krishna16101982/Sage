using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageUserApi.AppFunctions.Interfaces;
using SageUserApi.Models;

namespace SageUserApi.AppFunctions
{
    public class TokenFunctions : ITokenFunctions
    {
        private readonly JwtIssuerOptions _jwtIssuerOptions;

        public TokenFunctions(IOptions<JwtIssuerOptions> __jwtIssuerOptions)
        {
            _jwtIssuerOptions = __jwtIssuerOptions.Value;
        }

        /// <summary>
        /// Short-lived access token for the User Panel. Signed with the User API's own key and issuer/audience,
        /// so an Admin API token is never accepted here (and the reverse).
        /// </summary>
        public (string accessToken, int expiresInSeconds) CreateAccessToken(SalesUser _user)
        {
            DateTime _now = DateTime.UtcNow;
            DateTime _expires = _now.AddMinutes(_jwtIssuerOptions.AccessTokenMinutes);

            var _claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, _user.PublicId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, CommonFunctions.ToUnixEpochDate(_now).ToString(), ClaimValueTypes.Integer64),
                new Claim("uname", _user.Username),
                new Claim("role", _user.Role),
                new Claim(Constants.TokenValues.PermissionVersion, _user.PermissionVersion.ToString())
            };

            var _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtIssuerOptions.SecurityKey));
            var _jwt = new JwtSecurityToken(_jwtIssuerOptions.Issuer, _jwtIssuerOptions.Audience, _claims, _now, _expires,
                new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
            return (new JwtSecurityTokenHandler().WriteToken(_jwt), (int)(_expires - _now).TotalSeconds);
        }
    }
}
