using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Matrix4x4 GetConformalFactor(Vector4 stpiw, Vector4 pstpiw)
        {
            return Matrix4x4.identity;
        }

        override public Matrix4x4 GetMetric(Vector4 stpiw, Vector4 pstpiw)
        {
            Matrix4x4 metric = Matrix4x4.identity;
            metric[0, 0] = -1;
            metric[1, 1] = -1;
            metric[2, 2] = -1;
            metric[3, 3] = SRelativityUtil.cSqrd;
            return metric;
        }

        public override Vector4 GetWorldAcceleration(Vector3 piw, Vector3 playerPiw)
        {
            return Vector4.zero;
        }
    }
}
