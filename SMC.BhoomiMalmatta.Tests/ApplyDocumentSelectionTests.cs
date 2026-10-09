using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Tests.Support;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Tests
{
    /// <summary>Apply.aspx step 3: submit with files selected but not yet added with "जोडा / बदला".</summary>
    [TestClass]
    public class ApplyDocumentSelectionTests
    {
        private const string Identity = DocumentTypes.IdentityProof;
        private const string Address = DocumentTypes.AddressProof;
        private static readonly byte[] Exe = { 0x4D, 0x5A, 0x90, 0x00 };

        private static SelectedDocument ValidPdf(string type, string name = "file.pdf")
        { return ApplyDocumentSelection.Check(type, name, "application/pdf", TestData.Pdf.Length, () => TestData.Pdf); }

        private static string[] Texts(DocumentSelectionPlan plan) { return plan.Messages.Select(m => m.Message).ToArray(); }

        [TestMethod]
        public void Nothing_Staged_And_Both_Selected_Stages_Both_Without_A_Required_Error()
        {
            var plan = ApplyDocumentSelection.Plan(new string[0], new[] { ValidPdf(Identity, "id.pdf"), ValidPdf(Address, "address.pdf") });
            CollectionAssert.AreEqual(new[] { Identity, Address }, plan.ToStage.Select(x => x.DocumentType).ToArray());
            Assert.IsTrue(plan.ToStage.All(x => x.IsValid && x.Content != null && x.File != null && !string.IsNullOrEmpty(x.File.StoredName)));
            Assert.AreEqual(0, plan.Missing.Count); Assert.AreEqual(0, plan.Messages.Count); Assert.IsTrue(plan.CanContinue);
        }

        [TestMethod]
        public void One_Staged_And_The_Other_Selected_Stages_Only_The_Selected_One()
        {
            var plan = ApplyDocumentSelection.Plan(new[] { Identity }, new[] { ValidPdf(Address) });
            CollectionAssert.AreEqual(new[] { Address }, plan.ToStage.Select(x => x.DocumentType).ToArray());
            Assert.AreEqual(0, plan.Messages.Count); Assert.AreEqual(0, plan.Missing.Count);
        }

        [TestMethod]
        public void Both_Staged_And_None_Selected_Changes_Nothing()
        {
            var plan = ApplyDocumentSelection.Plan(new[] { Identity, Address }, new SelectedDocument[0]);
            Assert.AreEqual(0, plan.ToStage.Count); Assert.AreEqual(0, plan.Missing.Count); Assert.AreEqual(0, plan.Messages.Count); Assert.IsTrue(plan.CanContinue);
        }

        [TestMethod]
        public void Nothing_Staged_And_Nothing_Selected_Gives_Two_Required_Messages_With_The_New_Wording()
        {
            var plan = ApplyDocumentSelection.Plan(null, null);
            CollectionAssert.AreEqual(new[] { "ओळखपत्र जोडणे आवश्यक आहे. कृपया फाईल निवडा.", "पत्त्याचा पुरावा जोडणे आवश्यक आहे. कृपया फाईल निवडा." }, Texts(plan));
            CollectionAssert.AreEqual(new[] { Identity, Address }, plan.Missing);
            Assert.IsFalse(plan.CanContinue);
            Assert.IsFalse(Texts(plan).Any(t => t.Contains("जोडा") || t.Contains("बदला")), "no mention of the intermediate button");
        }

        [TestMethod]
        public void A_Selected_File_Replaces_The_Staged_File_Of_The_Same_Type()
        {
            var plan = ApplyDocumentSelection.Plan(new[] { Identity, Address }, new[] { ValidPdf(Identity, "new-id.pdf") });
            Assert.AreEqual(1, plan.ToStage.Count); Assert.AreEqual(Identity, plan.ToStage[0].DocumentType); Assert.AreEqual("new-id.pdf", plan.ToStage[0].File.SafeDisplayName);
            Assert.AreEqual(0, plan.Messages.Count);
        }

        [DataTestMethod]
        [DataRow("bad-extension")]
        [DataRow("oversize")]
        [DataRow("bad-signature")]
        [DataRow("mime-mismatch")]
        public void An_Invalid_Selected_File_Gives_Its_Specific_Message_Not_The_Generic_One(string kind)
        {
            var read = false;
            SelectedDocument selected;
            switch (kind)
            {
                case "bad-extension": selected = ApplyDocumentSelection.Check(Identity, "id.exe", "application/octet-stream", Exe.Length, () => { read = true; return Exe; }); break;
                case "oversize": selected = ApplyDocumentSelection.Check(Identity, "id.pdf", "application/pdf", FileValidator.MaxApplicationAttachmentBytes + 1, () => { read = true; return TestData.Pdf; }); break;
                case "bad-signature": selected = ApplyDocumentSelection.Check(Identity, "id.pdf", "application/pdf", Exe.Length, () => { read = true; return Exe; }); break;
                default: selected = ApplyDocumentSelection.Check(Identity, "id.png", "application/pdf", TestData.Pdf.Length, () => { read = true; return TestData.Pdf; }); break;
            }
            Assert.IsFalse(selected.IsValid, kind);
            var expected = kind == "oversize" ? "ओळखपत्र फाईलचा आकार २० MB पेक्षा जास्त आहे."
                : "ओळखपत्र: " + FileValidator.Validate(kind == "bad-extension" ? "id.exe" : kind == "bad-signature" ? "id.pdf" : "id.png", kind == "mime-mismatch" || kind == "bad-signature" ? "application/pdf" : "application/octet-stream", kind == "mime-mismatch" ? TestData.Pdf : Exe, FileValidator.MaxApplicationAttachmentBytes).Error.Message;
            Assert.AreEqual(expected, selected.Problem, kind);
            Assert.AreEqual(kind != "oversize", read, "an oversize file is refused before its content is read");

            // Identity invalid, address staged: the specific message only - no generic "required" text for identity, and the address is fine.
            var plan = ApplyDocumentSelection.Plan(new[] { Address }, new[] { selected });
            CollectionAssert.AreEqual(new[] { expected }, Texts(plan));
            Assert.AreEqual(0, plan.ToStage.Count); Assert.AreEqual(0, plan.Missing.Count); Assert.IsFalse(plan.CanContinue);
            Assert.IsFalse(Texts(plan).Any(t => t.Contains("जोडणे आवश्यक आहे")));
        }

        [TestMethod]
        public void An_Invalid_Selection_Keeps_The_Earlier_Staged_File_And_A_Valid_One_Is_Still_Staged()
        {
            var bad = ApplyDocumentSelection.Check(Identity, "id.exe", "application/octet-stream", Exe.Length, () => Exe);
            var plan = ApplyDocumentSelection.Plan(new[] { Identity }, new[] { bad, ValidPdf(Address) });
            CollectionAssert.AreEqual(new[] { Address }, plan.ToStage.Select(x => x.DocumentType).ToArray(), "the valid address file is staged so it survives the error");
            Assert.AreEqual(1, plan.Messages.Count); StringAssert.StartsWith(plan.Messages[0].Message, "ओळखपत्र: ");
            Assert.AreEqual(0, plan.Missing.Count, "the staged identity file is not discarded");
            // One invalid and one missing: both messages, the specific one first.
            var mixed = ApplyDocumentSelection.Plan(new string[0], new[] { bad });
            CollectionAssert.AreEqual(new[] { bad.Problem, "पत्त्याचा पुरावा जोडणे आवश्यक आहे. कृपया फाईल निवडा." }, Texts(mixed));
        }

        [TestMethod]
        public void Button_Messages_Are_Unchanged_And_Submit_Is_Wired_Before_The_Staged_Read()
        {
            Assert.AreEqual("पत्त्याचा पुरावा फाईलचा आकार २० MB पेक्षा जास्त आहे.", ApplyDocumentSelection.Check(Address, "a.pdf", "application/pdf", FileValidator.MaxApplicationAttachmentBytes + 1, () => TestData.Pdf).Problem);
            Assert.IsTrue(ValidPdf(Address).IsValid);
            var code = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Apply.aspx.cs"));
            var submit = code.Substring(code.IndexOf("protected void btnSubmit_Click", StringComparison.Ordinal));
            submit = submit.Substring(0, submit.IndexOf("private bool ShowCompletedApplication", StringComparison.Ordinal));
            var stage = submit.IndexOf("StageSelectedAtSubmit()", StringComparison.Ordinal);
            Assert.IsTrue(stage > 0 && stage < submit.IndexOf("GetStagedDocumentUploads(problems)", StringComparison.Ordinal), "selected files are staged before the staged files are read");
            Assert.IsTrue(stage < submit.IndexOf("if (!Page.IsValid)", StringComparison.Ordinal), "staged even when the declaration is not ticked yet");
            Assert.IsTrue(submit.IndexOf("ClearStagedAttachments(Session)", StringComparison.Ordinal) > submit.IndexOf("var ok = result.Item1;", StringComparison.Ordinal), "staged files are cleared only after a successful submission");
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(code, "ClearStagedAttachments\\(Session\\)").Count);
            StringAssert.Contains(code, "ApplyDocumentSelection.RequiredMessage(item.Label)");
            StringAssert.Contains(code, "lblIdentityFileName.Text = identity == null ? string.Empty : \"जोडलेली फाईल: \" + Enc(identity.FileName);");
            StringAssert.Contains(code, "protected void btnSaveIdentity_Click(object sender, EventArgs e) { StageSelectedAttachments(); }");
            var helper = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Services", "ApplyDocumentSelection.cs"));
            Assert.IsFalse(helper.Contains("ErrorLogger") || helper.Contains("System.Web"), "no logging and no Page/Session dependency");
        }
    }
}
