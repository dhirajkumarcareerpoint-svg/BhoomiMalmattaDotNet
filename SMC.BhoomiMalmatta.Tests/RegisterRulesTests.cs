using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class RegisterRulesTests
    {
        [DataTestMethod]
        [DataRow("9876543210", true)] [DataRow("6000000000", true)] [DataRow(" 7000000000 ", true)]
        [DataRow("1010202030", false)] [DataRow("5876543210", false)] [DataRow("987654321", false)] [DataRow("98765432101", false)]
        [DataRow("98765 43210", false)] [DataRow("९८७६५४३२१०", false)] [DataRow("", false)] [DataRow(null, false)]
        public void Mobile_Rule_Accepts_Only_Ten_Ascii_Digits_Starting_6_To_9(string value, bool expected) { Assert.AreEqual(expected, RegisterRules.IsValidMobile(value)); }

        [TestMethod] public void Mobile_Message_Is_The_Required_Marathi_Text() { Assert.AreEqual("मोबाइल क्रमांक १० अंकी आणि ६, ७, ८ किंवा ९ ने सुरू असावा.", RegisterRules.MobileMessage); }

        private static Property Valid() { return new Property { Category = PropertyCategories.Major, Name = "Gala", Status = PropertyStatuses.Vacant, MonthlyRent = 0, AnnualDemand = 0 }; }

        [TestMethod] public void Property_Requires_Known_Category_Name_And_Status()
        {
            Assert.AreEqual(0, RegisterRules.ValidateProperty(Valid()).Count);
            var p = Valid(); p.Category = "Other"; Assert.IsTrue(RegisterRules.ValidateProperty(p).Count > 0);
            p = Valid(); p.Name = " "; Assert.IsTrue(RegisterRules.ValidateProperty(p).Count > 0);
            p = Valid(); p.Status = "x"; Assert.IsTrue(RegisterRules.ValidateProperty(p).Count > 0);
            p = Valid(); p.MonthlyRent = -1; Assert.IsTrue(RegisterRules.ValidateProperty(p).Count > 0);
        }

        [TestMethod] public void Property_Category_Prefixes_Match_Existing_Codes()
        {
            Assert.AreEqual("MJ", PropertyCategories.Prefix(PropertyCategories.Major));
            Assert.AreEqual("MN", PropertyCategories.Prefix(PropertyCategories.Mini));
        }

        [TestMethod] public void Allocation_Rejects_Inverted_Dates_And_Bad_Mobile_And_Incomplete_Award()
        {
            var a = new AllocationProcess { PropertyId = 1, Method = AllocationMethods.Auction, Status = AllocationStatuses.Advertised, PublishDate = new DateTime(2026, 1, 10), LastDateToApply = new DateTime(2026, 1, 20), AuctionDate = new DateTime(2026, 1, 25) };
            Assert.AreEqual(0, RegisterRules.ValidateAllocation(a).Count);
            a.LastDateToApply = new DateTime(2026, 1, 9); Assert.IsTrue(RegisterRules.ValidateAllocation(a).Count > 0);
            a.LastDateToApply = new DateTime(2026, 1, 20); a.AuctionDate = new DateTime(2026, 1, 19); Assert.IsTrue(RegisterRules.ValidateAllocation(a).Count > 0);
            a.AuctionDate = null; a.LastDateToApply = a.PublishDate; Assert.AreEqual(0, RegisterRules.ValidateAllocation(a).Count, "same-day closing is allowed");
            a.HighestBidderMobile = "1010202030"; Assert.IsTrue(RegisterRules.ValidateAllocation(a).Contains(RegisterRules.MobileMessage));
            a.HighestBidderMobile = null; a.Status = AllocationStatuses.Awarded; Assert.IsTrue(RegisterRules.ValidateAllocation(a).Count > 0);
            a.HighestBidderName = "A"; a.ReserveAmount = 100; a.HighestBidAmount = 99; Assert.IsTrue(RegisterRules.ValidateAllocation(a).Count > 0, "below reserve");
            a.HighestBidAmount = 100; Assert.AreEqual(0, RegisterRules.ValidateAllocation(a).Count);
        }

        [TestMethod] public void Rent_Calculation_Is_Rate_Times_Months_Plus_Previous()
        {
            var r = RentCalculator.Calculate(12000m, 3, 500.5m);
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(36000m, r.Value.Current); Assert.AreEqual(36500.5m, r.Value.Total);
            Assert.IsTrue(r.Value.Formula.Contains("36,000") || r.Value.Formula.Contains("36000"));
        }

        [TestMethod] public void Rent_Calculation_Boundaries()
        {
            Assert.IsTrue(RentCalculator.Calculate(100m, 1, 0).IsSuccess);
            Assert.IsTrue(RentCalculator.Calculate(100m, RentCalculator.MaxMonths, 0).IsSuccess);
            Assert.AreEqual("PERIOD", RentCalculator.Calculate(100m, 0, 0).Error.Code);
            Assert.AreEqual("PERIOD", RentCalculator.Calculate(100m, RentCalculator.MaxMonths + 1, 0).Error.Code);
            Assert.AreEqual("NO_RATE", RentCalculator.Calculate(0m, 3, 0).Error.Code);
            Assert.AreEqual("PREV", RentCalculator.Calculate(100m, 3, -1m).Error.Code);
            Assert.AreEqual(0.03m, RentCalculator.Calculate(0.01m, 3, 0).Value.Current);
        }

        [TestMethod] public void Transfer_Fees_Come_From_Rate_And_Penalty_Only_For_Breach()
        {
            var rate = new GalaTransferRate { Category = "MajorGaale", TransferFee = 50000m, BreachPenalty = 100000m, IsActive = true };
            Assert.AreEqual(0m, TransferRules.Fees(rate, false).Value[1]);
            Assert.AreEqual(100000m, TransferRules.Fees(rate, true).Value[1]);
            Assert.AreEqual(50000m, TransferRules.Fees(rate, true).Value[0]);
            Assert.AreEqual("NO_RATE", TransferRules.Fees(null, false).Error.Code);
            rate.IsActive = false; Assert.IsTrue(TransferRules.Fees(rate, false).IsFailure);
        }

        [TestMethod] public void Transfer_Workflow_Follows_Request_Review_Payment_Approval_Only()
        {
            Assert.AreEqual(TransferStages.Submitted, TransferRules.Find(TransferStages.Draft, "submit").To);
            Assert.AreEqual(TransferStages.Reviewed, TransferRules.Find(TransferStages.Submitted, "review").To);
            Assert.AreEqual(TransferStages.PaymentVerified, TransferRules.Find(TransferStages.Reviewed, "pay").To);
            Assert.AreEqual(TransferStages.Approved, TransferRules.Find(TransferStages.PaymentVerified, "approve").To);
            Assert.IsNull(TransferRules.Find(TransferStages.Submitted, "approve"), "cannot skip review/payment");
            Assert.IsNull(TransferRules.Find(TransferStages.Reviewed, "approve"));
            Assert.IsNull(TransferRules.Find(TransferStages.Approved, "approve"), "no duplicate processing");
            Assert.IsNull(TransferRules.Find(TransferStages.Rejected, "review"));
            Assert.IsNull(TransferRules.Find(TransferStages.Draft, "reject"));
        }

        [TestMethod] public void Transfer_Roles_Are_Enforced_Per_Step()
        {
            Assert.IsTrue(TransferRules.Available(TransferStages.Submitted, Roles.JE).Any(s => s.Action == "review"));
            Assert.IsFalse(TransferRules.Available(TransferStages.Submitted, Roles.OS).Any(s => s.Action == "review"));
            Assert.IsTrue(TransferRules.Available(TransferStages.Reviewed, Roles.OS).Any(s => s.Action == "pay"));
            Assert.IsFalse(TransferRules.Available(TransferStages.Reviewed, Roles.JE).Any(s => s.Action == "pay"));
            Assert.IsTrue(TransferRules.Available(TransferStages.PaymentVerified, Roles.AssistantCommissioner).Any(s => s.Action == "approve"));
            Assert.IsFalse(TransferRules.Available(TransferStages.PaymentVerified, Roles.OS).Any(s => s.Action == "approve"));
            Assert.AreEqual(0, TransferRules.Available(TransferStages.PaymentVerified, Roles.Staff).Count);
            Assert.IsFalse(TransferRules.CanCreate(Roles.Staff));
            Assert.IsFalse(TransferRules.CanCreate(Roles.Citizen));
        }

        [TestMethod] public void Recovery_Rules_Guard_Closure_And_History()
        {
            var r = new RecoveryCase { PropertyId = 1, Stage = RecoveryStages.Demand, OutstandingAmount = 1000, RecoveredAmount = 0 };
            Assert.AreEqual(0, Enumerable.Count(RegisterRules.ValidateRecovery(r, null)));
            r.Stage = RecoveryStages.Recovered; Assert.IsTrue(RegisterRules.ValidateRecovery(r, null).Count > 0, "recovered needs full amount");
            r.RecoveredAmount = 1000; r.RecoveryDate = new DateTime(2026, 2, 1); Assert.AreEqual(0, RegisterRules.ValidateRecovery(r, null).Count);
            r.RecoveredAmount = 1001; Assert.IsTrue(RegisterRules.ValidateRecovery(r, null).Count > 0, "over-recovery");
            var closed = new RecoveryCase { Stage = RecoveryStages.Closed, RecoveredAmount = 1000, OutstandingAmount = 1000 };
            r.RecoveredAmount = 1000; Assert.IsTrue(RegisterRules.ValidateRecovery(r, closed).Count > 0, "closed case is immutable");
            var open = new RecoveryCase { Stage = RecoveryStages.Demand, RecoveredAmount = 500, OutstandingAmount = 1000 };
            var back = new RecoveryCase { PropertyId = 1, Stage = RecoveryStages.Demand, OutstandingAmount = 1000, RecoveredAmount = 400, RecoveryDate = new DateTime(2026, 2, 1) };
            Assert.IsTrue(RegisterRules.ValidateRecovery(back, open).Count > 0, "recovered amount cannot decrease");
            var notice = new RecoveryCase { PropertyId = 1, Stage = RecoveryStages.NoticeIssued, OutstandingAmount = 10 };
            Assert.IsTrue(RegisterRules.ValidateRecovery(notice, null).Count > 0, "notice number/date required");
        }

        [TestMethod] public void Scheme_Rules_Validate_Mobile_Waiver_And_Dates()
        {
            var s = new SchemeApplication { PropertyId = 1, SchemeType = "X", ApplicantName = "A", ApplicationDate = new DateTime(2026, 1, 1), Status = SchemeStatuses.Pending, OriginalOutstanding = 100, WaivedAmount = 10 };
            Assert.AreEqual(0, RegisterRules.ValidateScheme(s).Count);
            s.WaivedAmount = 101; Assert.IsTrue(RegisterRules.ValidateScheme(s).Count > 0);
            s.WaivedAmount = 10; s.ApplicantMobile = "1010202030"; Assert.IsTrue(RegisterRules.ValidateScheme(s).Contains(RegisterRules.MobileMessage));
            s.ApplicantMobile = "9876543210"; s.DecisionDate = new DateTime(2025, 12, 31); Assert.IsTrue(RegisterRules.ValidateScheme(s).Count > 0);
        }
    }
}
