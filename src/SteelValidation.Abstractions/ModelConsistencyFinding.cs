namespace SteelValidation.Abstractions
{
    /// <summary>
    /// A read-only name/profile/material consistency-rule result for one
    /// steel part in a model check.
    /// </summary>
    public class ModelConsistencyFinding
    {
        public int ObjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string ExpectedProfilePattern { get; set; } = string.Empty;
        public string ExpectedMaterialPattern { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
