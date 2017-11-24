using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        abstract public Matrix4x4 GetConformalFactor(Vector4 opticalStpiw, Vector4 pstpiw);
        abstract public Matrix4x4 GetMetric(Vector4 opticalStpiw, Vector4 pstpiw);

        //Properly, we want to satisfy the geodesic equations, using the Christoffel symbols.
        // This introduces a four acceleration.
        abstract public Vector4 GetWorldAcceleration(Vector3 opticalStpiw, Vector3 playerPos);
    }
}
