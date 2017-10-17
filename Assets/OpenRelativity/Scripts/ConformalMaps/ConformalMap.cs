using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        abstract public Matrix4x4 GetConformalFactor(Vector4 stpiw);
        abstract public Matrix4x4 GetMetric(Vector4 stpiw);

        //Properly, we want to satisfy the geodesic equations, using the Christoffel symbols.
        abstract public Matrix4x4[] GetWorldChristoffelSymbols(Vector3 piw, Vector3 playerPos);
    }
}
