using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        abstract public Matrix4x4 LocalToWorld(Vector4 stpiw);
        abstract public Matrix4x4 WorldToLocal(Vector4 pstpiw);

        // By convention, all of our metrics map to proper DISTANCE, as opposed to proper TIME.
        // (The metric is "intrinsic," i.e. proper distances are independent of coordinates,
        // except, numerically, we're actually always expecting inputs in a particular coordinate system,
        // and this is specifically the Unity "world" coordinate system, for this abstract method.)
        abstract public Matrix4x4 WorldCoordMetric(Vector4 stpiw);
    }
}
