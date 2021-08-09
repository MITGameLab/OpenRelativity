using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw, Vector3 viw)
        {
            Vector4 piw4 = piw + properTDiff * viw;
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
    }
}
