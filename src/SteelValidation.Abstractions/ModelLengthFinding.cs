namespace SteelValidation.Abstractions
{
    /// <summary>
    /// A read-only length-rule result for one steel part in a model check.
    /// </summary>
    public class ModelLengthFinding
    {
        public int ObjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public double? LengthMm { get; set; }
        public double? LimitMm { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
