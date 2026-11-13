using System;
using System.Threading;
using System.Threading.Tasks;
using SageCommonLib.Models.Interfaces;
using SageUserApi.Models;
using SageUserApi.Models.HttpPost;

namespace SageUserApi.AppFunctions.Interfaces
{
    public interface IAuthFunctions
    {
        Task<(AuthResult authResult, IFunctionReturn functionReturn)> LoginAsync(HttpLoginPostParams _params, string _ipAddress, string _userAgent, CancellationToken _ct = default);
        Task<(AuthResult authResult, IFunctionReturn functionReturn)> RefreshAsync(string _rawRefreshToken, string _ipAddress, string _userAgent, CancellationToken _ct = default);
        Task<IFunctionReturn> LogoutAsync(string _rawRefreshToken, CancellationToken _ct = default);
        Task<IFunctionReturn> ChangePasswordAsync(Guid _userPublicId, HttpChangePasswordPostParams _params, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Emails a one-time reset link to an active sales user. Same answer whether or not the email has an account.</summary>
        Task<IFunctionReturn> ForgotPasswordAsync(string _email, string _ipAddress, CancellationToken _ct = default);
        /// <summary>Sets a new password from the emailed link; every session ends.</summary>
        Task<IFunctionReturn> ResetPasswordAsync(HttpResetPasswordPostParams _params, string _ipAddress, CancellationToken _ct = default);
        Task<(CurrentSalesUserResponse currentUser, IFunctionReturn functionReturn)> GetCurrentUserAsync(Guid _userPublicId, CancellationToken _ct = default);
        /// <summary>Per-request check: the sales user still exists, is active, and the token's permission version is current.</summary>
        Task<bool> IsAccessTokenUserValidAsync(Guid _userPublicId, int _permissionVersion, CancellationToken _ct = default);
    }

    /// <summary>Delivers a reset link (email when configured, otherwise the log in Development). Must not throw for delivery problems.</summary>
    public interface IPasswordResetNotifier
    {
        Task SendAsync(string _email, string _firstName, string _rawToken, DateTime _expiresAtUtc, CancellationToken _ct = default);
    }

    public interface ITokenFunctions
    {
        (string accessToken, int expiresInSeconds) CreateAccessToken(SalesUser _user);
    }
}
