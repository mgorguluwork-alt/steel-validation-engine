namespace SteelValidation.Core.Numbering
{
    /// <summary>
    /// Tekla-independent input to the numbering rules for one steel part.
    /// Null or empty positions are missing; a null up-to-date flag is unknown.
    /// A null start number, or one below 1, means the numbering series start
    /// was not read and the series is taken to start at 1.
    /// </summary>
    public sealed class NumberingReportPart
    {
        public NumberingReportPart(
            int objectId,
            string? partPosition,
            string? assemblyPosition,
            bool? isNumberingUpToDate,
            int? partStartNumber = null,
            int? assemblyStartNumber = null,
            int? assemblyObjectId = null)
        {
            ObjectId = objectId;
            PartPosition = partPosition;
            AssemblyPosition = assemblyPosition;
            IsNumberingUpToDate = isNumberingUpToDate;
            PartStartNumber = partStartNumber;
            AssemblyStartNumber = assemblyStartNumber;
            AssemblyObjectId = assemblyObjectId;
        }

        public int ObjectId { get; }

        public string? PartPosition { get; }

        public string? AssemblyPosition { get; }

        public bool? IsNumberingUpToDate { get; }

        /// <summary>Tekla Part.PartNumber.StartNumber.</summary>
        public int? PartStartNumber { get; }

        /// <summary>
        /// Tekla Part.AssemblyNumber.StartNumber of the assembly's main part;
        /// null for secondary parts, whose value is not the assembly series.
        /// </summary>
        public int? AssemblyStartNumber { get; }

        /// <summary>Tekla id of the part's assembly; null when it was not read.</summary>
        public int? AssemblyObjectId { get; }
    }
}
