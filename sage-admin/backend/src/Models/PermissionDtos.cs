using System.Collections.Generic;

namespace SageAdminApi.Models
{
    public class PermissionDto
    {
        public long Id { get; set; }
        public long ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        /// <summary>Module.Action, e.g. Employee.View. Derived; never edited.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
    }

    public class ModuleDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
        public int PermissionCount { get; set; }
        /// <summary>Sidebar link (e.g. /products); null = not in the sidebar.</summary>
        public string MenuPath { get; set; }
        /// <summary>Filled by GET permissions/modules (used by the group permission matrix).</summary>
        public List<PermissionDto> Permissions { get; set; } = new List<PermissionDto>();
    }
}
