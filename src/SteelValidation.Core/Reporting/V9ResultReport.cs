using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SteelValidation.Core.Drawings;

namespace SteelValidation.Core.Reporting
{
    public sealed class V9ResultReport
    {
        internal V9ResultReport(QaReport document, IReadOnlyList<string> blockers, bool exportMetadataComplete)
        { Document = document; Blockers = blockers; ExportMetadataComplete = exportMetadataComplete; }
        public QaReport Document { get; }
        public IReadOnlyList<string> Blockers { get; }
        public bool ExportMetadataComplete { get; }
        public bool IsReadyForRelease => Blockers.Count == 0;
    }

    /// <summary>Pure final report. File metadata and NC header checks are not independent production validation.</summary>
    public static class V9ResultReportBuilder
    {
        public const string QaMissing = "QaMissing";
        public const string QaNotRun = "QaNotRun";
        public const string QaFindingsPresent = "QaFindingsPresent";
        public const string ModelIdentityMissing = "ModelIdentityMissing";
        public const string ExportModelMismatch = "ExportModelMismatch";
        public const string ProductionVerificationMissing = "ProductionVerificationMissing";
        public const string ExportKindMissing = "ExportKindMissing";
        public const string InvalidTimestamp = "InvalidReportTimestamp";
        public const string QaDrawingBindingUnverified = "QaDrawingBindingUnverified";

        /// <summary>All four export kinds are required for a complete V9 report. No Tekla calls or files are written here.</summary>
        public static V9ResultReport Build(string modelIdentity, QaReport? qa, ExportVerificationReport? exports,
            DrawingReadinessReport? drawings, bool ownerReleaseConfirmed, DateTime timestampUtc,
            bool productionVerificationConfirmed = false)
        {
            // QA and drawing reports currently carry no model/state identity. Caller booleans cannot supply that binding.
            var blockers = new List<string> { QaDrawingBindingUnverified };
            if (string.IsNullOrWhiteSpace(modelIdentity)) blockers.Add(ModelIdentityMissing);
            if (timestampUtc.Kind != DateTimeKind.Utc || timestampUtc == default(DateTime)) blockers.Add(InvalidTimestamp);
            if (qa == null) blockers.Add(QaMissing);
            else
            {
                if (qa.NotRunChecks.Count > 0) blockers.Add(QaNotRun);
                // Findings need review; this report does not silently waive informational or warning rows.
                if (qa.Rows.Count > 0) blockers.Add(QaFindingsPresent);
            }
            blockers.AddRange(FinalDrawingGate.Blockers(drawings, exports, ownerReleaseConfirmed));
            if (exports != null)
            {
                if (!string.Equals(exports.ModelIdentity, modelIdentity, StringComparison.Ordinal)) blockers.Add(ExportModelMismatch);
                foreach (ExportKind kind in Enum.GetValues(typeof(ExportKind)))
                    if (!exports.Items.Any(i => i.Kind == kind)) blockers.Add(ExportKindMissing + ":" + kind);
            }
            if (!productionVerificationConfirmed) blockers.Add(ProductionVerificationMissing);
            blockers = blockers.Distinct(StringComparer.Ordinal).ToList();

            var rows = qa?.Rows.ToList() ?? new List<QaReportRow>();
            if (exports != null)
                foreach (var item in exports.Items)
                    rows.Add(new QaReportRow("EXPORT", item.Kind.ToString(), item.IsOk ? "PASS" : "FAIL", Array.Empty<int>(),
                        string.Empty, string.Empty, item.Key, item.Code));
            foreach (var blocker in blockers)
                rows.Add(new QaReportRow("V9", "BLOCKER", "BLOCKED", Array.Empty<int>(), string.Empty, string.Empty, string.Empty, blocker));

            var summary = new StringBuilder();
            summary.AppendLine("STEEL VALIDATION ENGINE - V9 SONUC RAPORU");
            summary.AppendLine("Model: " + (string.IsNullOrWhiteSpace(modelIdentity) ? "BILINMIYOR" : modelIdentity));
            summary.AppendLine("UTC: " + timestampUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            summary.AppendLine(blockers.Count == 0 ? "SONUC: Sunulan onay ve dogrulama kanitlariyla serbest birakmaya hazir." : "SONUC: BLOKE / DOGRULANAMADI");
            summary.AppendLine("QA: " + (qa == null ? "CALISTIRILMADI" : qa.Rows.Count.ToString(CultureInfo.InvariantCulture) + " bulgu; " + qa.NotRunChecks.Count.ToString(CultureInfo.InvariantCulture) + " kontrol calistirilmadi"));
            summary.AppendLine("Ciktilar: " + (exports == null ? "DOGRULANAMADI" : exports.OkCount.ToString(CultureInfo.InvariantCulture) + " metadata kontrolu gecti; " + exports.ProblemCount.ToString(CultureInfo.InvariantCulture) + " sorun"));
            summary.AppendLine("Sinir: QA ve cizim raporlarinin model/durum baglari dogrulanamadi; bu rapor serbest birakma yetkisi vermez.");
            summary.AppendLine("Sinir: PDF/DWG icerigi ve malzeme listesi adetleri bu kontrol tarafindan dogrulanmaz; NC yalniz baslik denetimidir.");
            summary.AppendLine("Bagimsiz uretim dogrulamasi: " + (productionVerificationConfirmed ? "DISARIDAN TEYIT EDILDI" : "DOGRULANAMADI"));
            summary.AppendLine("Yetkili serbest birakma onayi: " + (ownerReleaseConfirmed ? "TEYIT EDILDI" : "YOK"));
            foreach (var blocker in blockers) summary.AppendLine("- " + blocker);
            var notRun = qa?.NotRunChecks.ToArray() ?? QaReportBuilder.Checks.ToArray();
            var document = new QaReport(rows.AsReadOnly(), Array.AsReadOnly(notRun), QaReportBuilder.ToCsv(rows), summary.ToString());
            return new V9ResultReport(document, blockers.AsReadOnly(), exports?.IsComplete == true);
        }
    }
}
