using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Matrix4x4 GetConformalFactor(Vector4 stpiw)
        {
            return Matrix4x4.identity;
        }

        override public Matrix4x4 GetMetric(Vector4 stpiw)
        {
            Matrix4x4 metric = Matrix4x4.identity;
            metric[0, 0] = -1;
            metric[1, 1] = -1;
            metric[2, 2] = -1;
            metric[3, 3] = SRelativityUtil.cSqrd;
            return metric;
        }

        //public override Vector3 GetComovingPseudoVelocity(Vector3 piw, Vector3 playerPos)
        //{
        //    return Vector3.zero;
        //}

        public override Matrix4x4[] GetWorldChristoffelSymbols(Vector3 piw, Vector3 playerPos)
        {
            Matrix4x4[] christoffels = new Matrix4x4[4];
            for (int i = 0; i < christoffels.Length; i++)
            {
                christoffels[i] = Matrix4x4.zero;
            }

            return christoffels;
        }
    }
}
