using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SteelValidation.Abstractions;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Core.Reporting
{
    /// <summary>
    /// Builds the V9 read-only QA report (CSV and text summary) from existing
    /// check results. Pure and deterministic: it does not read the clock, the
    /// culture or the environment, and never adds findings of its own. A
    /// missing result is reported as not run and contributes no rows.
    /// </summary>
    /// <remarks>
    /// One row is written per entry of the result lists:
    /// MODEL — every name-empty item, length finding and consistency finding;
    /// GEOMETRY — every finding;
    /// NUMBERING — every part and assembly position finding, every gap and
    /// every part whose numbering status is not up to date (the same rows the
    /// numbering grid shows).
    /// </remarks>
    public static class QaReportBuilder
    {
        public const string ModelCheck = "MODEL";
        public const string GeometryCheck = "GEOMETRY";
        public const string NumberingCheck = "NUMBERING";

        public const string NameEmptyRule = "NAME_EMPTY";
        public const string LengthRule = "LENGTH";
        public const string ConsistencyRule = "CONSISTENCY";

        public const string NotRunText = "NOT RUN";

        public const string FilePrefix = "QA-report-";

        public static readonly IReadOnlyList<string> Checks =
            new[] { ModelCheck, GeometryCheck, NumberingCheck };

        public static readonly IReadOnlyList<string> CsvColumns =
            new[] { "Check", "Rule", "Status", "ObjectId", "Name", "Profile", "Value", "Message" };

        private const string ObjectIdSeparator = ";";
        private const string LineBreak = "\r\n";

        /// <summary>
        /// Builds the report. modelName and timestamp are supplied by the
        /// caller and only appear in the summary.
        /// </summary>
        public static QaReport Build(
            ModelCheckResult? modelCheck,
            GeometryCheckResult? geometryCheck,
            NumberingReportResult? numberingReport,
            string? modelName,
            DateTime timestamp)
        {
            var rows = BuildRows(modelCheck, geometryCheck, numberingReport);

            var notRun = new List<string>();
            if (modelCheck == null)
                notRun.Add(ModelCheck);
            if (geometryCheck == null)
                notRun.Add(GeometryCheck);
            if (numberingReport == null)
                notRun.Add(NumberingCheck);

            return new QaReport(
                rows,
                notRun,
                ToCsv(rows),
                BuildSummary(rows, notRun, modelName, timestamp));
        }

        /// <summary>
        /// Returns the findings ordered by check (MODEL, GEOMETRY, NUMBERING),
        /// then lowest object id (rows without an object id last), then rule
        /// (ordinal), then source order.
        /// </summary>
        public static IReadOnlyList<QaReportRow> BuildRows(
            ModelCheckResult? modelCheck,
            GeometryCheckResult? geometryCheck,
            NumberingReportResult? numberingReport)
        {
            var rows = new List<QaReportRow>();

            if (modelCheck != null)
                AddModelRows(modelCheck, rows);
            if (geometryCheck != null)
                AddGeometryRows(geometryCheck, rows);
            if (numberingReport != null)
                AddNumberingRows(numberingReport, rows);

            // OrderBy is stable, so equal keys keep their source order.
            return rows
                .OrderBy(row => CheckOrder(row.Check))
                .ThenBy(row => row.ObjectIds.Count == 0 ? 1 : 0)
                .ThenBy(row => row.ObjectIds.Count == 0 ? 0 : row.ObjectIds[0])
                .ThenBy(row => row.Rule, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Writes a header and one record per row. Records end with CRLF;
        /// fields are quoted per RFC 4180 when needed.
        /// </summary>
        public static string ToCsv(IEnumerable<QaReportRow> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var csv = new StringBuilder();
            AppendRecord(csv, CsvColumns);

            foreach (var row in rows)
            {
                AppendRecord(csv, new[]
                {
                    row.Check,
                    row.Rule,
                    row.Status,
                    string.Join(
                        ObjectIdSeparator,
                        row.ObjectIds.Select(FormatInteger)),
                    row.Name,
                    row.Profile,
                    row.Value,
                    row.Message
                });
            }

            return csv.ToString();
        }

        /// <summary>
        /// Quotes a field when it contains a comma, a quote, CR or LF, and
        /// doubles embedded quotes. Null is written as an empty field.
        /// </summary>
        public static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            if (field!.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return field;

            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// "QA-report-yyyyMMdd-HHmmss" for the caller-supplied timestamp.
        /// </summary>
        public static string FileBaseName(DateTime timestamp)
        {
            return FilePrefix +
                timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        private static void AddModelRows(
            ModelCheckResult result,
            List<QaReportRow> rows)
        {
            foreach (var item in NotNull(result.NameEmptyItems, "NameEmptyItems"))
            {
                rows.Add(new QaReportRow(
                    ModelCheck,
                    NameEmptyRule,
                    Text(item.Status),
                    new[] { item.ObjectId },
                    Text(item.Name),
                    Text(item.Profile),
                    Text(item.Material),
                    "Steel part name is empty."));
            }

            foreach (var finding in NotNull(result.LengthFindings, "LengthFindings"))
            {
                rows.Add(new QaReportRow(
                    ModelCheck,
                    LengthRule,
                    Text(finding.Status),
                    new[] { finding.ObjectId },
                    Text(finding.Name),
                    Text(finding.Profile),
                    finding.LengthMm.HasValue
                        ? FormatNumber(finding.LengthMm.Value)
                        : string.Empty,
                    Text(finding.Message)));
            }

            foreach (var finding in NotNull(result.ConsistencyFindings, "ConsistencyFindings"))
            {
                rows.Add(new QaReportRow(
                    ModelCheck,
                    ConsistencyRule,
                    Text(finding.Status),
                    new[] { finding.ObjectId },
                    Text(finding.Name),
                    Text(finding.Profile),
                    Text(finding.Material),
                    Text(finding.Message)));
            }
        }

        private static void AddGeometryRows(
            GeometryCheckResult result,
            List<QaReportRow> rows)
        {
            foreach (var finding in NotNull(result.Findings, "Findings"))
            {
                rows.Add(new QaReportRow(
                    GeometryCheck,
                    Text(finding.Rule),
                    Text(finding.Status),
                    SortedIds(finding.ObjectIds),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Text(finding.Message)));
            }
        }

        private static void AddNumberingRows(
            NumberingReportResult result,
            List<QaReportRow> rows)
        {
            foreach (var report in new[] { result.PartPositions, result.AssemblyPositions })
            {
                if (report == null)
                    continue;

                foreach (var finding in NotNull(report.Findings, "Findings"))
                {
                    rows.Add(new QaReportRow(
                        NumberingCheck,
                        Text(finding.Rule),
                        Text(finding.Status),
                        SortedIds(finding.ObjectIds),
                        string.Empty,
                        string.Empty,
                        Text(finding.Position),
                        Text(finding.Message)));
                }

                foreach (var gap in NotNull(report.Gaps, "Gaps"))
                {
                    var listed = gap.MissingNumbers ?? new List<int>();
                    rows.Add(new QaReportRow(
                        NumberingCheck,
                        NumberingRules.NumberingGapRule,
                        NumberingRules.InfoStatus,
                        Array.Empty<int>(),
                        string.Empty,
                        string.Empty,
                        Text(gap.Prefix),
                        Text(gap.PositionKind) + " prefix '" + Text(gap.Prefix) + "': " +
                        FormatInteger(gap.MissingCount) + " missing between " +
                        FormatInteger(gap.StartNumber) + " and " +
                        FormatInteger(gap.HighestNumber) + ": " +
                        string.Join(", ", listed.Select(FormatInteger)) +
                        (gap.MissingCount > listed.Count
                            ? " (first " + FormatInteger(listed.Count) + " listed)"
                            : string.Empty)));
                }
            }

            foreach (var part in NotNull(result.Parts, "Parts"))
            {
                if (part.NumberingStatus == NumberingRules.UpToDateStatus)
                    continue;

                rows.Add(new QaReportRow(
                    NumberingCheck,
                    NumberingRules.NumberingStatusRule,
                    Text(part.NumberingStatus),
                    new[] { part.ObjectId },
                    string.Empty,
                    string.Empty,
                    Text(part.PartPosition),
                    part.IsNumberingUpToDate.HasValue
                        ? "Numbering is not up to date."
                        : "Tekla could not report whether numbering is up to date."));
            }
        }

        private static string BuildSummary(
            IReadOnlyList<QaReportRow> rows,
            IReadOnlyList<string> notRun,
            string? modelName,
            DateTime timestamp)
        {
            var summary = new StringBuilder();
            summary.Append("STEEL VALIDATION ENGINE - KALITE RAPORU").Append(LineBreak);
            summary.Append("Model: ")
                .Append(string.IsNullOrWhiteSpace(modelName) ? "(bilinmiyor)" : modelName)
                .Append(LineBreak);
            summary.Append("Tarih: ")
                .Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .Append(LineBreak);

            // Only real problems are listed; informational rows stay in the CSV.
            Func<QaReportRow, bool> isProblem = row => !row.Message.StartsWith("INFO", StringComparison.Ordinal);
            int totalProblems = rows.Count(isProblem);
            summary.Append(totalProblems == 0 ? "SONUC: Sorun bulunmadi." : "SONUC: " + FormatInteger(totalProblems) + " sorun bulundu.").Append(LineBreak);

            foreach (var check in Checks)
            {
                summary.Append(LineBreak);
                string label = check == ModelCheck ? "MODEL (isim / profil / uzunluk)" : check == GeometryCheck ? "GEOMETRI" : "NUMARALANDIRMA";

                if (notRun.Contains(check))
                {
                    summary.Append(label).Append(": CALISTIRILMADI").Append(LineBreak);
                    continue;
                }

                var checkRows = rows.Where(row => row.Check == check).ToList();
                var problems = checkRows.Where(isProblem).ToList();
                summary.Append(label).Append(": ")
                    .Append(problems.Count == 0 ? "sorun yok" : FormatInteger(problems.Count) + " sorun")
                    .Append(" (").Append(FormatInteger(checkRows.Count)).Append(" kayit incelendi)")
                    .Append(LineBreak);

                foreach (var r in problems.Take(10))
                {
                    string id = r.ObjectIds.Count > 0 ? "ID " + r.ObjectIds[0].ToString(CultureInfo.InvariantCulture) + " " : "";
                    summary.Append("  - ").Append(id).Append(r.Name).Append(": ").Append(r.Message).Append(LineBreak);
                }

                if (problems.Count > 10)
                    summary.Append("  ... ve ").Append(FormatInteger(problems.Count - 10)).Append(" sorun daha (CSV'de).").Append(LineBreak);
            }

            summary.Append(LineBreak).Append("Tum ayrintilar ayni isimli CSV dosyasinda.").Append(LineBreak);
            return summary.ToString();
        }

        private static void AppendRecord(StringBuilder csv, IEnumerable<string> fields)
        {
            csv.Append(string.Join(",", fields.Select(EscapeCsvField)));
            csv.Append(LineBreak);
        }

        private static int CheckOrder(string check)
        {
            switch (check)
            {
                case ModelCheck: return 0;
                case GeometryCheck: return 1;
                default: return 2;
            }
        }

        private static IEnumerable<T> NotNull<T>(IEnumerable<T>? items, string name)
            where T : class
        {
            if (items == null)
                yield break;

            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "The result list " + name + " cannot contain null entries.");
                }

                yield return item;
            }
        }

        private static IReadOnlyList<int> SortedIds(IEnumerable<int>? ids)
        {
            return ids == null
                ? Array.Empty<int>()
                : ids.OrderBy(id => id).ToArray();
        }

        private static string Text(string? value) => value ?? string.Empty;

        private static string FormatInteger(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string FormatNumber(double value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }
}
