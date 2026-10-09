using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMC.BhoomiMalmatta.Business.Abstractions;

namespace SMC.BhoomiMalmatta.Business.PropertyRegister
{
    /// <summary>One master row after cleaning: the kept row of its duplicate set, with a readable label.</summary>
    public sealed class CleanMasterRow
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
        public string Key { get; set; }
        public string Label { get; set; }
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Pure functions that make MR_AST_MasterData usable without changing it:
    /// (1) MOJIBAKE - Marathi text whose UTF-8 bytes were read as Windows-1252 and stored ("à¤..." instead of Devanagari) is repaired for display;
    /// (2) DUPLICATES - rows sharing a Key (and, for groups that ask for it, a DisplayOrder) collapse to one row:
    ///     the row with a clean MarathiName wins, ties -> lowest Id; no clean row -> lowest Id with the repaired label; repair fails -> EnglishName.
    /// Only active, not deleted rows take part. The application never writes master rows.
    /// </summary>
    public static class MasterLabelCleaner
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Dictionary<char, byte> Cp1252Bytes = BuildCp1252Map();

        /// <summary>Character -> byte of Windows-1252; the five bytes cp1252 leaves undefined (81 8D 8F 90 9D) map from U+0081.. as Windows stores them.</summary>
        private static Dictionary<char, byte> BuildCp1252Map()
        {
            var map = new Dictionary<char, byte>();
            var cp = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            for (var b = 0; b < 256; b++)
            {
                char c;
                try { c = cp.GetString(new[] { (byte)b })[0]; }
                catch (DecoderFallbackException) { c = (char)b; }
                if (!map.ContainsKey(c)) map.Add(c, (byte)b);
            }
            for (var c = '\u0080'; c <= '\u009F'; c++) if (!map.ContainsKey(c)) map.Add(c, (byte)c);
            return map;
        }

        public static bool HasDevanagari(string s) { return s != null && s.Any(c => c >= 'ऀ' && c <= 'ॿ'); }

        /// <summary>Damaged = contains U+00E0 followed by U+00A4 or U+00A5 (the UTF-8 lead bytes E0 A4 / E0 A5 seen as cp1252) and no Devanagari letter.</summary>
        public static bool IsMojibake(string s)
        {
            if (string.IsNullOrEmpty(s) || HasDevanagari(s)) return false;
            for (var i = 0; i < s.Length - 1; i++)
                if (s[i] == 'à' && (s[i + 1] == '¤' || s[i + 1] == '¥')) return true;
            return false;
        }

        /// <summary>cp1252-encode, strict UTF-8 decode; accepted only when the result has a Devanagari letter and no U+FFFD.</summary>
        public static bool TryRepair(string s, out string repaired)
        {
            repaired = null;
            if (string.IsNullOrEmpty(s)) return false;
            var bytes = new byte[s.Length];
            for (var i = 0; i < s.Length; i++)
            {
                byte b;
                if (!Cp1252Bytes.TryGetValue(s[i], out b)) return false;
                bytes[i] = b;
            }
            string text;
            try { text = StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { return false; }
            if (!HasDevanagari(text) || text.IndexOf('�') >= 0) return false;
            repaired = text;
            return true;
        }

        public static bool IsCleanMarathi(string s) { return !string.IsNullOrWhiteSpace(s) && !IsMojibake(s); }

        /// <summary>Display label of one row: clean MarathiName, else the repaired MarathiName, else EnglishName, else Key.</summary>
        public static string Label(MasterOptionRow row)
        {
            if (IsCleanMarathi(row.MarathiName)) return row.MarathiName.Trim();
            string repaired;
            if (TryRepair(row.MarathiName, out repaired)) return repaired.Trim();
            if (!string.IsNullOrWhiteSpace(row.EnglishName)) return row.EnglishName.Trim();
            return (row.Key ?? string.Empty).Trim();
        }

        /// <summary>
        /// Collapses duplicates of ONE group: same Key always; same DisplayOrder too when <paramref name="collapseByDisplayOrder"/> (category/status groups only).
        /// Result ordered by DisplayOrder, then Id.
        /// </summary>
        public static List<CleanMasterRow> Select(IEnumerable<MasterOptionRow> rows, bool collapseByDisplayOrder)
        {
            var candidates = (rows ?? Enumerable.Empty<MasterOptionRow>())
                .Where(r => r != null && r.IsActive && !r.IsDeleted && !string.IsNullOrWhiteSpace(r.Key))
                .OrderBy(r => IsCleanMarathi(r.MarathiName) ? 0 : 1).ThenBy(r => r.Id);
            var kept = new List<MasterOptionRow>();
            foreach (var r in candidates)
            {
                var key = r.Key.Trim();
                if (kept.Any(k => string.Equals(k.Key.Trim(), key, StringComparison.Ordinal))) continue;
                if (collapseByDisplayOrder && kept.Any(k => k.DisplayOrder == r.DisplayOrder)) continue;
                kept.Add(r);
            }
            return kept.OrderBy(r => r.DisplayOrder).ThenBy(r => r.Id)
                .Select(r => new CleanMasterRow { Id = r.Id, GroupName = r.GroupName, Key = r.Key.Trim(), Label = Label(r), DisplayOrder = r.DisplayOrder }).ToList();
        }
    }
}
