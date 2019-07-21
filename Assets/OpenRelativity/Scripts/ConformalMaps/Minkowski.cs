using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Matrix4x4 LocalToWorld(Vector4 stpiw)
        {
            return Matrix4x4.identity;
        }

        override public Matrix4x4 WorldToLocal(Vector4 stpiw)
        {
            return Matrix4x4.identity;
        }

        public override Matrix4x4 WorldCoordMetric(Vector4 stpiw)
        {
            // By convention, all of our metrics map to proper DISTANCE, as opposed to proper TIME.
            // (The metric is "intrinsic," i.e. proper distances are independent of coordinates,
            // except, numerically, we're actually always expecting inputs in a particular coordinate system,
            // and this is specifically the Unity "world" coordinate system, for this abstract method.)
            Matrix4x4 metric = Matrix4x4.identity;

            // "3" or "w" is the time index, and seconds * (meters / second) gives a quantity with units of "meters", (i.e. "distance").
            // The metric is "two-form" which can be used to find the square norm of a vector (by an inner product) so c^2 rather than c.
            metric[3, 3] = -SRelativityUtil.cSqrd;

            return metric;
        }
    }
}
