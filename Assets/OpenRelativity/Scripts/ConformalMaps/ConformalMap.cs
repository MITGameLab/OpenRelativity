using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Comovement
    {
        public Vector4 piw { get; set; }
        public Quaternion riw { get; set; }
    }

    public abstract class ConformalMap : RelativisticBehavior
    {
        abstract public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw);
        abstract public Vector3 GetRindlerAcceleration(Vector3 piw);
        abstract public Vector3 GetFreeFallVelocity(Vector3 piw);
    }
}
