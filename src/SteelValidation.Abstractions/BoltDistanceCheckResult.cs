using System;
using System.Collections.Generic;
using System.Linq;
namespace SteelValidation.Abstractions
{
    public sealed class BoltDistanceCheckFinding
    {
        public int BoltGroupId { get; set; }
        public int TargetPartId { get; set; }
        public int BoltCenterIndex { get; set; }
        public int? OtherBoltCenterIndex { get; set; }
        public string RuleKind { get; set; }
        public string Outcome { get; set; }
        public double? NominalDiameterMm { get; set; }
        public double? ActualMm { get; set; }
        public double? RequiredMm { get; set; }
        public double? ActualDiameterRatio { get; set; }
        public double RequiredDiameterRatio { get; set; }
        public string ReasonCode { get; set; }
        public string Detail { get; set; }
    }
    public sealed class BoltDistanceCheckResult
    {
        public IReadOnlyList<BoltDistanceCheckFinding> Items { get; set; } = new List<BoltDistanceCheckFinding>().AsReadOnly();
        public IReadOnlyList<string> CoverageNotes { get; set; } = new List<string>().AsReadOnly();
        public string ScopeDescription { get; set; }
        public string ModelPath { get; set; }
        public DateTime CapturedUtc { get; set; }
        public bool ReadComplete { get; set; }
        public int BoltGroupCount { get; set; }
        public int SkippedHoleOnlyGroupCount { get; set; }
        public int CountsPass => Items.Count(i => i.Outcome == "Pass");
        public int CountsFail => Items.Count(i => i.Outcome == "Fail");
        public int CountsUnknown => Items.Count(i => i.Outcome == "Unknown");
        public int CountsNotApplicable => Items.Count(i => i.Outcome == "NotApplicable");
    }
}
