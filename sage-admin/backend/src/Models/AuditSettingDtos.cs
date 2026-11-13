using System;
using System.Collections.Generic;
using System.Text.Json;
using SageCommonLib.Models.Paging;

namespace SageAdminApi.Models
{
    /// <summary>One row of the audit log list.</summary>
    public class AuditLogItem
    {
        public long Id { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>admin (Admin Panel user), sales (User Panel user) or system (no signed-in user, e.g. a failed sign-in).</summary>
        public string ActorType { get; set; } = string.Empty;
        public string ActorName { get; set; }
        public string ActorUsername { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; }
        public string IpAddress { get; set; }
        public bool HasOldValues { get; set; }
        public bool HasNewValues { get; set; }
    }

    /// <summary>One audit entry with the values before and after the change (JSON objects as recorded).</summary>
    public class AuditLogDetail : AuditLogItem
    {
        public JsonElement? OldValues { get; set; }
        public JsonElement? NewValues { get; set; }
    }

    /// <summary>Values for the audit log filters.</summary>
    public class AuditLogOptions
    {
        public List<string> EntityTypes { get; set; } = new List<string>();
        public List<string> Actions { get; set; } = new List<string>();
        /// <summary>Admin users who appear in the log.</summary>
        public List<AuditActorOption> Admins { get; set; } = new List<AuditActorOption>();
    }

    public class AuditActorOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A setting as the Settings screen shows it, with the limits used to check a new value.</summary>
    public class SettingDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; }
        /// <summary>string, int, bool or json.</summary>
        public string ValueType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; }
        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }
        public int? MaxLength { get; set; }
        public string Pattern { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string UpdatedByName { get; set; }
    }
}

namespace SageAdminApi.Models.Filter
{
    /// <summary>Query string of GET api/admin/audit-logs (sortDirection asc|desc; newest first by default).</summary>
    public class AuditLogFilter : HttpPaging
    {
        public string Search { get; set; }
        public string EntityType { get; set; }
        public string Action { get; set; }
        /// <summary>admin, sales or system.</summary>
        public string ActorType { get; set; }
        /// <summary>One admin user's actions.</summary>
        public Guid? ActorId { get; set; }
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
    }
}

namespace SageAdminApi.Models.HttpPost
{
    /// <summary>Body of PUT api/admin/settings/{key}.</summary>
    public class HttpSettingPutParams
    {
        public string Value { get; set; }
    }
}
