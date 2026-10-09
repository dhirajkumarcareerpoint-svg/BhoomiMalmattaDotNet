using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Hosting;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>
    /// PDF generation with QuestPDF. Marathi (Devanagari) uses the bundled Noto Sans Devanagari font (SIL OFL, App_Data\Fonts), shaped by HarfBuzz
    /// inside QuestPDF, so conjuncts render correctly. LICENSE NOTE: QuestPDF is free under its Community licence only for eligible organisations;
    /// Solapur Municipal Corporation must confirm eligibility or buy a licence (see SECURITY.md).
    /// </summary>
    public sealed class PdfGenerator : IPdfGenerator
    {
        private const string LatinFont = "Noto Sans";
        private const string MarathiFont = "Noto Sans Devanagari";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object Gate = new object();
        private static bool _ready;

        private static void Init()
        {
            lock (Gate)
            {
                if (_ready) return;

                var fontDirectory = FindFontDirectory();
                Register(LatinFont, Path.Combine(fontDirectory, "NotoSans-Variable.ttf"));
                foreach (var f in new[] { "NotoSansDevanagari-Regular.ttf", "NotoSansDevanagari-Bold.ttf" })
                    Register(MarathiFont, Path.Combine(fontDirectory, f));
                _ready = true;
            }
        }

        private static string FindFontDirectory()
        {
            var candidates = new List<string>();
            var mapped = HostingEnvironment.MapPath("~/App_Data/Fonts");
            if (!string.IsNullOrWhiteSpace(mapped)) candidates.Add(mapped);
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(basePath, "App_Data", "Fonts"));
            candidates.Add(Path.Combine(basePath, "..", "App_Data", "Fonts"));
            foreach (var path in candidates.Select(Path.GetFullPath))
                if (File.Exists(Path.Combine(path, "NotoSans-Variable.ttf")) && File.Exists(Path.Combine(path, "NotoSansDevanagari-Regular.ttf"))) return path;
            throw new FileNotFoundException("The bundled Noto Sans PDF fonts were not found under App_Data/Fonts.");
        }

        private static void Register(string family, string path)
        {
            using (var stream = File.OpenRead(path)) FontManager.RegisterFontWithCustomName(family, stream);
        }

        private static string Ref(ApplicationDetail d, string kind)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(d.ApplicationNumber + "|" + kind + "|" + d.Status))).Replace("-", "").Substring(0, 12);
        }

        private static string Money(decimal? v) { return v.HasValue ? "₹ " + v.Value.ToString("N2", Inv) : "—"; }
        private static string Date(DateTime d) { return d.ToString("dd/MM/yyyy", Inv); }
        private static string Local(DateTime utc) { return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Inv); }

        private static byte[] Render(string title, PermitConfigRecord cfg, string reference, Action<ColumnDescriptor> body)
        {
            Init();
            return Document.Create(doc => doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(36);
                page.DefaultTextStyle(x => x.FontFamily(LatinFont).FontSize(11).Fallback(f => f.FontFamily(MarathiFont)));
                page.Header().Column(h =>
                {
                    h.Item().AlignCenter().Text(cfg.OrganizationName ?? "सोलापूर महानगरपालिका").FontSize(18).Bold();
                    if (!string.IsNullOrWhiteSpace(cfg.DepartmentName)) h.Item().AlignCenter().Text(cfg.DepartmentName).FontSize(12);
                    h.Item().AlignCenter().PaddingTop(4).Text(title).FontSize(15).Bold();
                    h.Item().PaddingTop(6).LineHorizontal(1);
                });
                page.Content().PaddingVertical(10).Column(body);
                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5f);
                    f.Item().Row(r =>
                    {
                        r.RelativeItem().Text("संदर्भ ओळख: " + reference + "   |   तयार केले: " + Local(DateTime.UtcNow)).FontSize(8);
                        r.ConstantItem(80).AlignRight().Text(t => { t.Span("पान ").FontSize(8); t.CurrentPageNumber().FontSize(8); });
                    });
                    if (!string.IsNullOrWhiteSpace(cfg.FooterText)) f.Item().Text(cfg.FooterText).FontSize(8);
                });
            })).GeneratePdf();
        }

        /// <summary>Value printed in a PDF row: blank or missing values print "—", never an empty cell or "null".</summary>
        public static string Cell(string value) { return string.IsNullOrWhiteSpace(value) ? "—" : value; }

        private static void Row(ColumnDescriptor c, string k, string v)
        {
            c.Item().PaddingVertical(2).Row(r => { r.ConstantItem(170).Text(k).Bold(); r.RelativeItem().Text(Cell(v)); });
        }

        /// <summary>
        /// Applicant rows of the acknowledgement / application / certificate. Name, mobile and e-mail may be empty when
        /// Apply.CollectApplicantDetails=false, and Aadhaar/identity, PAN and GST are no longer asked: each of these rows is printed only when present.
        /// </summary>
        public static List<KeyValuePair<string, string>> ApplicantRows(ApplicationDetail d)
        {
            var rows = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrWhiteSpace(v)) rows.Add(new KeyValuePair<string, string>(k, v)); };
            add("अर्जदाराचा प्रकार", d.ApplicantType); add("अर्जदाराचे पूर्ण नाव", d.ApplicantName); add("मोबाईल", d.Mobile); add("ई-मेल", d.Email);
            add("आधार/ओळखपत्र क्रमांक", d.IdentityNumber); add("PAN क्रमांक", d.PanNumber); add("GST क्रमांक", d.GstNumber);
            return rows;
        }

        private static void Core(ColumnDescriptor c, ApplicationDetail d)
        {
            Row(c, "अर्ज क्रमांक", d.ApplicationNumber);
            foreach (var row in ApplicantRows(d)) Row(c, row.Key, row.Value);
            var serviceDisplay = ServiceDisplay.Get(d.ServiceType, d.BusinessType, d.OtherBusinessType);
            Row(c, "सेवा", string.Equals(serviceDisplay.ServiceName, d.ServiceType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.ServiceName) ? d.ServiceName : serviceDisplay.ServiceName);
            if (!string.IsNullOrWhiteSpace(serviceDisplay.SubServiceName)) Row(c, "उप-सेवा", serviceDisplay.SubServiceName);
            Row(c, "कालावधी", Date(d.StartDate) + " ते " + Date(d.EndDate));
            if (DemandServicePricingRules.IsAreaBased(d.ServiceType))
            {
                Row(c, "एकूण दिवस", d.TotalDays.ToString());
                Row(c, "लांबी × रुंदी", (d.LengthFt.HasValue ? d.LengthFt.Value.ToString("0.##", Inv) : "—") + " × " + (d.WidthFt.HasValue ? d.WidthFt.Value.ToString("0.##", Inv) : "—") + " फूट");
                Row(c, "क्षेत्रफळ (चौ.फूट)", d.AreaSqFt.HasValue ? d.AreaSqFt.Value.ToString("0.##", Inv) : "—");
                Row(c, "दर (प्रतिदिन)", Money(d.CalculatedRate)); Row(c, "एकूण रक्कम", Money(d.FeeAmount));
            }
            else
            {
                Row(c, "कालावधीचा प्रकार", "१ वर्ष"); Row(c, "वार्षिक निश्चित रक्कम", Money(d.FeeAmount));
            }
            Row(c, "स्थिती", d.Status); Row(c, "शुल्क स्थिती", d.PaymentStatus);
        }

        public byte[] Acknowledgement(ApplicationDetail d, PermitConfigRecord cfg)
        {
            return Render("अर्ज पोचपावती (Acknowledgement)", cfg, Ref(d, "ACK"), c =>
            {
                c.Item().PaddingBottom(8).Text("आपला मागणी अर्ज प्राप्त झाला आहे. खालील तपशील नोंदवला आहे.");
                Core(c, d);
                Row(c, "सादर दिनांक", d.SubmittedAt.HasValue ? Local(d.SubmittedAt.Value) : "—");
                c.Item().PaddingTop(12).Text("अर्जाची स्थिती तपासण्यासाठी अर्ज क्रमांक वापरा.").FontSize(9);
            });
        }

        public static byte[] TransferCertificate(SMC.BhoomiMalmatta.Models.Entities.GalaTransfer t, SMC.BhoomiMalmatta.Models.Entities.Property p, PermitConfigRecord cfg)
        {
            var reference = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("XFER|" + t.Id + "|" + t.Stage + "|" + t.NewHolder))).Replace("-", "").Substring(0, 12);
            return Render("हस्तांतरण प्रमाणपत्र (Transfer Certificate)", cfg, reference, c =>
            {
                Row(c, "मालमत्ता क्रमांक", p.PropertyCode); Row(c, "मालमत्ता", p.Name); Row(c, "प्रकार", PropertyLabels.Category(p.Category));
                Row(c, "पत्ता", p.Address); Row(c, "TP क्रमांक", p.TpNumber);
                Row(c, "पूर्वीचा धारक", t.ExistingHolder); Row(c, "नवीन धारक", t.NewHolder); Row(c, "हस्तांतरण प्रकार", TransferTypes.Label(t.TransferType));
                Row(c, "हस्तांतरण शुल्क", Money(t.TransferFee)); Row(c, "दंड", Money(t.Penalty)); Row(c, "भरणा संदर्भ", t.PaymentReference);
                Row(c, "मंजुरी दिनांक", t.ApprovedAt.HasValue ? Local(t.ApprovedAt.Value) : "—"); Row(c, "मंजूर करणारा", t.ApprovedBy);
                if (!string.IsNullOrWhiteSpace(cfg.AuthorityDesignation)) Row(c, "प्राधिकारी", cfg.AuthorityDesignation);
            });
        }

        public byte[] SubmittedApplication(ApplicationDetail d, PermitConfigRecord cfg)
        {
            return Render("सादर केलेला मागणी अर्ज", cfg, Ref(d, "APP"), c =>
            {
                Core(c, d);
                Row(c, "कायमचा पत्ता", d.PermanentAddress); Row(c, "पत्रव्यवहाराचा पत्ता", d.CorrespondenceAddress);
                Row(c, "राज्य / जिल्हा", d.State + " / " + d.District); Row(c, "शहर / तालुका / पिन", d.City + " / " + d.Taluka + " / " + d.PinCode);
                Row(c, "झोन", d.Zone); Row(c, "जागेचे ठिकाण", d.Location); Row(c, "वर्णन", d.ServiceDescription);
                Row(c, "जोडलेली कागदपत्रे", string.Join(", ", d.Documents.Select(x => x.DocumentType)));
            });
        }

        public byte[] Receipt(ApplicationDetail d, PermitConfigRecord cfg)
        {
            var paid = d.History.Where(h => h.Action == AuditActions.PaymentDone).ToList();
            var paidAt = paid.Count == 0 ? (DateTime?)null : paid[paid.Count - 1].AtUtc;
            return Render("शुल्क पावती (Payment Receipt)", cfg, Ref(d, "RCT"), c =>
            {
                Row(c, "अर्ज क्रमांक", d.ApplicationNumber);
                if (!string.IsNullOrWhiteSpace(d.ApplicantName)) Row(c, "अर्जदार", d.ApplicantName);
                Row(c, "सेवा", d.ServiceName ?? d.ServiceType);
                Row(c, "भरलेली रक्कम", Money(d.PayableAmount > 0 ? (decimal?)d.PayableAmount : d.FeeAmount)); Row(c, "UTR / पावती क्रमांक", d.PaymentReference);
                Row(c, "भरणा दिनांक", d.PaymentDate.HasValue ? d.PaymentDate.Value.ToString("dd/MM/yyyy", Inv) : "—");
                Row(c, "पडताळणी दिनांक", paidAt.HasValue ? Local(paidAt.Value) : "—"); Row(c, "शुल्क स्थिती", d.PaymentStatus);
                c.Item().PaddingTop(12).Text("ही पावती संगणकाद्वारे तयार केली आहे.").FontSize(9);
            });
        }

        public byte[] Certificate(ApplicationDetail d, PermitConfigRecord cfg, List<string> terms, string approvedBy)
        {
            var approved = d.History.Where(h => h.Action == AuditActions.FinalApproved).ToList();
            return Render(string.IsNullOrWhiteSpace(cfg.CertificateTitle) ? "परवानगी पत्र" : cfg.CertificateTitle, cfg, Ref(d, "CRT"), c =>
            {
                c.Item().PaddingBottom(8).Text("खालील अर्जास सोलापूर महानगरपालिकेमार्फत परवानगी देण्यात येत आहे.");
                Core(c, d);
                Row(c, "मंजुरी दिनांक", approved.Count == 0 ? "—" : Local(approved[approved.Count - 1].AtUtc));
                if (terms != null && terms.Count > 0)
                {
                    c.Item().PaddingTop(10).Text("अटी व शर्ती").Bold();
                    for (var i = 0; i < terms.Count; i++) c.Item().Text((i + 1) + ". " + terms[i]).FontSize(10);
                }
                c.Item().PaddingTop(30).AlignRight().Text(string.IsNullOrWhiteSpace(cfg.AuthorityDesignation) ? "सहाय्यक आयुक्त" : cfg.AuthorityDesignation).Bold();
                c.Item().AlignRight().Text(approvedBy ?? string.Empty).FontSize(9);
            });
        }
    }
}
