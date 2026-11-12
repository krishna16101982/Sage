namespace SageAdminApi.Models
{
    /// <summary>Payload returned by GET api/admin/status.</summary>
    public class ServiceStatus
    {
        public string Status { get; set; } = "ok";
        public string Host { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string ProjectVersion { get; set; } = string.Empty;
        public string PlatformVersion { get; set; } = "N/A";
        public string Database { get; set; } = string.Empty;
    }
}
