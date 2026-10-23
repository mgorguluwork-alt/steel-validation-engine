using System;
using System.Linq;
using SteelValidation.Core.Bolts;
using Xunit;

namespace SteelValidation.Tests;
public sealed class BoltDistanceCheckTests
{
    private static BoltCenter P(int i, double? x, double? y = 0, double? z = 0) => new(i, x, y, z);
    private static BoltDistanceGroupInput G(double? d = 20, BoltCenter[]? centers = null, BoltEdgeMeasurement[]? edges = null,
        int group = 10, int target = 100, bool complete = true, string? frame = "MODEL", bool? bolts = true) =>
        new(group, target, d, centers ?? new[] { P(0,0), P(1,60) }, edges ?? new[] { new BoltEdgeMeasurement(0,40,true), new BoltEdgeMeasurement(1,40,true) }, complete, frame, bolts);
    private static BoltDistanceReport Check(BoltDistanceGroupInput group) => BoltDistanceChecker.Check(new[] { group });
    private static BoltDistanceFinding[] Pitch(BoltDistanceReport r) => r.Items.Where(i => i.RuleKind == BoltDistanceRuleKind.Pitch).ToArray();

    [Fact] public void EqualityAtTwoDAndThreeDPassesWithActualValues()
    {
        var r = Check(G()); Assert.True(r.AllChecksPassed); Assert.Equal(4,r.CountsPass);
        Assert.All(r.Items, i => Assert.Equal(20,i.NominalDiameterMm));
        Assert.All(Pitch(r), i => { Assert.Equal(60,i.ActualMm); Assert.Equal(60,i.RequiredMm); Assert.Equal(3,i.ActualDiameterRatio); });
        Assert.Equal("WithinBoltGroupOnly",r.CoverageCode);
    }
    [Fact] public void Measured35Fails40And59Point9Fails60()
    {
        var r=Check(G(centers:new[]{P(0,0),P(1,59.9)},edges:new[]{new BoltEdgeMeasurement(0,35,true),new BoltEdgeMeasurement(1,40,true)}));
        var edge=r.Items.Single(x=>x.RuleKind==BoltDistanceRuleKind.Edge&&x.BoltCenterIndex==0);
        Assert.Equal(BoltDistanceOutcome.Fail,edge.Outcome); Assert.Equal(35,edge.ActualMm); Assert.Equal(40,edge.RequiredMm);
        Assert.All(Pitch(r),x=>Assert.Equal(BoltDistanceOutcome.Fail,x.Outcome));
    }
    [Theory] [InlineData(0)] [InlineData(-20)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidNominalDiameterNeverPasses(double d)
    {
        var r=Check(G(d)); Assert.Equal(0,r.CountsPass); Assert.Equal(4,r.CountsUnknown);
        Assert.All(r.Items,x=>{Assert.Null(x.RequiredMm);Assert.Null(x.NominalDiameterMm);});
    }
    [Fact] public void MissingDiameterNeverUsesHoleDiameterOrInventsThreshold()
    { var r=Check(G(null));Assert.Equal(4,r.CountsUnknown);Assert.All(r.Items,x=>Assert.Null(x.RequiredMm)); }
    [Fact] public void CoincidentDifferentBoltIndicesFailPitchAtZero()
    {var r=Check(G(centers:new[]{P(0,0),P(1,0)}));Assert.All(Pitch(r),x=>{Assert.Equal(0,x.ActualMm);Assert.Equal(BoltDistanceOutcome.Fail,x.Outcome);});}
    [Fact] public void DuplicateCenterIdentityIsUnknownInsteadOfZeroDistance()
    {var r=Check(G(centers:new[]{P(0,0),P(0,60)}));Assert.Equal(2,r.CountsUnknown);Assert.Empty(r.ClosestPitchPairs);Assert.All(r.Items,x=>Assert.Equal("InvalidCenterIdentity",x.ReasonCode));}
    [Fact] public void MissingNanOrIncompleteCoordinatesCannotVerifyNearestPair()
    {
        foreach(var g in new[]{G(centers:new[]{P(0,0),P(1,null)}),G(centers:new[]{P(0,0),P(1,double.NaN)}),G(complete:false),G(frame:null)})
        {var r=Check(g);Assert.All(Pitch(r),x=>{Assert.Equal(BoltDistanceOutcome.Unknown,x.Outcome);Assert.Null(x.ActualMm);});Assert.Empty(r.ClosestPitchPairs);}
    }
    [Fact] public void UnverifiedEdgeCannotExposeBoundingBoxValueAsActual()
    {
        var r=Check(G(edges:new[]{new BoltEdgeMeasurement(0,100,false,"bbox is not boundary")}));
        Assert.All(r.Items.Where(x=>x.RuleKind==BoltDistanceRuleKind.Edge),x=>{Assert.Equal(BoltDistanceOutcome.Unknown,x.Outcome);Assert.Null(x.ActualMm);});
    }
    [Theory] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidVerifiedEdgeMeasurementRemainsUnknown(double edge)
    {var r=Check(G(edges:new[]{new BoltEdgeMeasurement(0,edge,true)}));Assert.All(r.Items.Where(x=>x.RuleKind==BoltDistanceRuleKind.Edge),x=>Assert.Equal(BoltDistanceOutcome.Unknown,x.Outcome));}
    [Fact] public void OneBoltHasNoPitchPairAndDoesNotBecomeOverallPass()
    {var r=Check(G(centers:new[]{P(0,0)}));Assert.Single(Pitch(r));Assert.Equal(BoltDistanceOutcome.NotApplicable,Pitch(r)[0].Outcome);Assert.False(r.AllChecksPassed);Assert.Empty(r.ClosestPitchPairs);}
    [Fact] public void DifferentGroupsAreNeverComparedEvenWhenCentersCoincide()
    {
        var r=BoltDistanceChecker.Check(new[]{G(group:1,centers:new[]{P(0,0)}),G(group:2,centers:new[]{P(0,0)})});
        Assert.Equal(2,r.GroupsScanned);Assert.All(Pitch(r),x=>Assert.Equal(BoltDistanceOutcome.NotApplicable,x.Outcome));Assert.Empty(r.ClosestPitchPairs);
    }
    [Fact] public void PitchIsThreeDimensionalNotProjectedToZero()
    {var r=Check(G(centers:new[]{P(0,0),P(1,0,0,60)}));Assert.All(Pitch(r),x=>{Assert.Equal(60,x.ActualMm);Assert.Equal(BoltDistanceOutcome.Pass,x.Outcome);});}
    [Fact] public void ClosestActualPairAndEachNearestNeighborAreRecorded()
    {
        var r=Check(G(centers:new[]{P(0,0),P(1,100),P(2,61)}));var closest=Assert.Single(r.ClosestPitchPairs);
        Assert.Equal(1,closest.BoltCenterIndex);Assert.Equal(2,closest.OtherBoltCenterIndex);Assert.Equal(39,closest.ActualMm);
        var first=Pitch(r).Single(x=>x.BoltCenterIndex==0);Assert.Equal(2,first.OtherBoltCenterIndex);Assert.Equal(61,first.ActualMm);
    }
    [Fact] public void RigidTranslationAndRotationPreservePitchAndVerdict()
    {
        var c=Math.Cos(0.73);var s=Math.Sin(0.73);
        BoltCenter Move(BoltCenter p)=>P(p.Index,1000000+c*p.XMm!.Value-s*p.YMm!.Value,-2000000+s*p.XMm.Value+c*p.YMm.Value,500+p.ZMm!.Value);
        var points=new[]{P(0,0),P(1,18,24,0)};
        var a=Pitch(Check(G(10,points)));var b=Pitch(Check(G(10,points.Select(Move).ToArray())));
        Assert.All(a,x=>Assert.Equal(BoltDistanceOutcome.Pass,x.Outcome));Assert.All(b,x=>Assert.Equal(BoltDistanceOutcome.Pass,x.Outcome));
        Assert.Equal(a[0].ActualMm!.Value,b[0].ActualMm!.Value,6);
    }
    [Fact] public void TinyNumericalBoundaryAllowanceIsExplicitAndCannotWaiveRealDeficit()
    {
        var near=Pitch(Check(G(centers:new[]{P(0,0),P(1,60-0.000000001)})))[0];
        Assert.Equal(BoltDistanceOutcome.Pass,near.Outcome);Assert.Equal("NumericBoundary",near.ReasonCode);
        Assert.Equal(BoltDistanceOutcome.Fail,Pitch(Check(G(centers:new[]{P(0,0),P(1,59.999)})))[0].Outcome);
        Assert.All(Pitch(Check(G(1e-20,centers:new[]{P(0,0),P(1,0)}))),x=>Assert.Equal(BoltDistanceOutcome.Fail,x.Outcome));
    }
    [Fact] public void HoleOnlyAndUnknownBoltPresenceNeverPass()
    {
        Assert.All(Check(G(bolts:false)).Items,x=>Assert.Equal(BoltDistanceOutcome.NotApplicable,x.Outcome));
        Assert.All(Check(G(bolts:null)).Items,x=>Assert.Equal(BoltDistanceOutcome.Unknown,x.Outcome));
    }
    [Fact] public void UnknownTargetAndFiniteCoordinateOverflowFailClosed()
    {
        Assert.All(Check(G(target:0)).Items,x=>Assert.Equal(BoltDistanceOutcome.Unknown,x.Outcome));
        Assert.All(Pitch(Check(G(centers:new[]{P(0,double.MaxValue),P(1,-double.MaxValue)}))),x=>Assert.Equal(BoltDistanceOutcome.Unknown,x.Outcome));
    }
    [Fact] public void EmptyScopeDoesNotPassAndIncompleteLargeScopeHasNoPitchMeasurements()
    {
        Assert.False(BoltDistanceChecker.Check(Array.Empty<BoltDistanceGroupInput>()).AllChecksPassed);
        var g=G(centers:Enumerable.Range(0,2000).Select(i=>P(i,i)).ToArray(),complete:false,edges:Array.Empty<BoltEdgeMeasurement>());
        var r=Check(g);Assert.Equal(4000,r.CountsUnknown);Assert.Empty(r.ClosestPitchPairs);
    }
}
