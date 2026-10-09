using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    /// <summary>Read-only access to the MR_CFC_* master tables. Soft-deleted / inactive rows are ignored.</summary>
    public sealed class MasterDataRepository : IMasterDataReader
    {
        public RateInfo GetActiveRate(string serviceCode)
        {
            using (var db = new SmcDbContext())
            {
                var r = db.DemandServiceRates.AsNoTracking().Where(x => x.ServiceCode == serviceCode && x.IsActive && !x.IsDeleted).OrderByDescending(x => x.Id).FirstOrDefault();
                return r == null ? null : new RateInfo { ServiceCode = r.ServiceCode, Rate = r.Rate, Unit = r.Unit };
            }
        }

        public ServiceCatalog GetCatalog()
        {
            using (var db = new SmcDbContext())
            {
                var services = db.DemandServices.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).Select(x => x.Code).ToList();
                var types = db.DemandBusinessTypes.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).Select(x => x.Code).ToList();
                return new ServiceCatalog(new HashSet<string>(services), new HashSet<string>(types));
            }
        }

        private const string ServicesCacheKey = "SMC.BhoomiMalmatta.ActiveServices.v1";
        private static Dictionary<string, ServiceItem> LoadServiceMap()
        {
            using (var db = new SmcDbContext())
                return db.DemandServices.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.SortOrder)
                    .Select(x => new ServiceItem { Value = x.Value, Code = x.Code, Name = x.Name, NameMr = x.NameMr, SortOrder = x.SortOrder }).ToList()
                    .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        }
        public static Dictionary<string, ServiceItem> GetServiceMap()
        { return ServiceMasterCache.GetOrLoad(ServicesCacheKey, LoadServiceMap); }
        public static string GetServiceName(string code)
        {
            ServiceItem item;
            return !string.IsNullOrWhiteSpace(code) && GetServiceMap().TryGetValue(code, out item) ? item.NameMr : code;
        }

        private const string BusinessTypesCacheKey = "SMC.BhoomiMalmatta.ActiveBusinessTypes.v1";
        private static Dictionary<string, BusinessTypeItem> LoadBusinessTypeMap()
        {
            using (var db = new SmcDbContext())
                return db.DemandBusinessTypes.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.SortOrder)
                    .Select(x => new BusinessTypeItem { Value = x.Value, Code = x.Code, Name = x.Name, NameMr = x.NameMr }).ToList()
                    .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        }
        public static Dictionary<string, BusinessTypeItem> GetBusinessTypeMap()
        { return ServiceMasterCache.GetOrLoad(BusinessTypesCacheKey, LoadBusinessTypeMap); }
        public static string GetBusinessTypeName(string code)
        {
            BusinessTypeItem item;
            return !string.IsNullOrWhiteSpace(code) && GetBusinessTypeMap().TryGetValue(code, out item)
                ? (string.IsNullOrWhiteSpace(item.NameMr) ? item.Name : item.NameMr) : code;
        }
        public List<ServiceItem> GetServices()
        { return GetServiceMap().Values.OrderBy(x => x.SortOrder).ToList(); }

        public List<RateItem> GetRates()
        {
            using (var db = new SmcDbContext())
                return db.DemandServiceRates.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.ServiceCode)
                    .Select(x => new RateItem { ServiceCode = x.ServiceCode, Rate = x.Rate, Unit = x.Unit }).ToList();
        }

        public List<BusinessTypeItem> GetBusinessTypes()
        {
            using (var db = new SmcDbContext())
                return db.DemandBusinessTypes.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.SortOrder)
                    .Select(x => new BusinessTypeItem { Value = x.Value, Code = x.Code, Name = x.Name, NameMr = x.NameMr }).ToList();
        }

        public NumberConfigItem GetNumberConfig()
        {
            using (var db = new SmcDbContext())
            {
                var c = db.ApplicationNumberConfigurations.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.Id).FirstOrDefault();
                return c == null ? null : new NumberConfigItem { Prefix = c.Prefix, YearStrategy = c.YearStrategy, SequenceLength = c.SequenceLength, NextNumber = c.NextNumber };
            }
        }

        public List<SmsTemplateItem> GetSmsTemplates()
        {
            using (var db = new SmcDbContext())
                return db.SmsTemplates.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.EventType)
                    .Select(x => new SmsTemplateItem { EventType = x.EventType, TemplateName = x.TemplateName, Language = x.Language, ApprovalStatus = x.ApprovalStatus, IsActive = x.IsActive }).ToList();
        }

        /// <summary>Only an Approved, active, non-deleted template is ever used; the highest version wins.</summary>
        public SmsTemplateRecord GetSmsTemplate(string eventType, string language)
        {
            using (var db = new SmcDbContext())
            {
                var t = db.SmsTemplates.AsNoTracking().Where(x => x.EventType == eventType && x.Language == language && x.IsActive && !x.IsDeleted && x.ApprovalStatus == "Approved")
                    .OrderByDescending(x => x.Version).FirstOrDefault();
                return t == null ? null : new SmsTemplateRecord { EventType = t.EventType, MessageBody = t.MessageBody, DltTemplateId = t.DltTemplateId, SenderId = t.SenderId };
            }
        }

        public PermitConfigRecord GetPermitConfig()
        {
            using (var db = new SmcDbContext())
            {
                var c = db.PermitConfigurations.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderByDescending(x => x.Id).FirstOrDefault();
                return c == null ? new PermitConfigRecord { OrganizationName = "सोलापूर महानगरपालिका", CertificateTitle = "परवानगी पत्र" }
                    : new PermitConfigRecord { OrganizationName = c.OrganizationName, DepartmentName = c.DepartmentName, CertificateTitle = c.CertificateTitle, AuthorityDesignation = c.AuthorityDesignation, FooterText = c.FooterText };
            }
        }

        public List<string> GetPermitTerms()
        {
            using (var db = new SmcDbContext())
                return db.PermitTerms.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.DisplayOrder).Select(x => x.Text).ToList();
        }
    }
}
