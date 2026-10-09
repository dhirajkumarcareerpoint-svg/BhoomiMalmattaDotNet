using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Numbering;
using SMC.BhoomiMalmatta.Business.Payments;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class PricingTests
    {
        private static readonly DateTime D1 = new DateTime(2026, 10, 1);
        private static PricingRequest Req(string unit, decimal? master, DateTime s, DateTime e, decimal? l = null, decimal? w = null, decimal? officer = null)
        { return new PricingRequest { ServiceCode = "X", RateUnit = unit, MasterRate = master, StartDate = s, EndDate = e, LengthFt = l, WidthFt = w, OfficerEnteredRate = officer }; }

        [TestMethod] public void EndDateBeforeStartDate_IsRejected()
        {
            var r = DemandPricingCalculator.Calculate(Req("fixed", 100, D1, D1.AddDays(-1)));
            Assert.IsTrue(r.IsFailure); Assert.AreEqual("DATE_RANGE", r.Error.Code);
        }
        [DataTestMethod] [DataRow(0, 1)] [DataRow(1, 2)] [DataRow(9, 10)] [DataRow(30, 31)]
        public void TotalDays_AreInclusive(int plusDays, int expected) { Assert.AreEqual(expected, DemandPricingCalculator.TotalDays(D1, D1.AddDays(plusDays))); }

        [TestMethod] public void Annual_EndDate_Uses_AddYears_And_Clamps_LeapDay()
        {
            Assert.AreEqual(new DateTime(2027, 10, 6), DemandServicePricingRules.EffectiveEndDate("MiniGaala", new DateTime(2026, 10, 6), new DateTime(2030, 1, 1)));
            Assert.AreEqual(new DateTime(2025, 2, 28), DemandServicePricingRules.EffectiveEndDate("MajorGaala", new DateTime(2024, 2, 29), new DateTime(2030, 1, 1)));
        }

        [TestMethod] public void FixedRate_IgnoresDaysAndArea()
        {
            var r = DemandPricingCalculator.Calculate(Req("fixed", 50000m, D1, D1.AddDays(9)));
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(50000m, r.Value.FinalAmount.Value); Assert.AreEqual(10, r.Value.TotalDays);
        }
        [TestMethod] public void DailyRate_IsRateTimesDays() { Assert.AreEqual(300m, DemandPricingCalculator.Calculate(Req("daily", 100m, D1, D1.AddDays(2))).Value.FinalAmount.Value); }
        [TestMethod] public void AreaBased_IsAreaTimesRateTimesDays()
        {
            var r = DemandPricingCalculator.Calculate(Req("sq.ft.", 2m, D1, D1.AddDays(2), 10m, 5m)).Value;
            Assert.AreEqual(50m, r.AreaSqFt); Assert.AreEqual(300m, r.FinalAmount.Value);
        }
        [TestMethod] public void TenByTen_AtOneRupeePerSquareFoot_ForOneDay_IsOneHundred()
        {
            var r = DemandPricingCalculator.Calculate(Req("sq.ft.", 1m, D1, D1, 10m, 10m)).Value;
            Assert.AreEqual(100m, r.AreaSqFt); Assert.AreEqual(100m, r.DailyRate.Value); Assert.AreEqual(1, r.TotalDays); Assert.AreEqual(100m, r.FinalAmount.Value);
        }
        [TestMethod] public void TenByTen_AtOneRupeePerSquareFoot_ForThreeInclusiveDays_IsThreeHundred()
        {
            var r = DemandPricingCalculator.Calculate(Req("sq.ft.", 1m, D1, D1.AddDays(2), 10m, 10m)).Value;
            Assert.AreEqual(100m, r.AreaSqFt); Assert.AreEqual(100m, r.DailyRate.Value); Assert.AreEqual(3, r.TotalDays); Assert.AreEqual(300m, r.FinalAmount.Value);
        }
        [TestMethod] public void VariousBusinessSpace_WithoutMasterRate_UsesOfficerEnteredRate()
        {
            var r = DemandPricingCalculator.Calculate(Req("sq.ft.", null, D1, D1.AddDays(1), 4m, 2.5m, 3m)).Value;
            Assert.IsFalse(r.IsPendingOfficerAmount); Assert.AreEqual(60m, r.FinalAmount.Value); // 10 sq.ft x 3 x 2 days
        }
        [TestMethod] public void ScreenshotDimensions_RateAndSingleInclusiveDay_UseAreaRateDaysRule()
        {
            var r = DemandPricingCalculator.Calculate(Req("sq.ft.", null, D1, D1, 30m, 30m, 900m)).Value;
            Assert.AreEqual(900m, r.AreaSqFt); Assert.AreEqual(1, r.TotalDays); Assert.AreEqual(810000m, r.FinalAmount.Value);
        }
        [TestMethod] public void VariousBusinessSpace_WithNoRateAnywhere_IsPendingNotAnError()
        {
            var r = DemandPricingCalculator.Calculate(Req("sq.ft.", null, D1, D1, 4m, 2m));
            Assert.IsTrue(r.IsSuccess); Assert.IsTrue(r.Value.IsPendingOfficerAmount); Assert.IsNull(r.Value.FinalAmount);
        }
        [TestMethod] public void MasterRate_TakesPrecedenceOverOfficerRate() { Assert.AreEqual(20m, DemandPricingCalculator.Calculate(Req("sq.ft.", 2m, D1, D1, 5m, 2m, 99m)).Value.FinalAmount.Value); }
        [DataTestMethod] [DataRow(0, 5)] [DataRow(5, 0)] [DataRow(-1, 5)] [DataRow(5, -1)]
        public void AreaBased_RequiresPositiveDimensions(int l, int w) { Assert.AreEqual("AREA_REQUIRED", DemandPricingCalculator.Calculate(Req("sq.ft.", 2m, D1, D1, l, w)).Error.Code); }
        [TestMethod] public void UnknownUnit_IsRejected() { Assert.AreEqual("UNIT_UNKNOWN", DemandPricingCalculator.Calculate(Req("furlong", 2m, D1, D1)).Error.Code); }
        [TestMethod] public void Amount_IsRoundedToTwoDecimals() { Assert.AreEqual(1.34m, DemandPricingCalculator.Calculate(Req("sq.ft.", 0.1m, D1, D1, 3.3m, 4.05m)).Value.FinalAmount.Value); }
    }

    [TestClass]
    public class WorkflowMachineTests
    {
        [TestMethod] public void Officer_List_Action_Options_Are_Stage_Role_And_Payment_Specific()
        {
            CollectionAssert.AreEquivalent(new[] { WorkflowActions.JEVerifyForward, WorkflowActions.JEReturnForCorrection, WorkflowActions.JEReject }, System.Linq.Enumerable.ToArray(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.JEPending, Roles.JE, PaymentStatuses.PendingOSAmount)));
            var submittedPaymentActions = System.Linq.Enumerable.ToArray(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.PaymentRequired, Roles.OS, PaymentStatuses.PaymentSubmitted));
            Assert.AreEqual(2, submittedPaymentActions.Length, string.Join(",", submittedPaymentActions));
            Assert.IsTrue(submittedPaymentActions.Contains(WorkflowActions.OSConfirmPayment)); Assert.IsTrue(submittedPaymentActions.Contains(WorkflowActions.OSRejectPayment));
            Assert.AreEqual(0, System.Linq.Enumerable.Count(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.PaymentRequired, Roles.OS, PaymentStatuses.PaymentRequired)));
            var osAmountActions = System.Linq.Enumerable.ToArray(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.OSPending, Roles.OS, PaymentStatuses.PendingOSAmount));
            Assert.AreEqual(3, osAmountActions.Length); Assert.IsTrue(osAmountActions.Contains(WorkflowActions.OSRequestPayment)); Assert.IsTrue(osAmountActions.Contains(WorkflowActions.OSReturnForCorrection)); Assert.IsTrue(osAmountActions.Contains(WorkflowActions.OSReject));
            Assert.AreEqual(0, System.Linq.Enumerable.Count(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.AssistantCommissionerApprovalPending, Roles.AssistantCommissioner, PaymentStatuses.PaymentRequired)));
            // Default policy (Workflow.AcAllowReturnReject = false): Approve only. With the switch on, the previous three options return.
            var acActions = System.Linq.Enumerable.ToArray(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.AssistantCommissionerApprovalPending, Roles.AssistantCommissioner, PaymentStatuses.PaymentDone));
            CollectionAssert.AreEqual(new[] { WorkflowActions.ACApprove }, acActions);
            var acAll = System.Linq.Enumerable.ToArray(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.AssistantCommissionerApprovalPending, Roles.AssistantCommissioner, PaymentStatuses.PaymentDone, new WorkflowPolicy(true)));
            Assert.AreEqual(3, acAll.Length); Assert.IsTrue(acAll.Contains(WorkflowActions.ACApprove)); Assert.IsTrue(acAll.Contains(WorkflowActions.ACReturn)); Assert.IsTrue(acAll.Contains(WorkflowActions.ACReject));
        }
        [TestMethod] public void Legacy_Admin_And_Staff_Never_Get_Officer_List_Radios()
        {
            Assert.AreEqual(0, System.Linq.Enumerable.Count(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Legacy, ZoneStages.JEPending, Roles.JE, PaymentStatuses.PendingOSAmount)));
            foreach (var role in new[] { Roles.Admin, Roles.Staff }) Assert.AreEqual(0, System.Linq.Enumerable.Count(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, ZoneStages.JEPending, role, PaymentStatuses.PendingOSAmount)));
        }
        [TestMethod] public void Posted_Officer_Action_Not_Allowed_For_Role_Stage_Or_Payment_Is_Rejected()
        {
            Assert.AreEqual("WORKFLOW_FORBIDDEN", ZoneWorkflowMachine.Evaluate(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, WorkflowActions.OSConfirmPayment, Roles.JE, null).Error.Code);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", ZoneWorkflowMachine.Evaluate(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, WorkflowActions.ACApprove, Roles.OS, "मंजूर").Error.Code);
            Assert.AreEqual("PAYMENT_PRECONDITION", ZoneWorkflowMachine.Evaluate(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentRequired, WorkflowActions.ACApprove, Roles.AssistantCommissioner, "मंजूर").Error.Code);
            Assert.AreEqual("PAYMENT_PRECONDITION", ZoneWorkflowMachine.Evaluate(ZoneStages.OSPending, PaymentStatuses.PendingOSAmount, WorkflowActions.OSForwardToAC, Roles.OS, null).Error.Code);
        }
        [DataTestMethod]
        [DataRow(0, "0 ते 0 पैकी 0 नोंदी", "[1] / 1")]
        [DataRow(8, "1 ते 8 पैकी 8 नोंदी", "[1] / 1")]
        [DataRow(9, "1 ते 8 पैकी 9 नोंदी", "[1] / 2")]
        [DataRow(27, "1 ते 8 पैकी 27 नोंदी", "[1] / 4")]
        public void Officer_Pager_First_Page_Boundaries(int total, string range, string page)
        { var x = PagerSummary.Create(total, 1, 8); Assert.AreEqual(range, x.RangeText); Assert.AreEqual(page, x.PageText); }
        [TestMethod] public void Officer_Pager_Last_Page_Clamps_And_Reports_Range()
        { var x = PagerSummary.Create(27, 99, 8); Assert.AreEqual("25 ते 27 पैकी 27 नोंदी", x.RangeText); Assert.AreEqual("[4] / 4", x.PageText); }
        [TestMethod] public void Officer_Pager_Second_Page_Contains_Only_Remainder()
        { var x = PagerSummary.Create(9, 2, 8); Assert.AreEqual("9 ते 9 पैकी 9 नोंदी", x.RangeText); Assert.AreEqual("[2] / 2", x.PageText); }
        [TestMethod] public void Officer_Search_Filters_Trim_Values_And_Use_Inclusive_Date_Bounds()
        {
            var f = new ApplicationFilter { Search = " 2026-0042 ", Status = " Submitted ", ServiceType = " Space " , From = new DateTime(2026, 10, 1, 15, 30, 0), To = new DateTime(2026, 10, 6, 21, 10, 0), ZoneScope = 3 };
            f.Normalize();
            Assert.AreEqual("2026-0042", f.Search); Assert.AreEqual("Submitted", f.Status); Assert.AreEqual("Space", f.ServiceType);
            Assert.AreEqual(new DateTime(2026, 10, 1), f.From.Value); Assert.AreEqual(new DateTime(2026, 10, 6), f.To.Value); Assert.AreEqual(3, f.ZoneScope.Value);
        }

        [TestMethod] public void JE_Forwards_To_OS()
        {
            var r = ZoneWorkflowMachine.Evaluate(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, WorkflowActions.JEVerifyForward, Roles.JE, null);
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(ZoneStages.OSPending, r.Value.ToStage); Assert.AreEqual(Roles.OS, r.Value.ToRole);
        }
        [TestMethod] public void OS_Cannot_Forward_To_AC_Until_Payment_Done_Or_NotRequired()
        {
            Assert.AreEqual("PAYMENT_PRECONDITION", ZoneWorkflowMachine.Evaluate(ZoneStages.OSPending, PaymentStatuses.PendingOSAmount, WorkflowActions.OSForwardToAC, Roles.OS, null).Error.Code);
            Assert.IsTrue(ZoneWorkflowMachine.Evaluate(ZoneStages.OSPending, PaymentStatuses.PaymentDone, WorkflowActions.OSForwardToAC, Roles.OS, null).IsSuccess);
            Assert.IsTrue(ZoneWorkflowMachine.Evaluate(ZoneStages.OSPending, PaymentStatuses.NotRequired, WorkflowActions.OSForwardToAC, Roles.OS, null).IsSuccess);
        }
        [TestMethod] public void OS_To_AC_Moves_To_ApprovalPending()
        {
            var r = ZoneWorkflowMachine.Evaluate(ZoneStages.OSPending, PaymentStatuses.PaymentDone, WorkflowActions.OSForwardToAC, Roles.OS, null).Value;
            Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, r.ToStage); Assert.AreEqual(ApplicationStatuses.ApprovalPending, r.NewStatus);
        }
        [TestMethod] public void AC_FinalApproval_Remark_Is_Optional_And_Finalizes()
        {
            var s = ZoneStages.AssistantCommissionerApprovalPending;
            var blank = ZoneWorkflowMachine.Evaluate(s, PaymentStatuses.PaymentDone, WorkflowActions.ACApprove, Roles.AssistantCommissioner, "  ");
            Assert.IsTrue(blank.IsSuccess); Assert.AreEqual(ZoneStages.Approved, blank.Value.ToStage);
            var ok = ZoneWorkflowMachine.Evaluate(s, PaymentStatuses.PaymentDone, WorkflowActions.ACApprove, Roles.AssistantCommissioner, "मंजूर").Value;
            Assert.AreEqual(ZoneStages.Approved, ok.ToStage); Assert.AreEqual(ApplicationStatuses.Approved, ok.NewStatus);
        }
        [TestMethod] public void JE_Cannot_Perform_OS_Or_AC_Actions()
        {
            Assert.AreEqual("WORKFLOW_FORBIDDEN", ZoneWorkflowMachine.Evaluate(ZoneStages.OSPending, PaymentStatuses.PaymentDone, WorkflowActions.OSForwardToAC, Roles.JE, null).Error.Code);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", ZoneWorkflowMachine.Evaluate(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, WorkflowActions.ACApprove, Roles.JE, "x").Error.Code);
        }
        [TestMethod] public void OS_Cannot_Give_Final_Approval()
        { Assert.AreEqual("WORKFLOW_FORBIDDEN", ZoneWorkflowMachine.Evaluate(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, WorkflowActions.ACApprove, Roles.OS, "x").Error.Code); }
        [DataTestMethod]
        [DataRow(ZoneStages.JEPending, WorkflowActions.OSForwardToAC)] [DataRow(ZoneStages.JEPending, WorkflowActions.ACApprove)]
        [DataRow(ZoneStages.OSPending, WorkflowActions.ACApprove)] [DataRow(ZoneStages.OSPending, WorkflowActions.JEVerifyForward)]
        public void Skipping_Or_Invalid_Transitions_Are_Rejected(string stage, string action)
        { Assert.AreEqual("WORKFLOW_INVALID_TRANSITION", ZoneWorkflowMachine.Evaluate(stage, PaymentStatuses.PaymentDone, action, Roles.Admin, "x").Error.Code); }
        [DataTestMethod] [DataRow(ZoneStages.Approved)] [DataRow(ZoneStages.Rejected)]
        public void Finalized_Applications_Accept_No_Action_Except_Reopen(string stage)
        {
            Assert.AreEqual("WORKFLOW_FINALIZED", ZoneWorkflowMachine.Evaluate(stage, PaymentStatuses.PaymentDone, WorkflowActions.ACApprove, Roles.AssistantCommissioner, "x").Error.Code);
            Assert.AreEqual("WORKFLOW_FINALIZED", ZoneWorkflowMachine.Evaluate(stage, PaymentStatuses.PaymentDone, WorkflowActions.JEVerifyForward, Roles.JE, "x").Error.Code);
        }
        [TestMethod] public void Approved_Cannot_Be_Reopened_But_Rejected_Can_By_Admin_Only()
        {
            Assert.AreEqual("WORKFLOW_INVALID_TRANSITION", ZoneWorkflowMachine.Evaluate(ZoneStages.Approved, PaymentStatuses.PaymentDone, WorkflowActions.AdminReopen, Roles.Admin, "x").Error.Code);
            Assert.IsTrue(ZoneWorkflowMachine.Evaluate(ZoneStages.Rejected, PaymentStatuses.PaymentDone, WorkflowActions.AdminReopen, Roles.Admin, "x").IsSuccess);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", ZoneWorkflowMachine.Evaluate(ZoneStages.Rejected, PaymentStatuses.PaymentDone, WorkflowActions.AdminReopen, Roles.JE, "x").Error.Code);
        }
        [TestMethod] public void Staff_Role_Has_No_Workflow_Actions()
        {
            foreach (var stage in new[] { ZoneStages.JEPending, ZoneStages.OSPending, ZoneStages.AssistantCommissionerApprovalPending })
                Assert.AreEqual(0, System.Linq.Enumerable.Count(ZoneWorkflowMachine.AllowedActions(stage, Roles.Staff)));
        }
        [TestMethod] public void Every_Stage_Maps_To_A_Known_Status()
        {
            foreach (var st in ZoneStages.All) CollectionAssert.Contains(ApplicationStatuses.All, ZoneWorkflowMachine.StatusFor(st));
        }
    }

    [TestClass]
    public class HistoryAndNumberingTests
    {
        private static HistoryEntry E(string action)
        {
            return new HistoryEntry { ApplicationId = 1, ApplicationNumber = "2026-0005", FromRole = Roles.JE, ToRole = Roles.OS, Action = action, PreviousStatus = "Submitted", NewStatus = "UnderScrutiny",
                PreviousStage = "JEPending", NewStage = "OSPending", Remarks = "r", ActionByUserId = 7, ActionByUserName = "JE1", ActionAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"), IpAddress = "1.2.3.4", CorrelationId = "corr" };
        }
        [TestMethod] public void History_Is_Hash_Chained_And_Verifiable()
        {
            var j = ActionHistory.Append("[]", E("a")); j = ActionHistory.Append(j, E("b"));
            Assert.IsTrue(ActionHistory.VerifyChain(j)); Assert.AreEqual(2, ActionHistory.Parse(j).Count);
        }
        [TestMethod] public void History_Tampering_Is_Detected()
        {
            var j = ActionHistory.Append("[]", E("a")); j = ActionHistory.Append(j, E("b"));
            Assert.IsTrue(j.Contains("\"Remarks\":\"r\""));
            Assert.IsFalse(ActionHistory.VerifyChain(j.Replace("\"Remarks\":\"r\"", "\"Remarks\":\"changed\"")));
        }
        [TestMethod] public void History_Deletion_Or_Rewrite_Is_Not_AppendOnly()
        {
            var j1 = ActionHistory.Append("[]", E("a")); var j2 = ActionHistory.Append(j1, E("b"));
            Assert.IsTrue(ActionHistory.IsAppendOnly(j1, j2));
            Assert.IsFalse(ActionHistory.IsAppendOnly(j2, j1));
            Assert.IsFalse(ActionHistory.IsAppendOnly(j1, ActionHistory.Append("[]", E("different"))));
        }
        [TestMethod] public void History_Preserves_Marathi_Text()
        {
            var e = E("a"); e.Remarks = "पत्ता दुरुस्त करा";
            var j = ActionHistory.Append("[]", e);
            Assert.AreEqual("पत्ता दुरुस्त करा", ActionHistory.Parse(j)[0].Remarks); Assert.IsTrue(ActionHistory.VerifyChain(j));
        }
        [TestMethod] public void Payment_Confirmation_Is_Idempotent()
        {
            Assert.AreEqual(PaymentConfirmationOutcome.Applied, PaymentRules.Confirm(PaymentStatuses.PaymentSubmitted, null, "UTR123456"));
            Assert.AreEqual(PaymentConfirmationOutcome.AlreadyApplied, PaymentRules.Confirm(PaymentStatuses.PaymentDone, "UTR123456", "UTR123456"));
            Assert.AreEqual(PaymentConfirmationOutcome.Conflict, PaymentRules.Confirm(PaymentStatuses.PaymentDone, "UTR123456", "UTR999999"));
            Assert.AreEqual(PaymentConfirmationOutcome.NotAwaitingPayment, PaymentRules.Confirm(PaymentStatuses.NotRequired, null, "UTR123456"));
        }
        [DataTestMethod] [DataRow("UTR123456", true)] [DataRow("123456789012", true)] [DataRow("AB-12/34", false)] [DataRow("12345", false)] [DataRow("", false)] [DataRow("UTR 123456", false)] [DataRow("UTR<script>", false)]
        public void Payment_Reference_Validation(string reference, bool valid) { Assert.AreEqual(valid, PaymentRules.IsValidReference(reference)); }

        [TestMethod] public void Number_Fixed_Strategy_Uses_Stored_Prefix_Without_Hardcoded_Year()
        { Assert.AreEqual("2026-0005", ApplicationNumberFormatter.Format("2026", "Fixed", 4, 5, new DateTime(2031, 1, 1)).Value); }
        [TestMethod] public void Number_Calendar_And_Financial_Year_Strategies()
        {
            Assert.AreEqual("SMC-2027-0012", ApplicationNumberFormatter.Format("SMC", "CalendarYear", 4, 12, new DateTime(2027, 2, 1)).Value);
            Assert.AreEqual("SMC-2026-27-0012", ApplicationNumberFormatter.Format("SMC", "FinancialYear", 4, 12, new DateTime(2026, 4, 1)).Value);
            Assert.AreEqual("SMC-2026-27-0012", ApplicationNumberFormatter.Format("SMC", "FinancialYear", 4, 12, new DateTime(2027, 3, 31)).Value);
        }
        [TestMethod] public void Number_Unknown_Strategy_Or_Bad_Input_Fails()
        {
            Assert.IsTrue(ApplicationNumberFormatter.Format("X", "Weird", 4, 1, DateTime.Today).IsFailure);
            Assert.IsTrue(ApplicationNumberFormatter.Format("", "Fixed", 4, 1, DateTime.Today).IsFailure);
            Assert.IsTrue(ApplicationNumberFormatter.Format("X", "Fixed", 4, 0, DateTime.Today).IsFailure);
        }
    }
}
