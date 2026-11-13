namespace SageAdminApi.Models.HttpPost
{
    public class HttpModulePostParams
    {
        /// <summary>Immutable once created: permission codes are derived from it.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; }
        public int SortOrder { get; set; }
        /// <summary>Sidebar link such as /inventory; empty = not in the sidebar.</summary>
        public string MenuPath { get; set; }
    }

    public class HttpModulePutParams
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Sidebar link. Not sent (null) = keep the current link; empty string = remove it from the sidebar.</summary>
        public string MenuPath { get; set; }
    }

    public class HttpPermissionPostParams
    {
        public long ModuleId { get; set; }
        /// <summary>View, Add, Edit, Delete, Export, ... The code becomes Module.Action.</summary>
        public string Action { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
    }

    public class HttpPermissionPutParams
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
