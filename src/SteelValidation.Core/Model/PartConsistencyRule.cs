using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace SteelValidation.Core.Model
{
    /// <summary>
    /// The outcome of checking a part name's configured profile and material
    /// patterns. Unknown data is deliberately not treated as a mismatch.
    /// </summary>
    public enum PartConsistencyStatus
    {
        NameEmpty,
        NotChecked,
        ProfileUnknown,
        ProfileMismatch,
        MaterialUnknown,
        MaterialMismatch,
        Ok
    }

    /// <summary>
    /// The profile pattern and optional material pattern configured for a
    /// part name. Patterns use .NET regular-expression syntax.
    /// </summary>
    public sealed class PartConsistencySpec
    {
        public PartConsistencySpec(string profilePattern, string? materialPattern = null)
        {
            ProfilePattern = profilePattern;
            MaterialPattern = materialPattern;
        }

        public string ProfilePattern { get; }

        public string? MaterialPattern { get; }
    }

    /// <summary>
    /// A deterministic result from <see cref="PartConsistencyRule" />.
    /// </summary>
    public sealed class PartConsistencyResult
    {
        internal PartConsistencyResult(
            PartConsistencyStatus status,
            string partName,
            string profile,
            string material,
            string? expectedProfilePattern,
            string? expectedMaterialPattern,
            string message)
        {
            Status = status;
            PartName = partName;
            Profile = profile;
            Material = material;
            ExpectedProfilePattern = expectedProfilePattern;
            ExpectedMaterialPattern = expectedMaterialPattern;
            Message = message;
        }

        public PartConsistencyStatus Status { get; }

        public string PartName { get; }

        public string Profile { get; }

        public string Material { get; }

        public string? ExpectedProfilePattern { get; }

        public string? ExpectedMaterialPattern { get; }

        /// <summary>
        /// True only when supplied known data fails a configured pattern.
        /// </summary>
        public bool IsMismatch =>
            Status == PartConsistencyStatus.ProfileMismatch ||
            Status == PartConsistencyStatus.MaterialMismatch;

        /// <summary>
        /// A stable, human-readable description of the result.
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// Checks supplied part name, profile and material values against
    /// configured patterns without deriving or substituting any model fact.
    /// </summary>
    public sealed class PartConsistencyRule
    {
        private static readonly TimeSpan RegexMatchTimeout =
            TimeSpan.FromSeconds(1);

        private static readonly IReadOnlyDictionary<string, PartConsistencySpec>
            DefaultSpecs =
                new ReadOnlyDictionary<string, PartConsistencySpec>(
                    new Dictionary<string, PartConsistencySpec>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        ["ANGLE"] = new PartConsistencySpec(
                            @"^L\d+(\.\d+)?([*X]\d+(\.\d+)?){1,2}$"),
                        ["BASE PLATE"] = new PartConsistencySpec(@"^PL.*$"),
                        ["GUSSET PLATE"] = new PartConsistencySpec(@"^PL.*$"),
                        ["TIE ROD"] = new PartConsistencySpec(
                            @"^(D|RD|ROD)\d+(\.\d+)?$"),
                        ["SAG ROD"] = new PartConsistencySpec(
                            @"^(D|RD|ROD)\d+(\.\d+)?$"),
                        ["ANCHOR BOLT"] = new PartConsistencySpec(
                            @"^(D|RD|ROD)\d+(\.\d+)?$")
                    });

        private readonly Dictionary<string, CompiledSpec> _specifications;

        /// <summary>
        /// The default rules used when no configuration is supplied.
        /// </summary>
        public static IReadOnlyDictionary<string, PartConsistencySpec>
            DefaultSpecifications => DefaultSpecs;

        /// <summary>
        /// Creates a consistency rule. A supplied configuration replaces the
        /// defaults; pass null to use the default profile rules.
        /// </summary>
        public PartConsistencyRule(
            IDictionary<string, PartConsistencySpec>? specifications = null)
        {
            _specifications =
                new Dictionary<string, CompiledSpec>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<KeyValuePair<string, PartConsistencySpec>> source;
            if (specifications == null)
                source = DefaultSpecs;
            else
                source = specifications;

            foreach (var configuredSpec in source)
            {
                var name = Normalize(configuredSpec.Key);
                if (name.Length == 0)
                {
                    throw new ArgumentException(
                        "A consistency-rule name cannot be empty.",
                        nameof(specifications));
                }

                if (_specifications.ContainsKey(name))
                {
                    throw new ArgumentException(
                        "Consistency-rule names must be unique after trimming.",
                        nameof(specifications));
                }

                if (configuredSpec.Value == null)
                {
                    throw new ArgumentException(
                        "A consistency-rule specification cannot be null.",
                        nameof(specifications));
                }

                var profilePattern = configuredSpec.Value.ProfilePattern;
                if (string.IsNullOrWhiteSpace(profilePattern))
                {
                    throw new ArgumentException(
                        "A consistency-rule profile pattern cannot be empty.",
                        nameof(specifications));
                }

                var materialPattern = configuredSpec.Value.MaterialPattern;
                if (materialPattern != null && string.IsNullOrWhiteSpace(materialPattern))
                {
                    throw new ArgumentException(
                        "A consistency-rule material pattern cannot be empty.",
                        nameof(specifications));
                }

                _specifications.Add(
                    name,
                    new CompiledSpec(
                        profilePattern,
                        CompilePattern(profilePattern, nameof(specifications)),
                        materialPattern,
                        materialPattern == null
                            ? null
                            : CompilePattern(materialPattern, nameof(specifications))));
            }
        }

        /// <summary>
        /// Checks only the supplied values. It never infers a profile or
        /// material from a name, and it leaves names with no rule unchecked.
        /// </summary>
        public PartConsistencyResult Check(
            string? partName,
            string? profile,
            string? material)
        {
            var normalizedName = Normalize(partName);
            var normalizedProfile = Normalize(profile);
            var normalizedMaterial = Normalize(material);

            if (normalizedName.Length == 0)
            {
                return CreateResult(
                    PartConsistencyStatus.NameEmpty,
                    normalizedName,
                    normalizedProfile,
                    normalizedMaterial,
                    null,
                    null,
                    "INFO - " + DisplayName(normalizedName) + " profile " +
                    DisplayValue(normalizedProfile) +
                    " was not checked; expected profile pattern " +
                    NoConfiguredPattern);
            }

            if (!_specifications.TryGetValue(normalizedName, out var spec))
            {
                return CreateResult(
                    PartConsistencyStatus.NotChecked,
                    normalizedName,
                    normalizedProfile,
                    normalizedMaterial,
                    null,
                    null,
                    "INFO - " + DisplayName(normalizedName) + " profile " +
                    DisplayValue(normalizedProfile) +
                    " was not checked; expected profile pattern " +
                    NoConfiguredPattern);
            }

            if (normalizedProfile.Length == 0)
            {
                return CreateResult(
                    PartConsistencyStatus.ProfileUnknown,
                    normalizedName,
                    normalizedProfile,
                    normalizedMaterial,
                    spec.ProfilePattern,
                    spec.MaterialPattern,
                    "INFO - " + DisplayName(normalizedName) + " profile " +
                    DisplayValue(normalizedProfile) +
                    " is unknown; expected profile pattern " +
                    spec.ProfilePattern);
            }

            if (!spec.ProfileRegex.IsMatch(normalizedProfile))
            {
                return CreateResult(
                    PartConsistencyStatus.ProfileMismatch,
                    normalizedName,
                    normalizedProfile,
                    normalizedMaterial,
                    spec.ProfilePattern,
                    spec.MaterialPattern,
                    "WARNING - " + DisplayName(normalizedName) + " profile " +
                    DisplayValue(normalizedProfile) +
                    " does not match expected profile pattern " +
                    spec.ProfilePattern);
            }

            if (spec.MaterialRegex != null && normalizedMaterial.Length == 0)
            {
                return CreateResult(
                    PartConsistencyStatus.MaterialUnknown,
                    normalizedName,
                    normalizedProfile,
                    normalizedMaterial,
                    spec.ProfilePattern,
                    spec.MaterialPattern,
                    "INFO - " + DisplayName(normalizedName) + " profile " +
                    DisplayValue(normalizedProfile) +
                    " matches expected profile pattern " + spec.ProfilePattern +
                    "; material is unknown; expected material pattern " +
                    spec.MaterialPattern);
            }

            if (spec.MaterialRegex != null &&
                !spec.MaterialRegex.IsMatch(normalizedMaterial))
            {
                return CreateResult(
                    PartConsistencyStatus.MaterialMismatch,
                    normalizedName,
                    normalizedProfile,
                    normalizedMaterial,
                    spec.ProfilePattern,
                    spec.MaterialPattern,
                    "WARNING - " + DisplayName(normalizedName) + " profile " +
                    DisplayValue(normalizedProfile) +
                    " matches expected profile pattern " + spec.ProfilePattern +
                    "; material " + DisplayValue(normalizedMaterial) +
                    " does not match expected material pattern " +
                    spec.MaterialPattern);
            }

            var materialMessage = spec.MaterialPattern == null
                ? string.Empty
                : "; material " + DisplayValue(normalizedMaterial) +
                  " matches expected material pattern " + spec.MaterialPattern;

            return CreateResult(
                PartConsistencyStatus.Ok,
                normalizedName,
                normalizedProfile,
                normalizedMaterial,
                spec.ProfilePattern,
                spec.MaterialPattern,
                "INFO - " + DisplayName(normalizedName) + " profile " +
                DisplayValue(normalizedProfile) +
                " matches expected profile pattern " + spec.ProfilePattern +
                materialMessage);
        }

        private static PartConsistencyResult CreateResult(
            PartConsistencyStatus status,
            string partName,
            string profile,
            string material,
            string? expectedProfilePattern,
            string? expectedMaterialPattern,
            string message) =>
            new PartConsistencyResult(
                status,
                partName,
                profile,
                material,
                expectedProfilePattern,
                expectedMaterialPattern,
                message);

        private static Regex CompilePattern(string pattern, string parameterName)
        {
            try
            {
                return new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    RegexMatchTimeout);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    "A consistency-rule pattern must be a valid regular expression.",
                    parameterName,
                    exception);
            }
        }

        private static string Normalize(string? value) =>
            value == null ? string.Empty : value.Trim();

        private static string DisplayName(string normalizedName) =>
            normalizedName.Length == 0 ? "(unnamed part)" : normalizedName;

        private static string DisplayValue(string normalizedValue) =>
            normalizedValue.Length == 0 ? "(empty)" : normalizedValue;

        private const string NoConfiguredPattern = "(no configured profile pattern)";

        private sealed class CompiledSpec
        {
            public CompiledSpec(
                string profilePattern,
                Regex profileRegex,
                string? materialPattern,
                Regex? materialRegex)
            {
                ProfilePattern = profilePattern;
                ProfileRegex = profileRegex;
                MaterialPattern = materialPattern;
                MaterialRegex = materialRegex;
            }

            public string ProfilePattern { get; }

            public Regex ProfileRegex { get; }

            public string? MaterialPattern { get; }

            public Regex? MaterialRegex { get; }
        }
    }
}
