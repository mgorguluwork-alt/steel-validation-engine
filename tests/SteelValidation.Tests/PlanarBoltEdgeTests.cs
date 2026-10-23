using System;
using System.Linq;
using SteelValidation.Core.Bolts;
using Xunit;

namespace SteelValidation.Tests;
public sealed class PlanarBoltEdgeTests
{
    private static BoltCenter P(double x,double y,double z=0)=>new(0,x,y,z);
    private static BoltCenter[] Square()=>new[]{P(0,0),P(200,0),P(200,200),P(0,200)};
    private static BoltCenter Axis()=>P(0,0,1);
    [Fact] public void RealStraightBoundaryDistanceEqualsFortyAtBoltCenter()
    {Assert.Equal(40,PlanarBoltEdge.Measure(Square(),P(40,100),Axis())!.Value,8);}
    [Fact] public void ActualDiagonalContourIsNotItsBoundingBox()
    {
        var cutCorner=new[]{P(0,0),P(200,0),P(200,120),P(120,200),P(0,200)};
        var edge=PlanarBoltEdge.Measure(cutCorner,P(150,150),Axis());
        Assert.NotNull(edge);Assert.Equal(20/Math.Sqrt(2),edge!.Value,8);
        var input=new BoltDistanceGroupInput(1,2,20,new[]{new BoltCenter(0,150,150,0)},
            new[]{new BoltEdgeMeasurement(0,edge,true)},true,"MODEL");
        var finding=BoltDistanceChecker.Check(new[]{input}).Items.Single(x=>x.RuleKind==BoltDistanceRuleKind.Edge);
        Assert.Equal(BoltDistanceOutcome.Fail,finding.Outcome);Assert.Equal(40,finding.RequiredMm);
    }
    [Fact] public void OutsideDegenerateNonPlanarAndConcaveContoursAreUnknown()
    {
        Assert.Null(PlanarBoltEdge.Measure(Square(),P(250,100),Axis()));
        Assert.Null(PlanarBoltEdge.Measure(new[]{P(0,0),P(50,0),P(100,0)},P(20,0),Axis()));
        Assert.Null(PlanarBoltEdge.Measure(new[]{P(0,0),P(200,0),P(200,200,5),P(0,200)},P(40,100),Axis()));
        Assert.Null(PlanarBoltEdge.Measure(new[]{P(0,0),P(200,0),P(100,50),P(200,200),P(0,200)},P(40,100),Axis()));
    }
    [Fact] public void SelfCrossingContourDoesNotProduceEdgeEvidence()
    {Assert.Null(PlanarBoltEdge.Measure(new[]{P(0,0),P(200,200),P(0,200),P(200,0)},P(100,100),Axis()));}
    [Fact] public void UnknownAndNonFiniteInputsRemainUnknown()
    {
        Assert.Null(PlanarBoltEdge.Measure(Square(),new BoltCenter(0,null,100,0),Axis()));
        Assert.Null(PlanarBoltEdge.Measure(Square(),P(double.NaN,100),Axis()));
        Assert.Null(PlanarBoltEdge.Measure(Square(),P(40,100),P(0,0,0)));
        Assert.Null(PlanarBoltEdge.Measure(new[]{P(0,0),P(double.PositiveInfinity,0),P(200,200)},P(40,100),Axis()));
    }
    [Fact] public void TiltedAxisCannotFalselyPassNominalMidplaneDistance()
    {
        // Orthogonal projection gives 40.05 (false PASS at d20); true z=0 intersection is39.95.
        // Physical front/back-face geometry of a tilted hole is unsupported, so the result must remain unknown.
        var edge=PlanarBoltEdge.Measure(Square(),P(40.05,100,1000),P(0.0001,0,1));
        Assert.Null(edge);
        var input=new BoltDistanceGroupInput(1,2,20,new[]{new BoltCenter(0,40.05,100,1000)},
            new[]{new BoltEdgeMeasurement(0,edge,false,"Tilted axis unsupported")},true,"MODEL");
        var finding=BoltDistanceChecker.Check(new[]{input}).Items.Single(x=>x.RuleKind==BoltDistanceRuleKind.Edge);
        Assert.Equal(BoltDistanceOutcome.Unknown,finding.Outcome);
    }
    [Fact] public void NormalAxisLineCanStartAwayFromPlatePlane()
    {Assert.Equal(40,PlanarBoltEdge.Measure(Square(),P(40,100,1000),Axis())!.Value,8);}
    [Fact] public void RigidTransformAndReversedContourPreserveActualEdgeDistance()
    {
        var c=Math.Cos(0.6);var s=Math.Sin(0.6);
        // Rotate about X, then translate. Axis is transformed as a vector, never translated.
        BoltCenter Move(BoltCenter p)=>P(1000+p.XMm!.Value,-500+c*p.YMm!.Value-s*p.ZMm!.Value,300+s*p.YMm.Value+c*p.ZMm.Value);
        var contour=Square().Select(Move).Reverse().ToArray();
        var axis=P(0,-s,c);
        Assert.Equal(40,PlanarBoltEdge.Measure(contour,Move(P(40,100,20)),axis)!.Value,6);
    }
    [Fact] public void BoltExactlyOnPhysicalBoundaryHasZeroEdgeDistance()
    {Assert.Equal(0,PlanarBoltEdge.Measure(Square(),P(0,100),Axis())!.Value,8);}
    [Fact] public void ClosedContourWithRepeatedFinalPointIsAccepted()
    {Assert.Equal(40,PlanarBoltEdge.Measure(Square().Concat(new[]{P(0,0)}).ToArray(),P(40,100),Axis())!.Value,8);}
    [Fact] public void CenterJustOutsideContourCannotPassUsingTinyPositiveDiameter()
    {
        var edge=PlanarBoltEdge.Measure(Square(),P(-0.0000005,100),Axis());
        Assert.Null(edge);
        var g=new BoltDistanceGroupInput(1,2,0.00000001,new[]{new BoltCenter(0,-0.0000005,100,0)},
            new[]{new BoltEdgeMeasurement(0,edge,false)},true,"MODEL");
        Assert.Equal(BoltDistanceOutcome.Unknown,BoltDistanceChecker.Check(new[]{g}).Items.Single(x=>x.RuleKind==BoltDistanceRuleKind.Edge).Outcome);
    }

}
