using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Comotion ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            Vector4 piw4 = piw;
            piw4.w = properTDiff;
            return new Comotion
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
