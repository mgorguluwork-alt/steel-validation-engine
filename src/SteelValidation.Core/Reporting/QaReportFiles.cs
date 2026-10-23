using System;
using System.IO;
using System.Text;

namespace SteelValidation.Core.Reporting
{
    /// <summary>
    /// Writes a built QA report to disk. Kept apart from
    /// <see cref="QaReportBuilder"/>, which stays pure. Existing files are
    /// never overwritten.
    /// </summary>
    public static class QaReportFiles
    {
        /// <summary>
        /// True when path is folder itself or lies below it. Both paths are
        /// normalised with Path.GetFullPath and compared case-insensitively,
        /// as on Windows. An empty folder contains nothing.
        /// </summary>
        public static bool IsInsideFolder(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
                return false;

            var fullPath = Normalize(path);
            var fullFolder = Normalize(folder);

            if (string.Equals(fullPath, fullFolder, StringComparison.OrdinalIgnoreCase))
                return true;

            return fullPath.StartsWith(
                fullFolder + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes QA-report-&lt;yyyyMMdd-HHmmss&gt;.csv and .txt into folder
        /// (created if missing). Throws IOException, without writing
        /// anything, when either file already exists.
        /// </summary>
        public static QaReportFileSet Write(
            string folder,
            QaReport report,
            DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("An output folder is required.", nameof(folder));
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            Directory.CreateDirectory(folder);

            var baseName = QaReportBuilder.FileBaseName(timestamp);
            var files = new QaReportFileSet(
                Path.Combine(folder, baseName + ".csv"),
                Path.Combine(folder, baseName + ".txt"));

            foreach (var existing in new[] { files.CsvPath, files.SummaryPath })
            {
                if (File.Exists(existing))
                    throw new IOException("QA report file already exists: " + existing);
            }

            WriteNew(files.CsvPath, report.Csv);
            try
            {
                WriteNew(files.SummaryPath, report.Summary);
            }
            catch
            {
                // Only remove the CSV this call created a moment ago.
                File.Delete(files.CsvPath);
                throw;
            }

            return files;
        }

        private static void WriteNew(string path, string content)
        {
            // CreateNew fails instead of overwriting a file created meanwhile.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                writer.Write(content);
            }
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>The two files written for one QA report.</summary>
    public sealed class QaReportFileSet
    {
        public QaReportFileSet(string csvPath, string summaryPath)
        {
            CsvPath = csvPath;
            SummaryPath = summaryPath;
        }

        public string CsvPath { get; }

        public string SummaryPath { get; }
    }
}
