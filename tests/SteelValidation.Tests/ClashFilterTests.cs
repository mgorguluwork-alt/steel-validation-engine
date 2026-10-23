using SteelValidation.Abstractions;
using SteelValidation.Core.Clash;

namespace SteelValidation.Tests;

public sealed class ClashFilterTests
{
    private static List<ClashCheckItem> Items()
    {
        return new List<ClashCheckItem>
        {
            new()
            {
                ClashType = ClashRules.GetTypeText(ClashKind.Clash),
                Object1 = "Primary Beam A",
                Object2 = "Column 1",
                Overlap = 12.5,
                Severity = ClashRules.High
            },
            new()
            {
                ClashType = ClashRules.GetTypeText(ClashKind.MinDistance),
                Object1 = "Brace B",
                Object2 = "Secondary Beam B",
                Overlap = 2.5,
                Severity = ClashRules.Medium
            },
            new()
            {
                ClashType = ClashRules.GetTypeText(ClashKind.Invalid),
                Object1 = "Plate C",
                Object2 = "Base plate",
                Overlap = 0,
                Severity = ClashRules.Low
            }
        };
    }

    [Fact]
    public void No_Criteria_Keeps_All_Items()
    {
        var items = Items();

        IList<ClashCheckItem> nullCriteria = ClashFilter.Apply(items, null);
        IList<ClashCheckItem> emptyCriteria = ClashFilter.Apply(
            items,
            new ClashFilterCriteria
            {
                Severities = new List<string>(),
                ClashTypes = new List<string>(),
                ObjectText = "  "
            });

        Assert.Equal(items, nullCriteria);
        Assert.Equal(items, emptyCriteria);
        Assert.NotSame(items, nullCriteria);
    }

    [Fact]
    public void Severity_Criterion_Keeps_Only_Exact_Severity()
    {
        var items = Items();

        IList<ClashCheckItem> filtered = ClashFilter.Apply(
            items,
            new ClashFilterCriteria
            {
                Severities = new[] { ClashRules.Medium }
            });

        Assert.Equal(new[] { items[1] }, filtered);
    }

    [Fact]
    public void Clash_Type_Criterion_Keeps_Only_Exact_Type()
    {
        var items = Items();

        IList<ClashCheckItem> filtered = ClashFilter.Apply(
            items,
            new ClashFilterCriteria
            {
                ClashTypes = new[]
                {
                    ClashRules.GetTypeText(ClashKind.Invalid)
                }
            });

        Assert.Equal(new[] { items[2] }, filtered);
    }

    [Fact]
    public void Object_Text_Matches_Either_Object_Without_Case_Sensitivity()
    {
        var items = Items();

        IList<ClashCheckItem> object1Match = ClashFilter.Apply(
            items,
            new ClashFilterCriteria { ObjectText = "pRiMaRy bEaM" });
        IList<ClashCheckItem> object2Match = ClashFilter.Apply(
            items,
            new ClashFilterCriteria { ObjectText = "sEcOnDaRy" });

        Assert.Equal(new[] { items[0] }, object1Match);
        Assert.Equal(new[] { items[1] }, object2Match);
    }

    [Fact]
    public void Minimum_Overlap_Criterion_Keeps_Only_Positive_Values_At_Least_Minimum()
    {
        var items = Items();

        IList<ClashCheckItem> filtered = ClashFilter.Apply(
            items,
            new ClashFilterCriteria { MinimumOverlap = 3.0 });

        Assert.Equal(new[] { items[0] }, filtered);
    }

    [Fact]
    public void Zero_Overlap_Is_Kept_Without_Minimum_And_Dropped_With_Any_Minimum()
    {
        var items = Items();

        IList<ClashCheckItem> withoutMinimum = ClashFilter.Apply(
            items,
            new ClashFilterCriteria());
        IList<ClashCheckItem> zeroMinimum = ClashFilter.Apply(
            items,
            new ClashFilterCriteria { MinimumOverlap = 0 });

        Assert.Contains(items[2], withoutMinimum);
        Assert.DoesNotContain(items[2], zeroMinimum);
        Assert.Equal(new[] { items[0], items[1] }, zeroMinimum);
    }

    [Fact]
    public void Combined_Criteria_Use_And_Logic()
    {
        var items = Items();

        IList<ClashCheckItem> filtered = ClashFilter.Apply(
            items,
            new ClashFilterCriteria
            {
                Severities = new[] { ClashRules.High },
                ClashTypes = new[]
                {
                    ClashRules.GetTypeText(ClashKind.Clash)
                },
                ObjectText = "column",
                MinimumOverlap = 10
            });

        Assert.Equal(new[] { items[0] }, filtered);
    }

    [Fact]
    public void Filter_Preserves_Order_And_Does_Not_Modify_Input()
    {
        var items = Items();
        ClashCheckItem[] originalItems = items.ToArray();
        int originalCount = items.Count;

        IList<ClashCheckItem> filtered = ClashFilter.Apply(
            items,
            new ClashFilterCriteria
            {
                Severities = new[] { ClashRules.Low, ClashRules.High }
            });

        Assert.Equal(new[] { items[0], items[2] }, filtered);
        Assert.Equal(originalCount, items.Count);
        Assert.Equal(originalItems, items);
    }

    [Fact]
    public void Filtered_Summary_Is_Separate_From_Original_Result()
    {
        var source = new ClashCheckResult
        {
            ScannedParts = 20,
            NativeClashCount = 7,
            Items = Items()
        };
        ClashResultSummary.Summarize(source);
        ClashCheckItem[] originalItems = source.Items.ToArray();
        int originalTotal = source.TotalClashes;
        int originalHigh = source.HighCount;
        int originalMedium = source.MediumCount;
        int originalLow = source.LowCount;

        IList<ClashCheckItem> filteredItems = ClashFilter.Apply(
            source.Items,
            new ClashFilterCriteria
            {
                Severities = new[] { ClashRules.High, ClashRules.Low }
            });
        ClashCheckResult filtered = ClashResultSummary.SummarizeFiltered(
            source,
            filteredItems);

        Assert.Equal(2, filtered.TotalClashes);
        Assert.Equal(1, filtered.HighCount);
        Assert.Equal(0, filtered.MediumCount);
        Assert.Equal(1, filtered.LowCount);
        Assert.Equal(20, filtered.ScannedParts);
        Assert.Equal(7, filtered.NativeClashCount);
        Assert.Equal(originalItems, source.Items);
        Assert.Equal(originalTotal, source.TotalClashes);
        Assert.Equal(originalHigh, source.HighCount);
        Assert.Equal(originalMedium, source.MediumCount);
        Assert.Equal(originalLow, source.LowCount);
        Assert.NotSame(source.Items, filtered.Items);
    }
}
