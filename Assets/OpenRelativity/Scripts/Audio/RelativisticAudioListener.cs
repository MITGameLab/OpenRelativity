using UnityEngine;

namespace OpenRelativity.Audio
{
    public class RelativisticAudioListener : RelativisticBehavior
    {
        public Vector3 piw {
            get
            {
                return playerAudioListener.transform.position;
            }
        }

        public Vector3 viw
        {
            get
            {
                return state.PlayerVelocityVector;
            }
        }

        protected AudioListener playerAudioListener;

        // Start is called before the first frame update
        void Start()
        {
            playerAudioListener = GetComponent<AudioListener>();
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}