using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        public Matrix4x4 metric;

        abstract public Matrix4x4 GetConformalFactor(Vector4 piw);

        public double GetProperTimeBetween(Vector4 startPiw, Vector4 endPiw)
        {
            Vector4 interval = endPiw - startPiw;
            return Vector3.Dot(interval, metric * interval);
        }
    }
}
