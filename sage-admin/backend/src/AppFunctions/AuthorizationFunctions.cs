using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SageAdminApi.AppFunctions.Interfaces;
using SageCommonLib.AppFunctions.Interfaces;

namespace SageAdminApi.AppFunctions
{
    public class AuthorizationFunctions : IAuthorizationFunctions
    {
        private readonly IDatabaseFunctions _databaseFunctions;

        public AuthorizationFunctions(IDatabaseFunctions __databaseFunctions)
        {
            _databaseFunctions = __databaseFunctions;
        }

        /// <summary>
        /// Uses dbo.fn_UserHasPermission so the rule (active user, active group/permission/module, Super Admin bypass)
        /// lives in one place in SQL. Nothing is trusted from the token except the user's identity.
        /// </summary>
        public async Task<bool> HasPermissionAsync(Guid _userPublicId, string _permissionCode, CancellationToken _ct = default)
        {
            const string _sql = "SELECT dbo.fn_UserHasPermission(Id, @code) FROM dbo.AdminUsers WHERE PublicId = @p AND IsDeleted = 0";
            return await _databaseFunctions.ExecuteScalarAsync<bool>(_sql, new[]
            {
                new SqlParameter("@code", SqlDbType.VarChar, 101) { Value = _permissionCode },
                new SqlParameter("@p", _userPublicId)
            }, _ct);
        }

        public async Task<long?> GetUserIdAsync(Guid _userPublicId, CancellationToken _ct = default)
        {
            long _id = await _databaseFunctions.ExecuteScalarAsync<long>("SELECT Id FROM dbo.AdminUsers WHERE PublicId = @p AND IsDeleted = 0", new[] { new SqlParameter("@p", _userPublicId) }, _ct);
            return _id == 0 ? (long?)null : _id;
        }
    }
}
