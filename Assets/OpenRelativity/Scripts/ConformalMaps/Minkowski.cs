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
    }
}
