using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        public void Awake() {
            metric = Matrix4x4.identity;
            metric[0, 0] = -1;
            metric[1, 1] = -1;
            metric[2, 2] = -1;
            metric[3, 3] = 1;
        }

        override public Matrix4x4 GetConformalFactor(Vector4 stpiw)
        {
            return Matrix4x4.identity;
        }
    }
}
