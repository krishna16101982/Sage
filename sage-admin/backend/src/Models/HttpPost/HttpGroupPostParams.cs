using System.Collections.Generic;

namespace SageAdminApi.Models.HttpPost
{
    public class HttpGroupPostParams
    {
        /// <summary>Immutable once created.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
    }

    public class HttpGroupPutParams
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        /// <summary>Active or Inactive.</summary>
        public string Status { get; set; } = "Active";
    }

    /// <summary>The complete permission set the group should hold afterwards (replaces the current set).</summary>
    public class HttpGroupPermissionsPutParams
    {
        public List<long> PermissionIds { get; set; } = new List<long>();
    }
}
