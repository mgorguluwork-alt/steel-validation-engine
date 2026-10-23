namespace SteelValidation.Core.Model
{
    public static class ModelCheckRules
    {
        public const string NameEmptyStatus = "ERROR - NAME EMPTY";

        /// <summary>
        /// Concrete parts are skipped by the model check. Uses the same
        /// culture-sensitive ToUpper the adapter used; a missing material is
        /// not excluded.
        /// </summary>
        public static bool IsExcludedMaterial(string? material) =>
            material != null &&
            material.ToUpper().Contains("CONCRETE");

        public static bool IsNameMissing(string? name) =>
            string.IsNullOrWhiteSpace(name);
    }
}
