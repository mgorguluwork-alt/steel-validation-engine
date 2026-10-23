using SteelValidation.Core.Numbering;
using SteelValidation.Core.Revision;

namespace SteelValidation.Tests;

public sealed class RevisionTests
{
    private static readonly DateTime T0 = new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    private static ObjectState State(
        string guid,
        int id,
        string? position = "C1",
        string? profile = "HEA200",
        double? length = 6000,
        FabricationReleaseState release = FabricationReleaseState.NotReleased,
        string? assembly = "A1",
        string? material = "S355",
        string? name = "COLUMN") =>
        new ObjectState(guid, id, position, assembly, profile, material, name, "2", length, release);

    private static ModelSnapshot Snapshot(params ObjectState[] objects) =>
        new ModelSnapshot("Test model", T0, "Claude", "test", false, objects);

    [Fact]
    public void Unchanged_Objects_Produce_No_Changes()
    {
        var diff = SnapshotComparer.Compare(Snapshot(State("g1", 1)), Snapshot(State("g1", 1)));

        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void Added_Removed_And_Modified_Objects_Are_Found_By_Guid_In_Guid_Order()
    {
        var before = Snapshot(State("b", 2), State("a", 1, profile: "HEA200"));
        var after = Snapshot(State("c", 3), State("a", 7, profile: "HEA220"));

        var diff = SnapshotComparer.Compare(before, after);

        Assert.Collection(
            diff.Changes,
            change =>
            {
                Assert.Equal("a", change.Guid);
                Assert.Equal(ObjectChangeKind.Modified, change.Kind);
                var field = Assert.Single(change.Fields);
                Assert.Equal(SnapshotComparer.ProfileField, field.Field);
                Assert.Equal("HEA200", field.Before);
                Assert.Equal("HEA220", field.After);
            },
            change => Assert.Equal((("b"), ObjectChangeKind.Removed), (change.Guid, change.Kind)),
            change => Assert.Equal((("c"), ObjectChangeKind.Added), (change.Guid, change.Kind)));
    }

    [Fact]
    public void A_New_Object_Id_Alone_Is_Not_A_Change_Because_The_Guid_Is_The_Identity()
    {
        var diff = SnapshotComparer.Compare(Snapshot(State("g1", 1)), Snapshot(State("g1", 99)));

        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void Every_Changed_Field_Is_Listed_In_A_Fixed_Order()
    {
        var before = State("g", 1, position: "C1", profile: "HEA200", length: 6000,
            release: FabricationReleaseState.NotReleased, assembly: "A1", material: "S235", name: "BEAM");
        var after = State("g", 1, position: "C2", profile: "HEA220", length: 6100,
            release: FabricationReleaseState.Released, assembly: "A2", material: "S355", name: "COLUMN");

        var change = Assert.Single(SnapshotComparer.Compare(Snapshot(before), Snapshot(after)).Changes);

        Assert.Equal(
            new[]
            {
                SnapshotComparer.PartPositionField, SnapshotComparer.AssemblyPositionField,
                SnapshotComparer.ProfileField, SnapshotComparer.MaterialField, SnapshotComparer.NameField,
                SnapshotComparer.LengthField, SnapshotComparer.ReleaseStateField
            },
            change.Fields.Select(field => field.Field));
        Assert.Equal("6000", change.Fields.Single(f => f.Field == SnapshotComparer.LengthField).Before);
        Assert.Equal("Released", change.Fields.Single(f => f.Field == SnapshotComparer.ReleaseStateField).After);
    }

    [Fact]
    public void Floating_Point_Noise_In_Length_Is_Not_A_Change_But_A_Real_Change_Is()
    {
        var noise = SnapshotComparer.Compare(Snapshot(State("g", 1, length: 6000)), Snapshot(State("g", 1, length: 6000.004)));
        var real = SnapshotComparer.Compare(Snapshot(State("g", 1, length: 6000)), Snapshot(State("g", 1, length: 6000.5)));

        Assert.False(noise.HasChanges);
        Assert.Equal("6000.5", Assert.Single(Assert.Single(real.Changes).Fields).After);
    }

    [Fact]
    public void An_Unreported_Length_Differs_From_A_Reported_One()
    {
        var diff = SnapshotComparer.Compare(Snapshot(State("g", 1, length: null)), Snapshot(State("g", 1, length: 6000)));

        var field = Assert.Single(Assert.Single(diff.Changes).Fields);
        Assert.Equal("", field.Before);
        Assert.Equal("6000", field.After);
    }

    [Fact]
    public void Cyrillic_And_Latin_Prefixes_Are_Different_Values()
    {
        var diff = SnapshotComparer.Compare(Snapshot(State("g", 1, position: "K1")), Snapshot(State("g", 1, position: "К1")));

        Assert.Equal(SnapshotComparer.PartPositionField, Assert.Single(Assert.Single(diff.Changes).Fields).Field);
    }

    [Theory]
    [InlineData(FabricationReleaseState.Released, FabricationReleaseState.Released, true)]
    [InlineData(FabricationReleaseState.NotReleased, FabricationReleaseState.Released, true)]
    [InlineData(FabricationReleaseState.NotReleased, FabricationReleaseState.NotReleased, false)]
    [InlineData(FabricationReleaseState.Unknown, FabricationReleaseState.Unknown, false)]
    public void A_Change_To_A_Released_Object_Is_Flagged_For_Change_Control(
        FabricationReleaseState before, FabricationReleaseState after, bool flagged)
    {
        var diff = SnapshotComparer.Compare(
            Snapshot(State("g", 1, profile: "HEA200", release: before)),
            Snapshot(State("g", 1, profile: "HEA220", release: after)));

        Assert.Equal(flagged, Assert.Single(diff.Changes).AffectsReleasedObject);
    }

    [Fact]
    public void A_Removed_Released_Object_Is_Flagged()
    {
        var diff = SnapshotComparer.Compare(
            Snapshot(State("g", 1, release: FabricationReleaseState.Released)), Snapshot());

        var change = Assert.Single(diff.Changes);
        Assert.Equal(ObjectChangeKind.Removed, change.Kind);
        Assert.True(change.AffectsReleasedObject);
    }

    [Fact]
    public void A_Snapshot_Rejects_Duplicate_Guids_Local_Times_And_A_Missing_Author()
    {
        Assert.Throws<ArgumentException>(() => Snapshot(State("g", 1), State("g", 2)));
        Assert.Throws<ArgumentException>(() =>
            new ModelSnapshot("m", new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Local), "Claude", "r", false, Array.Empty<ObjectState>()));
        Assert.Throws<ArgumentException>(() => new ModelSnapshot("m", T0, " ", "r", false, Array.Empty<ObjectState>()));
        Assert.Throws<ArgumentException>(() => State(" ", 1));
    }

    [Fact]
    public void A_Snapshot_Lists_Objects_By_Guid_And_Finds_Them()
    {
        var snapshot = Snapshot(State("b", 2), State("a", 1));

        Assert.Equal(new[] { "a", "b" }, snapshot.Objects.Select(o => o.Guid));
        Assert.Equal(2, snapshot.Find("b")!.ObjectId);
        Assert.Null(snapshot.Find("x"));
    }

    [Fact]
    public void A_Proposed_Gap_Repair_Becomes_One_Traceable_Record_Per_Object()
    {
        var snapshot = Snapshot(
            State("g1", 1, position: "C1", assembly: "A1"),
            State("g2", 2, position: "C3", assembly: "A7"),
            State("g3", 3, position: "C3", assembly: "A8"));
        var plan = GapRepairPlanner.Plan(
            new[]
            {
                new NumberingReportPart(1, "C1", "A1", true),
                new NumberingReportPart(2, "C3", "A7", true),
                new NumberingReportPart(3, "C3", "A8", true)
            },
            new MapReleaseStateSource(new[] { 1, 2, 3 }.Select(id =>
                new KeyValuePair<int, FabricationReleaseState>(id, FabricationReleaseState.NotReleased))),
            isWholeModel: true);

        var records = PositionChangeRecords.FromPlan(plan, snapshot, T0, "Claude");

        Assert.Collection(
            records.Where(r => r.PositionKind == NumberingRules.PartPositionKind),
            record =>
            {
                Assert.Equal("g2", record.Guid);
                Assert.Equal("A7", record.AssemblyPosition);
                Assert.Equal("C3", record.OldPosition);
                Assert.Equal("C2", record.NewPosition);
                Assert.Equal(T0, record.TimestampUtc);
                Assert.Equal("Claude", record.By);
                Assert.Equal(GapRepairPlanner.Reason, record.Reason);
                Assert.Equal(FabricationReleaseState.NotReleased, record.ReleaseState);
            },
            record => Assert.Equal(("g3", "C2"), (record.Guid, record.NewPosition)));
    }

    [Fact]
    public void Blocked_Series_Produce_No_Records()
    {
        var plan = GapRepairPlanner.Plan(
            new[] { new NumberingReportPart(1, "C1", null, true), new NumberingReportPart(2, "C3", null, true) },
            UnknownReleaseStateSource.Instance,
            isWholeModel: true);

        Assert.Empty(PositionChangeRecords.FromPlan(plan, Snapshot(State("g1", 1), State("g2", 2)), T0, "Claude"));
    }

    [Fact]
    public void A_Change_Without_A_Guid_In_The_Snapshot_Is_Refused()
    {
        var plan = UnreleasedPlan();

        Assert.Throws<InvalidOperationException>(() =>
            PositionChangeRecords.FromPlan(plan, Snapshot(State("g1", 1)), T0, "Claude"));
    }

    [Fact]
    public void A_Snapshot_That_Disagrees_About_Release_State_Is_Refused()
    {
        var plan = UnreleasedPlan();
        var snapshot = Snapshot(State("g1", 1), State("g2", 2, release: FabricationReleaseState.Released));

        Assert.Throws<InvalidOperationException>(() => PositionChangeRecords.FromPlan(plan, snapshot, T0, "Claude"));
    }

    [Fact]
    public void Records_Need_Utc_Time_And_An_Author()
    {
        var plan = UnreleasedPlan();
        var snapshot = Snapshot(State("g1", 1), State("g2", 2));

        Assert.Throws<ArgumentException>(() =>
            PositionChangeRecords.FromPlan(plan, snapshot, new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Unspecified), "Claude"));
        Assert.Throws<ArgumentException>(() => PositionChangeRecords.FromPlan(plan, snapshot, T0, ""));
    }

    private static GapRepairPlan UnreleasedPlan() =>
        GapRepairPlanner.Plan(
            new[] { new NumberingReportPart(1, "C1", null, true), new NumberingReportPart(2, "C3", null, true) },
            new MapReleaseStateSource(new[] { 1, 2 }.Select(id =>
                new KeyValuePair<int, FabricationReleaseState>(id, FabricationReleaseState.NotReleased))),
            isWholeModel: true);
}
