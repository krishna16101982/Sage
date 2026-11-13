namespace SageAdminApi.Models.HttpPost
{
    /// <summary>Body of POST api/admin/customers and PUT api/admin/customers/{id}.</summary>
    public class HttpCustomerPostParams
    {
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Gstin { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
    }
}
