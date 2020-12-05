using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        //Keep track of Game State so that we can reference it quickly.
        private void FetchState()
        {
            _state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
        }
        private GameState _state;
        public GameState state
        {
            get
            {
                if (_state == null)
                {
                    FetchState();
                }

                return _state;
            }
        }

        abstract public Vector4 ComoveOptical(float properTDiff, Vector3 piw);
        abstract public Vector3 GetRindlerAcceleration(Vector3 piw);
    }
}
