using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            Vector4 piw4 = piw;
            piw4.w = properTDiff;
            return new Comovement
            {
                piw = piw4,
                riw = riw
            };
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            return Vector3.zero;
        }

        public override Vector3 GetFreeFallVelocity(Vector3 piw)
        {
            return Vector3.zero;
        }

        public override Matrix4x4 GetMetric(Vector3 piw)
        {
            return new Matrix4x4(
                new Vector4(-1, 0, 0, 0),
                new Vector4(0, -1, 0, 0),
                new Vector4(0, 0, -1, 0),
                new Vector4(0, 0, 0, 1)
            );
        }
    }
}
