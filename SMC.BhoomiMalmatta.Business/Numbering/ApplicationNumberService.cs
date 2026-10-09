using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;

namespace SMC.BhoomiMalmatta.Business.Numbering
{
    /// <summary>
    /// Takes sequence numbers from the DB configuration and skips any that already exist (the live config says NextNumber=5 while 67
    /// applications exist, so a collision check is mandatory). The unique index on ApplicationNumber remains the final guard.
    /// </summary>
    public sealed class ApplicationNumberService
    {
        private readonly IApplicationNumberStore _store; private readonly IClock _clock; private readonly string _separator;
        public ApplicationNumberService(IApplicationNumberStore store, IClock clock, string separator = "-") { _store = store; _clock = clock; _separator = separator; }

        public Result<string> Next(int maxAttempts = 100)
        {
            for (var i = 0; i < maxAttempts; i++)
            {
                var r = _store.ReserveNext();
                if (r == null) return Result.Failure<string>(new Error("NUMBER_CONFIG_MISSING", "अर्ज क्रमांक संरचना सक्रिय नाही."));
                var f = ApplicationNumberFormatter.Format(r.Prefix, r.YearStrategy, r.SequenceLength, r.Number, _clock.UtcNow.AddHours(5.5), _separator);
                if (f.IsFailure) return f;
                if (!_store.Exists(f.Value)) return f;
            }
            return Result.Failure<string>(new Error("NUMBER_EXHAUSTED", "अर्ज क्रमांक तयार करता आला नाही. कृपया पुन्हा प्रयत्न करा."));
        }
    }
}
