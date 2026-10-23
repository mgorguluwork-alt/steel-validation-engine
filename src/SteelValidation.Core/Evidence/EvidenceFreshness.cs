using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelValidation.Core.Evidence
{
    /// <summary>The read-only evidence a production write may depend on.</summary>
    public enum EvidenceKind
    {
        NumberingReport = 0,
        DrawingInventory = 1,
        DrawingExport = 2
    }

    /// <summary>Stable result codes; the UI maps them to text.</summary>
    public static class EvidenceFreshnessCode
    {
        public const string Fresh = "Fresh";
        public const string Missing = "Missing";
        public const string WrongModel = "WrongModel";
        public const string ModelChangedSince = "ModelChangedSince";
        public const string StateUnknown = "StateUnknown";
        public const string TooOld = "TooOld";
        public const string FutureTimestamp = "FutureTimestamp";
        public const string InvalidStamp = "InvalidStamp";
    }

    /// <summary>
    /// What a numbering report, drawing inventory or export was taken against:
    /// the model identity, the moment it was read and an opaque model state token
    /// (for example a hash of GUID/Name/Profile/Position/Phase/UDA) read at that moment.
    /// </summary>
    public sealed class EvidenceStamp
    {
        public EvidenceStamp(EvidenceKind kind, string? modelIdentity, DateTime capturedUtc, string? modelStateToken)
        {
            Kind = kind;
            ModelIdentity = modelIdentity ?? string.Empty;
            CapturedUtc = capturedUtc;
            ModelStateToken = modelStateToken ?? string.Empty;
        }

        public EvidenceKind Kind { get; }
        public string ModelIdentity { get; }
        public DateTime CapturedUtc { get; }
        public string ModelStateToken { get; }
    }

    /// <summary>The live model as seen right now.</summary>
    public sealed class EvidenceContext
    {
        public EvidenceContext(string? modelIdentity, string? currentStateToken, DateTime nowUtc, TimeSpan maxAge, IEnumerable<EvidenceStamp>? stamps)
        {
            ModelIdentity = modelIdentity ?? string.Empty;
            CurrentStateToken = currentStateToken ?? string.Empty;
            NowUtc = nowUtc;
            MaxAge = maxAge;
            Stamps = (stamps ?? Array.Empty<EvidenceStamp>()).Where(s => s != null).ToArray();
        }

        public string ModelIdentity { get; }
        public string CurrentStateToken { get; }
        public DateTime NowUtc { get; }
        public TimeSpan MaxAge { get; }
        public IReadOnlyList<EvidenceStamp> Stamps { get; }
    }

    public sealed class EvidenceFreshnessResult
    {
        internal EvidenceFreshnessResult(EvidenceKind kind, string code)
        {
            Kind = kind;
            Code = code;
        }

        public EvidenceKind Kind { get; }
        public string Code { get; }
        public bool IsFresh => Code == EvidenceFreshnessCode.Fresh;
    }

    /// <summary>
    /// V4: stale numbering, inventory or export evidence is not evidence. Pure logic;
    /// it reads nothing from Tekla and anything it cannot prove fresh is stale (fail closed).
    /// </summary>
    public static class EvidenceFreshnessRules
    {
        public static EvidenceFreshnessResult Check(EvidenceKind kind, EvidenceContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // several stamps of one kind: every one must be fresh; the newest decides nothing on its own
            var stamps = context.Stamps.Where(s => s.Kind == kind).ToList();
            if (stamps.Count == 0)
                return new EvidenceFreshnessResult(kind, EvidenceFreshnessCode.Missing);

            foreach (var s in stamps)
            {
                var code = CheckOne(s, context);
                if (code != EvidenceFreshnessCode.Fresh)
                    return new EvidenceFreshnessResult(kind, code);
            }
            return new EvidenceFreshnessResult(kind, EvidenceFreshnessCode.Fresh);
        }

        /// <summary>One result per required kind, in the order given. Empty required list = nothing to check.</summary>
        public static IReadOnlyList<EvidenceFreshnessResult> CheckAll(IEnumerable<EvidenceKind> required, EvidenceContext? context)
        {
            if (required == null) throw new ArgumentNullException(nameof(required));
            var kinds = required.Distinct().ToList();
            if (context == null)
                return kinds.Select(k => new EvidenceFreshnessResult(k, EvidenceFreshnessCode.Missing)).ToList();
            return kinds.Select(k => Check(k, context)).ToList();
        }

        private static string CheckOne(EvidenceStamp s, EvidenceContext c)
        {
            if (string.IsNullOrWhiteSpace(s.ModelIdentity) || s.CapturedUtc == default(DateTime) || s.CapturedUtc.Kind == DateTimeKind.Local)
                return EvidenceFreshnessCode.InvalidStamp;
            if (c.MaxAge <= TimeSpan.Zero || c.NowUtc == default(DateTime))
                return EvidenceFreshnessCode.InvalidStamp;

            if (string.IsNullOrWhiteSpace(c.ModelIdentity) ||
                !string.Equals(s.ModelIdentity, c.ModelIdentity, StringComparison.Ordinal))
                return EvidenceFreshnessCode.WrongModel;

            // without a state token on both sides "nothing changed" cannot be shown
            if (string.IsNullOrWhiteSpace(s.ModelStateToken) || string.IsNullOrWhiteSpace(c.CurrentStateToken))
                return EvidenceFreshnessCode.StateUnknown;
            if (!string.Equals(s.ModelStateToken, c.CurrentStateToken, StringComparison.Ordinal))
                return EvidenceFreshnessCode.ModelChangedSince;

            if (s.CapturedUtc > c.NowUtc)
                return EvidenceFreshnessCode.FutureTimestamp;
            if (c.NowUtc - s.CapturedUtc > c.MaxAge)
                return EvidenceFreshnessCode.TooOld;

            return EvidenceFreshnessCode.Fresh;
        }
    }
}
