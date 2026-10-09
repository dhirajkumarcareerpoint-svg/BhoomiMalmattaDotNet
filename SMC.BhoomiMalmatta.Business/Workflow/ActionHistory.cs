using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace SMC.BhoomiMalmatta.Business.Workflow
{
    /// <summary>One immutable history entry stored inside TR_CFC_DemandApplicationZoneWorkflows.ActionHistoryJson.</summary>
    public sealed class HistoryEntry
    {
        public int Sequence { get; set; }
        public int ApplicationId { get; set; }
        public string ApplicationNumber { get; set; }
        public string FromRole { get; set; }
        public string ToRole { get; set; }
        public string Action { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public string PreviousStage { get; set; }
        public string NewStage { get; set; }
        public string Remarks { get; set; }
        public int? ActionByUserId { get; set; }
        public string ActionByUserName { get; set; }
        /// <summary>ISO-8601 UTC ("O" format).</summary>
        public string ActionAtUtc { get; set; }
        public string IpAddress { get; set; }
        public string CorrelationId { get; set; }
        public string AttachmentReference { get; set; }
        /// <summary>Applicant payment submission only: the UTR and the payment date (yyyy-MM-dd) of THIS submission, so an earlier UTR stays visible after a rejection and resubmission.</summary>
        public string PaymentReference { get; set; }
        public string PaymentDate { get; set; }
        public string PreviousHash { get; set; }
        public string Hash { get; set; }

        public HistoryEntry Clone() { return (HistoryEntry)MemberwiseClone(); }
        public DateTime AtUtc
        {
            get
            {
                DateTime d;
                return DateTime.TryParse(ActionAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out d) ? d : DateTime.MinValue;
            }
        }
    }

    /// <summary>
    /// Append-only, hash-chained history. The existing database has no history table, so ActionHistoryJson is the store;
    /// the SHA-256 chain makes any edit or deletion of past entries detectable.
    /// </summary>
    public static class ActionHistory
    {
        public const string Genesis = "GENESIS";
        private static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }; }

        public static IList<HistoryEntry> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<HistoryEntry>();
            var list = Json().Deserialize<List<HistoryEntry>>(json);
            return list ?? new List<HistoryEntry>();
        }

        public static string ComputeHash(HistoryEntry e)
        {
            var material = string.Join("|", new[] {
                e.PreviousHash, e.Sequence.ToString(), e.ApplicationId.ToString(), e.ApplicationNumber, e.FromRole, e.ToRole, e.Action,
                e.PreviousStatus, e.NewStatus, e.PreviousStage, e.NewStage, e.Remarks,
                e.ActionByUserId.HasValue ? e.ActionByUserId.Value.ToString() : "", e.ActionByUserName,
                e.ActionAtUtc, e.IpAddress, e.CorrelationId, e.AttachmentReference });
            // Appended only when present, so every entry written before these fields existed keeps its original hash.
            if (e.PaymentReference != null || e.PaymentDate != null) material += "|" + e.PaymentReference + "|" + e.PaymentDate;
            using (var sha = SHA256.Create())
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(material)));
        }

        public static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", string.Empty); }

        /// <summary>Returns the new JSON with <paramref name="draft"/> appended. Existing entries are never rewritten.</summary>
        public static string Append(string existingJson, HistoryEntry draft)
        {
            var list = Parse(existingJson).ToList();
            var entry = draft.Clone();
            entry.Sequence = list.Count + 1;
            entry.PreviousHash = list.Count == 0 ? Genesis : list[list.Count - 1].Hash;
            entry.Hash = string.Empty;
            entry.Hash = ComputeHash(entry);
            list.Add(entry);
            return Json().Serialize(list);
        }

        public static bool VerifyChain(string json)
        {
            var prev = Genesis; var seq = 1;
            foreach (var e in Parse(json))
            {
                if (e.Sequence != seq++ || e.PreviousHash != prev || e.Hash != ComputeHash(e)) return false;
                prev = e.Hash;
            }
            return true;
        }

        /// <summary>True when newJson keeps every entry of oldJson unchanged (only appends) and the chain is intact.</summary>
        public static bool IsAppendOnly(string oldJson, string newJson)
        {
            var o = Parse(oldJson); var n = Parse(newJson);
            if (n.Count < o.Count) return false;
            for (var i = 0; i < o.Count; i++)
                if (o[i].Hash != n[i].Hash || o[i].Sequence != n[i].Sequence) return false;
            return VerifyChain(newJson);
        }
    }
}
