using System;
using System.Collections.Generic;
using System.Web.UI;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class CalculationsPage : ModulePage<Calculation>
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin }; } }

        protected override string Heading { get { return "Calculation"; } }
        protected override string EntityName { get { return "Calculation"; } }
        protected override string SearchCaption { get { return "शोध"; } }
        protected override bool CanEdit(Calculation e) { return false; }
        protected override string[] Headers { get { return new[] { "क्रमांक", "मालमत्ता", "मासिक भाडे (₹)", "महिने", "मागील थकबाकी (₹)", "चालू मागणी (₹)", "एकूण (₹)", "दिनांक" }; } }

        protected override List<Field> BuildFields(bool isNew)
        {
            var f = new List<Field>
            {
                new Field("PropertyId", "मालमत्ता", FieldKind.Select) { Required = true, Options = PropertyOptions(true) },
                new Field("PeriodMonths", "कालावधी (महिने, १ ते " + RentCalculator.MaxMonths + ")", FieldKind.Int) { Required = true },
                new Field("PreviousOutstanding", "मागील थकबाकी (₹)", FieldKind.Decimal)
            };
            if (!isNew)
            {
                f.Add(new Field("Rate", "मासिक भाडे (दर)", FieldKind.Text)); f.Add(new Field("CurrentDemand", "चालू मागणी (₹)", FieldKind.Text));
                f.Add(new Field("TotalAmount", "एकूण रक्कम (₹)", FieldKind.Text)); f.Add(new Field("CalculationDate", "गणना दिनांक", FieldKind.Text)); f.Add(new Field("Status", "स्थिती", FieldKind.Text));
            }
            return f;
        }

        protected override PagedResult<Calculation> Query(string search, string status, int? propertyId, int page) { return AppServices.Register.SearchCalculations(propertyId, page, PageSize); }
        protected override Calculation LoadEntity(int id) { return AppServices.Register.GetCalculation(id); }
        protected override int IdOf(Calculation e) { return e.Id; }
        protected override string DetailTitle(Calculation e) { return "Calculation #" + e.Id + " — " + PropertyLabel(e.PropertyId); }
        protected override string[] Cells(Calculation e) { return new[] { "#" + e.Id, PropertyLabel(e.PropertyId), Money(e.Rate), e.PeriodMonths.ToString(), Money(e.PreviousOutstanding), Money(e.CurrentDemand), Money(e.TotalAmount), Day(e.CalculationDate) }; }

        protected override Dictionary<string, string> Values(Calculation e)
        {
            return new Dictionary<string, string>
            {
                { "PropertyId", e.PropertyId.ToString() }, { "PeriodMonths", e.PeriodMonths.ToString() }, { "PreviousOutstanding", e.PreviousOutstanding.HasValue ? e.PreviousOutstanding.Value.ToString("N2") : "0.00" },
                { "Rate", Money(e.Rate) }, { "CurrentDemand", Money(e.CurrentDemand) }, { "TotalAmount", Money(e.TotalAmount) }, { "CalculationDate", Day(e.CalculationDate) }, { "Status", e.Status }
            };
        }
        protected override Dictionary<string, string> Defaults()
        {
            int pid; var q = Request.QueryString["property"];
            return new Dictionary<string, string> { { "PropertyId", q != null && int.TryParse(q, out pid) ? pid.ToString() : "" }, { "PreviousOutstanding", "0" } };
        }

        protected override Result<int> Save(int id, Dictionary<string, string> v)
        {
            var pid = I(v, "PropertyId", "मालमत्ता"); var months = I(v, "PeriodMonths", "कालावधी"); var prev = D(v, "PreviousOutstanding", "मागील थकबाकी") ?? 0m;
            if (Errors.Count > 0) return Result.Failure<int>(new Error("INVALID", string.Join(" ", Errors)));
            var prop = AppServices.Register.GetProperty(pid.Value);
            if (prop == null) return Result.Failure<int>(new Error("NO_PROPERTY", "मालमत्ता आढळली नाही."));
            var calc = RentCalculator.Calculate(prop.MonthlyRent, months.Value, prev);
            if (calc.IsFailure) return Result.Failure<int>(calc.Error);
            var c = calc.Value;
            return AppServices.Register.SaveCalculation(new Calculation
            {
                PropertyId = prop.Id, Rate = c.Rate, PeriodMonths = c.Months, PreviousOutstanding = c.Previous, CurrentDemand = c.Current, CalculatedAmount = c.Current, TotalAmount = c.Total,
                CalculationDate = DateTime.Today, Status = CalculationStatuses.Calculated, Shera = c.Formula
            }, Actor());
        }

        protected override void BuildExtras(Control host, Calculation e)
        {
            Lit(host, "<div class=\"card mt\"><h3 style=\"margin-top:0\">सूत्र (Formula)</h3><div class=\"preview\"><b>" + Enc(e.Shera ?? "") + "</b></div>" +
                "<p class=\"muted\">दर मालमत्ता नोंदणीतील मासिक भाड्यावरून घेतला जातो; गणना सर्व्हरवर केली जाते आणि जतन केलेली नोंद बदलता येत नाही.</p></div>");
            RegisterUi.Documents(host, "Calculation", e.Id, CanWrite, Actor(), Url("id=" + e.Id), m => Fail(new[] { m }));
        }
    }
}
