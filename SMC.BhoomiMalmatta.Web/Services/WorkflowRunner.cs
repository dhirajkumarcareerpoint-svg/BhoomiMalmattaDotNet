using System.Collections.Generic;
using System;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Services
{
    public sealed class WorkflowInput
    {
        public int ApplicationId { get; set; }
        public string Action { get; set; }
        public string ExpectedStage { get; set; }
        public string ExpectedRowVersion { get; set; }
        public string Remarks { get; set; }
        public decimal? OfficerRate { get; set; }
        public string PaymentReference { get; set; }
        public string PaymentMode { get; set; }
        public DateTime? PaymentDate { get; set; }
        public SMC.BhoomiMalmatta.Business.Abstractions.DocumentUpload PaymentProof { get; set; }
    }

    /// <summary>
    /// Shared by the Web Forms pages and the Web API. The browser never supplies status, role, zone or a payable amount:
    /// the amount for "payment required" is recomputed here from the master rate (or the OS-entered rate when the master rate is NULL).
    /// </summary>
    public static class WorkflowRunner
    {
        public static Result<WorkflowResult> Run(WorkflowInput i, ActorContext actor)
        {
            decimal? amount = null; decimal? officerRate = null;
            if (i.Action == WorkflowActions.OSRequestPayment)
            {
                var d = AppServices.Query.GetDetail(i.ApplicationId);
                if (d == null) return Result.Failure<WorkflowResult>(new Error("NOT_FOUND", "अर्ज आढळला नाही."));
                var master = AppServices.Masters.GetActiveRate(d.ServiceType);
                var masterHasRate = master != null && master.Rate.HasValue && master.Rate.Value > 0;
                var data = new SubmissionData { ServiceType = d.ServiceType, StartDate = d.StartDate, EndDate = d.EndDate, LengthFt = d.LengthFt, WidthFt = d.WidthFt };
                var price = AppServices.Submission.Preview(data, masterHasRate ? null : i.OfficerRate);
                if (price.IsFailure) return Result.Failure<WorkflowResult>(price.Error);
                if (!price.Value.FinalAmount.HasValue)
                    return Result.Failure<WorkflowResult>(new Error("RATE_REQUIRED", "या सेवेसाठी दर निश्चित नाही; कृपया दर (₹) प्रविष्ट करा."));
                amount = price.Value.FinalAmount;
                if (!masterHasRate) officerRate = price.Value.Rate;
            }
            if (i.Action == WorkflowActions.OSConfirmPayment || i.Action == WorkflowActions.OSRejectPayment)
            {
                var current = AppServices.Query.GetDetail(i.ApplicationId);
                if (current == null) return Result.Failure<WorkflowResult>(new Error("NOT_FOUND", "अर्ज आढळला नाही."));
                i.PaymentReference = current.PaymentReference;
            }
            return AppServices.Workflow.Execute(new WorkflowCommand
            {
                ApplicationId = i.ApplicationId, Action = i.Action, ExpectedStage = i.ExpectedStage, ExpectedRowVersion = i.ExpectedRowVersion, Remarks = i.Remarks,
                Amount = amount, OfficerRate = officerRate, PaymentReference = i.PaymentReference, PaymentMode = i.PaymentMode, PaymentDate = i.PaymentDate, PaymentProof = i.PaymentProof
            }, actor);
        }

        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { WorkflowActions.JEVerifyForward, "तपासणी पूर्ण - OS कडे पाठवा (JE शिफारस)" },
            { WorkflowActions.JEReturnForCorrection, "दुरुस्तीसाठी अर्जदाराकडे परत पाठवा" },
            { WorkflowActions.JEReject, "अर्ज नाकारा" },
            { WorkflowActions.OSRequestPayment, "शुल्क भरणा मागणी (दर/रक्कम पडताळून)" },
            { WorkflowActions.OSMarkPaymentNotRequired, "शुल्क लागू नाही" },
            { WorkflowActions.OSConfirmPayment, "भरणा पडताळणी / नोंद" },
            { WorkflowActions.OSRejectPayment, "भरणा नाकारा (शेरा आवश्यक)" },
            { WorkflowActions.OSForwardToAC, "सहाय्यक आयुक्तांकडे पाठवा" },
            { WorkflowActions.OSReturnForCorrection, "दुरुस्तीसाठी अर्जदाराकडे परत पाठवा" },
            { WorkflowActions.OSReject, "अर्ज नाकारा" },
            { WorkflowActions.ACApprove, "अंतिम मंजुरी" },
            { WorkflowActions.ACReturn, "OS कडे परत पाठवा" },
            { WorkflowActions.ACReject, "अर्ज नाकारा" },
            { WorkflowActions.AdminReopen, "अर्ज पुन्हा उघडा" },
        };
        public static string Label(string action) { string l; return Labels.TryGetValue(action, out l) ? l : action; }
    }
}
