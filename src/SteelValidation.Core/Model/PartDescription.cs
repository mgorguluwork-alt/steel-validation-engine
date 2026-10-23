namespace SteelValidation.Core.Model
{
    public static class PartDescription
    {
        /// <summary>
        /// "NAME PROFILE" when both are present, otherwise whichever is
        /// present, otherwise the fallback (the part's type name).
        /// </summary>
        public static string Describe(string? name, string? profile, string fallback)
        {
            profile = profile ?? "";
            name = name ?? "";

            if (!string.IsNullOrWhiteSpace(name) &&
                !string.IsNullOrWhiteSpace(profile))
            {
                return name.Trim() + " " + profile.Trim();
            }

            if (!string.IsNullOrWhiteSpace(profile))
                return profile.Trim();

            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();

            return fallback;
        }
    }
}
