namespace SteelValidation.Abstractions
{
    public interface IModelAdapter
    {
        string SourceVersion { get; }

        bool IsConnected();

        BoltDistanceCheckResult RunBoltDistanceCheck(bool selectedOnly = false);

        ModelCheckResult RunModelCheck();

        GeometryCheckResult RunGeometryCheck();

        GeometryCheckResult RunGeometryCheckSelected();

        NumberingReportResult RunNumberingReport();

        NumberingReportResult RunNumberingReportSelected();
    }
}
