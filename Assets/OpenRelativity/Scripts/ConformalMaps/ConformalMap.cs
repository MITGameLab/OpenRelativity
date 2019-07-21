using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        abstract public Matrix4x4 LocalToWorld(Vector4 stpiw);
        abstract public Matrix4x4 WorldToLocal(Vector4 pstpiw);
    }
}
