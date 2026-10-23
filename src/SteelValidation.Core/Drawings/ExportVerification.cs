using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SteelValidation.Core.Evidence;

namespace SteelValidation.Core.Drawings
{
    public enum ExportKind
    {
        DrawingPdf = 0,
        NcFile = 1,
        DrawingDwg = 2,
        MaterialList = 3
    }

    /// <summary>Stable result codes of one export check.</summary>
    public static class ExportCode
    {
        public const string Ok = "Ok";
        public const string Missing = "Missing";
        public const string EmptyFile = "EmptyFile";
        public const string HashMissing = "HashMissing";
        public const string Stale = "Stale";
        public const string WrongModel = "WrongModel";
        public const string Duplicate = "Duplicate";
        public const string Unexpected = "Unexpected";
        public const string NcUnparsable = "NcUnparsable";
        public const string NcNoStartBlock = "NcNoStartBlock";
        public const string NcNoEndBlock = "NcNoEndBlock";
        public const string NcMarkMismatch = "NcMarkMismatch";
        public const string NcProfileMismatch = "NcProfileMismatch";
        public const string NcLengthMismatch = "NcLengthMismatch";
        public const string NcExpectationMissing = "NcExpectationMissing";
    }

    /// <summary>One required output: PDF/DWG per drawing mark, NC per part position, or material list per report key.</summary>
    public sealed class ExpectedExport
    {
        public ExpectedExport(ExportKind kind, string key, string? profile = null, double? lengthMm = null)
        {
            if (!Enum.IsDefined(typeof(ExportKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An export key is required.", nameof(key));
            Kind = kind;
            Key = key.Trim();
            Profile = (profile ?? string.Empty).Trim();
            LengthMm = lengthMm;
        }

        public ExportKind Kind { get; }

        /// <summary>Drawing mark (PDF/DWG), part position (NC), or stable material-list report key.</summary>
        public string Key { get; }

        /// <summary>NC only: the profile the model says the part has.</summary>
        public string Profile { get; }

        /// <summary>NC only: the length the model says the part has.</summary>
        public double? LengthMm { get; }
    }

    /// <summary>One file as found on disk by the adapter. The Core never reads files itself.</summary>
    public sealed class ExportRecord
    {
        public ExportRecord(ExportKind kind, string key, string? fileName, bool exists, long sizeBytes, string? sha256,
            DateTime capturedUtc, string? modelIdentity, string? modelStateToken, string? ncText = null)
        {
            Kind = kind;
            Key = (key ?? string.Empty).Trim();
            FileName = fileName ?? string.Empty;
            Exists = exists;
            SizeBytes = sizeBytes;
            Sha256 = (sha256 ?? string.Empty).Trim();
            CapturedUtc = capturedUtc;
            ModelIdentity = modelIdentity ?? string.Empty;
            ModelStateToken = modelStateToken ?? string.Empty;
            NcText = ncText;
        }

        public ExportKind Kind { get; }
        public string Key { get; }
        public string FileName { get; }
        public bool Exists { get; }
        public long SizeBytes { get; }
        public string Sha256 { get; }
        public DateTime CapturedUtc { get; }
        public string ModelIdentity { get; }
        public string ModelStateToken { get; }
        public string? NcText { get; }
    }

    public sealed class NcSummary
    {
        internal NcSummary(string pieceMark, string profile, double? lengthMm, bool hasStart, bool hasEnd)
        {
            PieceMark = pieceMark;
            Profile = profile;
            LengthMm = lengthMm;
            HasStartBlock = hasStart;
            HasEndBlock = hasEnd;
        }

        public string PieceMark { get; }
        public string Profile { get; }
        public double? LengthMm { get; }
        public bool HasStartBlock { get; }
        public bool HasEndBlock { get; }
    }

    /// <summary>
    /// Reads the header of a DSTV NC1 file: after the "ST" block start, comment lines ("**") are skipped and the fields are, in order,
    /// order, drawing, phase, piece mark, grade, quantity, profile, profile code, length. ASSUMPTION: this is the DSTV layout; it is
    /// checked only against mock text until a real exported file is read live.
    /// </summary>
    public static class NcHeaderReader
    {
        public static NcSummary Read(string? text)
        {
            var lines = (text ?? string.Empty).Replace("\r", "").Split('\n');
            var start = Array.FindIndex(lines, l => l.Trim().Equals("ST", StringComparison.OrdinalIgnoreCase));
            var hasEnd = lines.Any(l => l.Trim().Equals("EN", StringComparison.OrdinalIgnoreCase));
            if (start < 0) return new NcSummary(string.Empty, string.Empty, null, false, hasEnd);

            var fields = lines.Skip(start + 1).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("**", StringComparison.Ordinal)).Take(9).ToList();
            string F(int i) => i < fields.Count ? fields[i] : string.Empty;
            double? length = double.TryParse(F(8), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
            return new NcSummary(F(3), F(6), length, true, hasEnd);
        }
    }

    public sealed class ExportCheckItem
    {
        internal ExportCheckItem(ExportKind kind, string key, string code)
        {
            Kind = kind;
            Key = key;
            Code = code;
        }

        public ExportKind Kind { get; }
        public string Key { get; }
        public string Code { get; }
        public bool IsOk => Code == ExportCode.Ok;
    }

    public sealed class ExportVerificationReport
    {
        internal ExportVerificationReport(IReadOnlyList<ExportCheckItem> items, EvidenceContext? context)
        {
            Items = items;
            ModelIdentity = context?.ModelIdentity ?? string.Empty;
            ModelStateToken = context?.CurrentStateToken ?? string.Empty;
            VerifiedUtc = context?.NowUtc ?? default(DateTime);
        }

        public string ModelIdentity { get; }
        public string ModelStateToken { get; }
        public DateTime VerifiedUtc { get; }

        public IReadOnlyList<ExportCheckItem> Items { get; }
        public int OkCount => Items.Count(i => i.IsOk);
        public int ProblemCount => Items.Count - OkCount;

        /// <summary>True only when something was expected and every expected export is Ok and nothing unexpected was found.
        /// It says the supplied file metadata is complete and current; NC headers also matched expectations.
        /// PDF/DWG contents and material-list quantities are NOT independently validated, and this is NOT release approval.</summary>
        public bool IsComplete => Items.Count > 0 && ProblemCount == 0;
    }

    /// <summary>V9: are the expected PDF, DWG, material-list and NC files present and current for THIS model state?
    /// Pure logic over what the adapter read from disk. A SHA-256 match alone proves nothing about the model (audit D-07).</summary>
    public static class ExportVerifier
    {
        private const double LengthToleranceMm = 0.5;

        public static ExportVerificationReport Verify(IEnumerable<ExpectedExport> expected, IEnumerable<ExportRecord> found, EvidenceContext? context)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (found == null) throw new ArgumentNullException(nameof(found));

            var expectedList = expected.ToList();
            var records = found.ToList();
            if (expectedList.Any(e => e == null) || records.Any(r => r == null))
                throw new ArgumentException("Export collections cannot contain null entries.");
            var items = new List<ExportCheckItem>();

            foreach (var e in expectedList.OrderBy(x => x.Kind).ThenBy(x => x.Key, StringComparer.Ordinal))
            {
                var matches = records.Where(r => r.Kind == e.Kind && string.Equals(r.Key, e.Key, StringComparison.Ordinal)).ToList();
                items.Add(new ExportCheckItem(e.Kind, e.Key, expectedList.Count(x => x.Kind == e.Kind && string.Equals(x.Key, e.Key, StringComparison.Ordinal)) > 1 ? ExportCode.Duplicate
                    : matches.Count == 0 ? ExportCode.Missing
                    : matches.Count > 1 ? ExportCode.Duplicate : CheckOne(e, matches[0], context)));
            }

            foreach (var r in records.Where(r => !expectedList.Any(e => e.Kind == r.Kind && string.Equals(e.Key, r.Key, StringComparison.Ordinal)))
                         .OrderBy(r => r.Kind).ThenBy(r => r.Key, StringComparer.Ordinal))
                items.Add(new ExportCheckItem(r.Kind, r.Key, ExportCode.Unexpected));

            return new ExportVerificationReport(items.AsReadOnly(), context);
        }

        private static string CheckOne(ExpectedExport e, ExportRecord r, EvidenceContext? ctx)
        {
            if (!r.Exists) return ExportCode.Missing;
            if (r.SizeBytes <= 0) return ExportCode.EmptyFile;
            if (r.Sha256.Length != 64 || r.Sha256.Any(c => !Uri.IsHexDigit(c))) return ExportCode.HashMissing;

            if (ctx == null) return ExportCode.Stale;
            var stamp = new EvidenceStamp(EvidenceKind.DrawingExport, r.ModelIdentity, r.CapturedUtc, r.ModelStateToken);
            var fresh = EvidenceFreshnessRules.Check(EvidenceKind.DrawingExport,
                new EvidenceContext(ctx.ModelIdentity, ctx.CurrentStateToken, ctx.NowUtc, ctx.MaxAge, new[] { stamp }));
            if (!fresh.IsFresh)
                return fresh.Code == EvidenceFreshnessCode.WrongModel ? ExportCode.WrongModel : ExportCode.Stale;

            return e.Kind == ExportKind.NcFile ? CheckNc(e, r) : ExportCode.Ok;
        }

        private static string CheckNc(ExpectedExport e, ExportRecord r)
        {
            if (string.IsNullOrWhiteSpace(r.NcText)) return ExportCode.NcUnparsable;
            if (e.Profile.Length == 0 || !e.LengthMm.HasValue) return ExportCode.NcExpectationMissing;

            var nc = NcHeaderReader.Read(r.NcText);
            if (!nc.HasStartBlock) return ExportCode.NcNoStartBlock;
            if (!nc.HasEndBlock) return ExportCode.NcNoEndBlock;
            if (!string.Equals(nc.PieceMark, e.Key, StringComparison.Ordinal)) return ExportCode.NcMarkMismatch;
            if (!string.Equals(nc.Profile, e.Profile, StringComparison.OrdinalIgnoreCase)) return ExportCode.NcProfileMismatch;
            if (!nc.LengthMm.HasValue) return ExportCode.NcUnparsable;
            return Math.Abs(nc.LengthMm.Value - e.LengthMm.Value) <= LengthToleranceMm ? ExportCode.Ok : ExportCode.NcLengthMismatch;
        }
    }

    /// <summary>
    /// The last check before a drawing set may be called ready for release: drawings current (DrawingReadinessReport), exports complete
    /// (ExportVerificationReport) and an explicit owner release confirmation. None of the three replaces another.
    /// </summary>
    public static class FinalDrawingGate
    {
        public const string DrawingsNotCurrent = "DrawingsNotCurrent";
        public const string ExportsIncomplete = "ExportsIncomplete";
        public const string ReleaseNotConfirmed = "ReleaseNotConfirmed";

        public static IReadOnlyList<string> Blockers(DrawingReadinessReport? drawings, ExportVerificationReport? exports, bool ownerReleaseConfirmed)
        {
            var blockers = new List<string>();
            if (drawings == null || !drawings.IsInventoryCurrent) blockers.Add(DrawingsNotCurrent);
            if (exports == null || !exports.IsComplete) blockers.Add(ExportsIncomplete);
            if (!ownerReleaseConfirmed) blockers.Add(ReleaseNotConfirmed);
            return blockers;
        }

        public static bool IsReadyForRelease(DrawingReadinessReport? drawings, ExportVerificationReport? exports, bool ownerReleaseConfirmed) =>
            Blockers(drawings, exports, ownerReleaseConfirmed).Count == 0;
    }
}
