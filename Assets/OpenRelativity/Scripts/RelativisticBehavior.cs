using UnityEngine;

namespace OpenRelativity
{
    public class RelativisticBehavior : MonoBehaviour
    {
        private GameState _state;
        protected void FetchState()
        {
            _state = GameObject.FindGameObjectWithTag(Tags.player).GetComponent<GameState>();
        }
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
    }
}