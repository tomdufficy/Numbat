using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

private static List<Brep> CreateSoftZigZagInfill(Curve bayCurve, double bottomZ, double topZ, double diameter, double bayLength, double tolerance)
{
    var breps = new List<Brep>();
    var length = bayCurve.GetLength();

    if (length <= RhinoMath.ZeroTolerance || bayLength <= RhinoMath.ZeroTolerance || diameter <= RhinoMath.ZeroTolerance)
        return breps;

    var stationCount = Math.Max(1, (int)Math.Ceiling(length / bayLength));
    var actualBayLength = length / stationCount;

    var softCurve = new PolyCurve();

    for (var i = 0; i < stationCount; i++)
    {
        var startDistance = i * actualBayLength;
        var endDistance = (i + 1) * actualBayLength;

        if (endDistance > length)
            endDistance = length;

        var startZ = i % 2 == 0 ? topZ : bottomZ;
        var endZ = i % 2 == 0 ? bottomZ : topZ;

        var startPoint = PointAtDistanceAndZ(bayCurve, startDistance, startZ);
        var endPoint = PointAtDistanceAndZ(bayCurve, endDistance, endZ);

        var tangentLength = Math.Min(actualBayLength * 0.35, Math.Abs(topZ - bottomZ) * 0.25);

        var startHandle = startPoint;
        var endHandle = endPoint;

        if (startZ > endZ)
        {
            startHandle.Z -= tangentLength;
            endHandle.Z += tangentLength;
        }
        else
        {
            startHandle.Z += tangentLength;
            endHandle.Z -= tangentLength;
        }

        var bezier = NurbsCurve.Create(
            false,
            3,
            new List<Point3d>
            {
                startPoint,
                startHandle,
                endHandle,
                endPoint
            }
        );

        if (bezier != null)
            softCurve.Append(bezier);
    }

    if (softCurve.SegmentCount == 0)
        return breps;

    breps.AddRange(CreatePipe(softCurve, diameter, tolerance));
    return breps;
}