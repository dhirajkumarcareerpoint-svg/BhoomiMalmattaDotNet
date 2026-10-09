using System;
using System.Security.Principal;
using System.Text;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Security
{
    /// <summary>Identity rebuilt on every request from the encrypted Forms-authentication ticket (never from anything the browser can edit).</summary>
    public sealed class SmcIdentity : IIdentity
    {
        public string Name { get; set; }
        public string AuthenticationType { get { return "SMC.Forms"; } }
        public bool IsAuthenticated { get { return true; } }
        public bool IsCitizen { get { return Role == Roles.Citizen; } }
        public string Role { get; set; }
        public int UserId { get; set; }
        public int? ZoneNumber { get; set; }
        public string FullName { get; set; }
        public int ApplicationId { get; set; }
        public string ApplicationNumber { get; set; }

        /// <summary>userData layout: "E|id|role|zone|base64(fullName)" for employees, "C|applicationId|applicationNumber" for citizens.</summary>
        public static SmcIdentity Parse(string name, string userData)
        {
            if (string.IsNullOrEmpty(userData)) return null;
            var p = userData.Split('|');
            try
            {
                if (p[0] == "E" && p.Length == 5)
                    return new SmcIdentity
                    {
                        Name = name, UserId = int.Parse(p[1]), Role = p[2], ZoneNumber = p[3].Length == 0 ? (int?)null : int.Parse(p[3]),
                        FullName = Encoding.UTF8.GetString(Convert.FromBase64String(p[4]))
                    };
                if (p[0] == "C" && p.Length == 3)
                    return new SmcIdentity { Name = name, Role = Roles.Citizen, ApplicationId = int.Parse(p[1]), ApplicationNumber = p[2], FullName = "अर्जदार" };
            }
            catch (FormatException) { }
            return null;
        }

        public static string EmployeeData(int id, string role, int? zone, string fullName)
        { return "E|" + id + "|" + role + "|" + (zone.HasValue ? zone.Value.ToString() : "") + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(fullName ?? "")); }
        public static string CitizenData(int applicationId, string applicationNumber) { return "C|" + applicationId + "|" + applicationNumber; }
    }

    public sealed class SmcPrincipal : IPrincipal
    {
        private readonly SmcIdentity _identity;
        public SmcPrincipal(SmcIdentity identity) { _identity = identity; }
        public SmcIdentity Me { get { return _identity; } }
        IIdentity IPrincipal.Identity { get { return _identity; } }
        public bool IsInRole(string role) { return string.Equals(_identity.Role, role, StringComparison.Ordinal); }
    }
}
