using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>Resolves display-only Marathi service and business-type names from the active, five-minute cached masters.</summary>
    public sealed class ServiceDisplayValue
    {
        public string ServiceName { get; set; }
        public string SubServiceName { get; set; }
    }

    public static class ServiceDisplay
    {
        public static ServiceDisplayValue Get(string serviceCode, string businessTypeCode, string otherText)
        {
            IDictionary<string, ServiceItem> services;
            IDictionary<string, BusinessTypeItem> types;
            try { services = MasterDataRepository.GetServiceMap(); }
            catch (InvalidOperationException) { services = new Dictionary<string, ServiceItem>(StringComparer.OrdinalIgnoreCase); }
            try { types = string.IsNullOrWhiteSpace(businessTypeCode) ? new Dictionary<string, BusinessTypeItem>(StringComparer.OrdinalIgnoreCase) : MasterDataRepository.GetBusinessTypeMap(); }
            catch (InvalidOperationException) { types = new Dictionary<string, BusinessTypeItem>(StringComparer.OrdinalIgnoreCase); }
            return Resolve(serviceCode, businessTypeCode, otherText, services, types);
        }

        public static ServiceDisplayValue Resolve(string serviceCode, string businessTypeCode, string otherText,
            IDictionary<string, ServiceItem> services, IDictionary<string, BusinessTypeItem> types)
        {
            services = services ?? new Dictionary<string, ServiceItem>(StringComparer.OrdinalIgnoreCase);
            types = types ?? new Dictionary<string, BusinessTypeItem>(StringComparer.OrdinalIgnoreCase);
            ServiceItem service;
            var serviceName = !string.IsNullOrWhiteSpace(serviceCode) && services.TryGetValue(serviceCode, out service)
                ? Preferred(service.NameMr, service.Name, serviceCode) : serviceCode;

            string subService = null;
            if (!string.IsNullOrWhiteSpace(businessTypeCode))
            {
                BusinessTypeItem businessType;
                if (types.TryGetValue(businessTypeCode, out businessType))
                {
                    if (string.Equals(businessTypeCode, "Other", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(otherText))
                        subService = otherText.Trim();
                    else
                        subService = Preferred(businessType.NameMr, businessType.Name, businessTypeCode);
                }
                else subService = string.Equals(businessTypeCode, "Other", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(otherText)
                    ? otherText.Trim() : businessTypeCode;
            }
            return new ServiceDisplayValue { ServiceName = serviceName, SubServiceName = subService };
        }

        private static string Preferred(string marathi, string name, string code)
        {
            return !string.IsNullOrWhiteSpace(marathi) ? marathi : !string.IsNullOrWhiteSpace(name) ? name : code;
        }
    }

    public static class ApplicantStatusViewMapper
    {
        public static CitizenStatusView Map(ApplicationDetail detail)
        {
            if (detail == null) throw new ArgumentNullException("detail");
            return Apply(detail, ServiceDisplay.Get(detail.ServiceType, detail.BusinessType, detail.OtherBusinessType));
        }

        public static CitizenStatusView Map(ApplicationDetail detail, IDictionary<string, ServiceItem> services, IDictionary<string, BusinessTypeItem> types)
        {
            if (detail == null) throw new ArgumentNullException("detail");
            return Apply(detail, ServiceDisplay.Resolve(detail.ServiceType, detail.BusinessType, detail.OtherBusinessType, services, types));
        }

        private static CitizenStatusView Apply(ApplicationDetail detail, ServiceDisplayValue display)
        {
            var view = CitizenStatusMapper.MapDetail(detail);
            view.ServiceName = string.Equals(display.ServiceName, detail.ServiceType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(detail.ServiceName)
                ? detail.ServiceName : display.ServiceName;
            view.SubServiceName = display.SubServiceName;
            view.BusinessType = display.SubServiceName;
            return view;
        }
    }

    public static class ApplicantQrAvailability
    {
        public static bool IsAvailable(string configuredPath, string applicationRoot, Func<string, bool> exists)
        {
            var resolved = ApplicantQrPath.Resolve(configuredPath, applicationRoot, exists);
            var path = resolved == null ? null : resolved.Value;
            if (resolved != null && resolved.IsVirtual) path = Path.Combine(applicationRoot, path.Substring(2).Replace('/', Path.DirectorySeparatorChar));
            var extension = path == null ? string.Empty : Path.GetExtension(path).ToLowerInvariant();
            var available = resolved != null && PaymentQrImageType.IsAllowedExtension(extension) && exists(path) && PaymentQrImageType.IsSupportedFile(path);
            if (!available)
            {
                var reason = string.IsNullOrWhiteSpace(configuredPath) ? "Payment.QrImagePath is blank." : path == null || !exists(path) ? "Configured payment QR file is missing or path is invalid." : "Configured payment QR file must contain PNG or JPEG data and use a supported extension.";
                LogMissingOnce(reason);
            }
            return available;
        }

        public static string DiagnosticStatus(string configuredPath, string applicationRoot, Func<string, bool> exists)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return "not configured";
            var resolved = ApplicantQrPath.Resolve(configuredPath, applicationRoot, exists);
            var path = resolved == null ? null : resolved.Value;
            if (resolved != null && resolved.IsVirtual) path = Path.Combine(applicationRoot, path.Substring(2).Replace('/', Path.DirectorySeparatorChar));
            return path != null && PaymentQrImageType.IsAllowedExtension(Path.GetExtension(path)) && exists(path) && PaymentQrImageType.IsSupportedFile(path)
                ? "configured+file found" : "configured+file missing";
        }

        private static void LogMissingOnce(string reason)
        {
            const string key = "SMC.PaymentQr.Missing.LastLoggedUtc";
            if (HttpRuntime.Cache.Get(key) != null) return;
            lock (typeof(ApplicantQrAvailability))
            {
                if (HttpRuntime.Cache.Get(key) != null) return;
                HttpRuntime.Cache.Insert(key, DateTime.UtcNow, null, DateTime.Now.AddHours(1), System.Web.Caching.Cache.NoSlidingExpiration);
            }
            ErrorLogger.Log(Guid.NewGuid().ToString("N"), "PaymentQrUnavailable", new FileNotFoundException(reason));
        }
    }

    public static class PaymentQrImageType
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static bool IsAllowedExtension(string extension)
        {
            extension = (extension ?? string.Empty).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        public static string Detect(string extension, Stream input)
        {
            if (!IsAllowedExtension(extension) || input == null || !input.CanRead) return null;
            var position = input.CanSeek ? input.Position : 0;
            var header = new byte[8];
            var read = input.Read(header, 0, header.Length);
            if (input.CanSeek) input.Position = position;
            if (read == 8 && PngSignature.SequenceEqual(header)) return "image/png";
            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return "image/jpeg";
            return null;
        }

        public static bool IsSupportedFile(string path)
        {
            try { using (var input = File.OpenRead(path)) return Detect(Path.GetExtension(path), input) != null; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    public sealed class PaymentQrAssetResponse
    {
        public int StatusCode { get; set; }
        public string ContentType { get; set; }
        public string FileExtension { get; set; }
        public byte[] Content { get; set; }
    }

    public static class PaymentQrAsset
    {
        public static PaymentQrAssetResponse Load(string configuredPath, string applicationRoot, Func<string, bool> exists, Func<string, Stream> open)
        {
            var missing = new PaymentQrAssetResponse { StatusCode = 404 };
            if (open == null) return missing;
            var resolved = ApplicantQrPath.Resolve(configuredPath, applicationRoot, exists);
            var path = resolved == null ? null : resolved.Value;
            if (resolved != null && resolved.IsVirtual) path = Path.Combine(applicationRoot, path.Substring(2).Replace('/', Path.DirectorySeparatorChar));
            if (path == null || !exists(path)) return missing;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                using (var input = open(path))
                {
                    var mime = PaymentQrImageType.Detect(extension, input);
                    if (mime == null) return missing;
                    using (var output = new MemoryStream())
                    {
                        input.CopyTo(output);
                        return new PaymentQrAssetResponse { StatusCode = 200, ContentType = mime, FileExtension = extension, Content = output.ToArray() };
                    }
                }
            }
            catch (IOException) { return missing; }
            catch (UnauthorizedAccessException) { return missing; }
        }
    }

    public static class ApplicantDateDisplay
    {
        public static string Digits(string value)
        {
            if (!ApplicantDateFormatter.UseDevanagariDigits || string.IsNullOrEmpty(value)) return value;
            const string latin = "0123456789";
            const string devanagari = "\u0966\u0967\u0968\u0969\u096A\u096B\u096C\u096D\u096E\u096F";
            for (var i = 0; i < latin.Length; i++) value = value.Replace(latin[i], devanagari[i]);
            return value;
        }
    }
}
