using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        public GameState state { get; set; }

        abstract public Vector3 ComovePlayer(float properTDiff, Vector3 piw);
    }
}
