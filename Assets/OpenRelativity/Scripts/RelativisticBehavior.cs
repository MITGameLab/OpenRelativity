using UnityEngine;

namespace OpenRelativity
{
    public class RelativisticBehavior : MonoBehaviour
    {
        public GameState state
        {
            get
            {
                return GameState.Instance;
            }
        }
    }
}