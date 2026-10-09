using System;
using SMC.BhoomiMalmatta.Models.Common;

namespace SMC.BhoomiMalmatta.Business.Numbering
{
    /// <summary>
    /// Formats application numbers from MR_CFC_ApplicationNumberConfigurations (Prefix, YearStrategy, SequenceLength, NextNumber).
    /// Live config: Prefix "2026", YearStrategy "Fixed" → no year is added; the prefix is used as stored (nothing hardcoded).
    /// </summary>
    public static class ApplicationNumberFormatter
    {
        public static Result<string> Format(string prefix, string yearStrategy, int sequenceLength, int number, DateTime localDate, string separator = "-")
        {
            if (string.IsNullOrWhiteSpace(prefix)) return Result.Failure<string>(new Error("NUMBER_PREFIX", "अर्ज क्रमांक संरचना (prefix) उपलब्ध नाही."));
            if (sequenceLength < 1 || sequenceLength > 12 || number < 1) return Result.Failure<string>(new Error("NUMBER_SEQUENCE", "अर्ज क्रमांक क्रम अवैध आहे."));
            var seq = number.ToString().PadLeft(sequenceLength, '0');
            var p = prefix.Trim();
            switch ((yearStrategy ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "fixed": return Result.Success(p + separator + seq);
                case "calendaryear": return Result.Success(p + separator + localDate.Year + separator + seq);
                case "financialyear":
                    var start = localDate.Month >= 4 ? localDate.Year : localDate.Year - 1;
                    return Result.Success(p + separator + start + "-" + ((start + 1) % 100).ToString("00") + separator + seq);
                default: return Result.Failure<string>(new Error("NUMBER_STRATEGY", "अर्ज क्रमांक वर्ष पद्धत ओळखता आली नाही."));
            }
        }
    }
}
