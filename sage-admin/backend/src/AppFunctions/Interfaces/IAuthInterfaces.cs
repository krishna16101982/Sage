using System;
using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.Models;
using SageAdminApi.Models.HttpPost;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.AppFunctions.Interfaces
{
    public interface IAuthFunctions
    {
        Task<(AuthResult authResult, IFunctionReturn functionReturn)> LoginAsync(HttpLoginPostParams _params, string _ipAddress, string _userAgent, CancellationToken _ct = default);
        Task<(AuthResult authResult, IFunctionReturn functionReturn)> RefreshAsync(string _rawRefreshToken, string _ipAddress, string _userAgent, CancellationToken _ct = default);
        Task<IFunctionReturn> LogoutAsync(string _rawRefreshToken, string _ipAddress, CancellationToken _ct = default);
        Task<IFunctionReturn> ForgotPasswordAsync(string _email, string _ipAddress, CancellationToken _ct = default);
        Task<IFunctionReturn> ResetPasswordAsync(HttpResetPasswordPostParams _params, string _ipAddress, CancellationToken _ct = default);
        Task<IFunctionReturn> ChangePasswordAsync(Guid _userPublicId, HttpChangePasswordPostParams _params, string _ipAddress, CancellationToken _ct = default);
        Task<(CurrentUserResponse currentUser, IFunctionReturn functionReturn)> GetCurrentUserAsync(Guid _userPublicId, CancellationToken _ct = default);
        /// <summary>Called for every authenticated request: the user must still be active and the token's permission version current.</summary>
        Task<bool> IsAccessTokenUserValidAsync(Guid _userPublicId, int _permissionVersion, CancellationToken _ct = default);
    }

    public interface ITokenFunctions
    {
        (string accessToken, int expiresInSeconds) CreateAccessToken(AdminUser _user);
    }

    public interface IAuditFunctions
    {
        /// <summary>Append one row to dbo.AuditLogs. oldValues/newValues are serialized to JSON (or null).</summary>
        Task LogAsync(long? _actorUserId, string _action, string _entityType, string _entityId, object _oldValues, object _newValues, string _ipAddress, CancellationToken _ct = default);
    }

    public interface IPasswordResetNotifier
    {
        /// <summary>Deliver the reset token to the user (email in production). Must not throw for delivery problems.</summary>
        Task SendAsync(string _email, string _firstName, string _rawToken, DateTime _expiresAtUtc, CancellationToken _ct = default);
    }
}
