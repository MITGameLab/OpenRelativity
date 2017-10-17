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
        // However, we only pass the metric at one point, to the shader.
        // It would be cheaper to calculate the apparent velocity due to the Christoffel symbols, once,
        // and pass the apparent "velocity" to the rigidbody.
        abstract public Vector3 GetComovingPseudoVelocity(Vector3 piw, Vector3 playerPos);
    }
}
