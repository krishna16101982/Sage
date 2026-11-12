using System.Threading;
using System.Threading.Tasks;
using SageAdminApi.Models;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.AppFunctions.Interfaces
{
    public interface IStatusFunctions
    {
        Task<(ServiceStatus serviceStatus, IFunctionReturn functionReturn)> GetStatusAsync(string _project, string _projectVersion, CancellationToken _ct = default);
    }
}
