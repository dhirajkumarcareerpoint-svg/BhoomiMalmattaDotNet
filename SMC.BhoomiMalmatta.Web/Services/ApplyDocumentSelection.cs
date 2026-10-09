using System;
using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>A file chosen in one of the step-3 upload boxes, after the existing validation (extension, MIME, signature, size).</summary>
    public sealed class SelectedDocument
    {
        public string DocumentType { get; set; }
        /// <summary>Labelled validation message, or null when the file is valid.</summary>
        public string Problem { get; set; }
        public FileCheck File { get; set; }
        public byte[] Content { get; set; }
        public bool IsValid { get { return Problem == null; } }
    }

    /// <summary>What the submit button must do with the selected and the already-staged files.</summary>
    public sealed class DocumentSelectionPlan
    {
        public DocumentSelectionPlan() { ToStage = new List<SelectedDocument>(); Missing = new List<string>(); Messages = new List<FieldError>(); }
        /// <summary>Valid selected files; each replaces an earlier staged file of the same type.</summary>
        public List<SelectedDocument> ToStage { get; private set; }
        /// <summary>Required types with nothing staged and nothing selected.</summary>
        public List<string> Missing { get; private set; }
        /// <summary>Validation problems of selected files, then "required" messages. Empty = the submission may continue.</summary>
        public List<FieldError> Messages { get; private set; }
        public bool CanContinue { get { return Messages.Count == 0; } }
    }

    /// <summary>
    /// Apply.aspx step 3: decides which selected files to stage when "अर्ज सादर करा" is pressed without the per-file "जोडा / बदला" button,
    /// which document types are still missing and which messages to show. No Page/Session dependency, no logging (no file names logged).
    /// </summary>
    public static class ApplyDocumentSelection
    {
        public const string IdentityLabel = "ओळखपत्र";
        public const string AddressLabel = "पत्त्याचा पुरावा";

        /// <summary>The documents every application needs, in display order.</summary>
        public static readonly KeyValuePair<string, string>[] Required =
        {
            new KeyValuePair<string, string>(DocumentTypes.IdentityProof, IdentityLabel),
            new KeyValuePair<string, string>(DocumentTypes.AddressProof, AddressLabel),
        };

        public static string Label(string documentType)
        {
            var match = Required.FirstOrDefault(x => x.Key == documentType);
            return match.Value ?? documentType;
        }

        public static string RequiredMessage(string label) { return label + " जोडणे आवश्यक आहे. कृपया फाईल निवडा."; }
        public static string OversizeMessage(string label) { return label + " फाईलचा आकार २० MB पेक्षा जास्त आहे."; }

        /// <summary>
        /// The existing staging validation, unchanged: size limit first (without reading the content), then FileValidator
        /// (extension, MIME, signature, size) with the application-attachment limit. <paramref name="readContent"/> is called only when the size is allowed.
        /// </summary>
        public static SelectedDocument Check(string documentType, string fileName, string contentType, long contentLength, Func<byte[]> readContent)
        {
            var label = Label(documentType);
            if (contentLength > FileValidator.MaxApplicationAttachmentBytes)
                return new SelectedDocument { DocumentType = documentType, Problem = OversizeMessage(label) };
            var bytes = readContent();
            var checkedFile = FileValidator.Validate(fileName, contentType, bytes, FileValidator.MaxApplicationAttachmentBytes);
            if (checkedFile.IsFailure) return new SelectedDocument { DocumentType = documentType, Problem = label + ": " + checkedFile.Error.Message };
            return new SelectedDocument { DocumentType = documentType, File = checkedFile.Value, Content = bytes };
        }

        /// <summary>
        /// For each required type: a selected file wins (valid -> stage it, replacing what was staged; invalid -> its specific message, the staged file
        /// stays); otherwise a staged file is used as it is; otherwise the type is missing and gets the "required" message.
        /// </summary>
        public static DocumentSelectionPlan Plan(IEnumerable<string> stagedTypes, IEnumerable<SelectedDocument> selected)
        {
            var staged = new HashSet<string>(stagedTypes ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var chosen = (selected ?? Enumerable.Empty<SelectedDocument>()).Where(x => x != null).ToList();
            var plan = new DocumentSelectionPlan();
            var required = new List<FieldError>();
            foreach (var item in Required)
            {
                var pick = chosen.LastOrDefault(x => x.DocumentType == item.Key);
                if (pick != null)
                {
                    if (pick.IsValid) plan.ToStage.Add(pick);
                    else plan.Messages.Add(new FieldError("Documents", pick.Problem));
                    continue;
                }
                if (staged.Contains(item.Key)) continue;
                plan.Missing.Add(item.Key);
                required.Add(new FieldError("Documents", RequiredMessage(item.Value)));
            }
            plan.Messages.AddRange(required);
            return plan;
        }
    }
}
