using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : RelativisticBehavior
    {
        abstract public Vector4 ComoveOptical(float properTDiff, Vector3 piw);
        abstract public Vector3 GetRindlerAcceleration(Vector3 piw);
    }
}
