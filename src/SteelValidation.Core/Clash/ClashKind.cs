namespace SteelValidation.Core.Clash
{
    /// <summary>
    /// Tekla-independent mirror of ClashCheckData.ClashTypeEnum. The Tekla
    /// adapter maps the native value to this one before applying the rules.
    /// </summary>
    public enum ClashKind
    {
        Invalid,
        IsInside,
        ExactMatch,
        Clash,
        MinDistance,
        FailedSolid,
        CutThrough,
        ComplexClash,
        FailedTest
    }
}
