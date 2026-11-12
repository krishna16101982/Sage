using System;
using System.Collections.Generic;

namespace SageAdminApi.Models
{
    /// <summary>Permission group as listed. Id is the group's PublicId (internal ids are never exposed for groups).</summary>
    public class GroupDto
    {
        public Guid Id { get; set; }
        /// <summary>Immutable once created.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        /// <summary>Active or Inactive. An inactive group grants nothing.</summary>
        public string Status { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public int UserCount { get; set; }
        public int PermissionCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    /// <summary>One group plus its permission matrix: every active permission, and any inactive one the group still holds.</summary>
    public class GroupDetailDto : GroupDto
    {
        public List<GroupMatrixModule> Modules { get; set; } = new List<GroupMatrixModule>();
    }

    public class GroupMatrixModule
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; }
        public bool IsActive { get; set; }
        public List<GroupMatrixPermission> Permissions { get; set; } = new List<GroupMatrixPermission>();
    }

    public class GroupMatrixPermission
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        /// <summary>False when the permission or its module is inactive (it can be kept or removed, not newly granted).</summary>
        public bool IsActive { get; set; }
        public bool Assigned { get; set; }
    }
}
