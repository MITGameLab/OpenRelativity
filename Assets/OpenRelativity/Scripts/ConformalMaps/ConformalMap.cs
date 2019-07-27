using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        public GameState state { get; set; }

        abstract public Vector4 ComoveOptical(float properTDiff, Vector3 piw);
    }
}
