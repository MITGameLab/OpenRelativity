using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        override public Vector3 ComovePlayer(float properTDiff, Vector3 piw)
        {
            return piw;
        }
    }
}
