namespace SteelValidation.Abstractions
{
    /// <summary>
    /// The read-only inputs of a V9 QA report: the full-model results of the
    /// model check, geometry check and numbering report, plus the model name
    /// and folder read from the Tekla model info. A result that was not run
    /// is null; ModelName and ModelPath are empty when Tekla did not report
    /// them.
    /// </summary>
    public class QaReportData
    {
        public string ModelName { get; set; } = string.Empty;

        public string ModelPath { get; set; } = string.Empty;

        public ModelCheckResult? ModelCheck { get; set; }

        public GeometryCheckResult? GeometryCheck { get; set; }

        public NumberingReportResult? NumberingReport { get; set; }
    }
}
