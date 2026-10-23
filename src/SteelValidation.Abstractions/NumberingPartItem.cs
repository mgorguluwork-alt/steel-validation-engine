namespace SteelValidation.Abstractions
{
    /// <summary>
    /// Numbering data read for one steel part. Positions that could not be
    /// read are empty. IsNumberingUpToDate is null when Tekla could not
    /// report it.
    /// </summary>
    public class NumberingPartItem
    {
        public int ObjectId { get; set; }

        public string PartPosition { get; set; } = string.Empty;

        public string AssemblyPosition { get; set; } = string.Empty;

        public bool? IsNumberingUpToDate { get; set; }

        public string NumberingStatus { get; set; } = string.Empty;
    }
}
