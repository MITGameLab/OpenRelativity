using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity
{
    public class RindlerHorizon : MonoBehaviour
    {
        public GameState state;

        private Renderer myRenderer;

        // Use this for initialization
        void Start()
        {
            myRenderer = GetComponent<Renderer>();
        }

        // Update is called once per frame
        void Update()
        {
            Vector3 pAccel = state.PlayerAccelerationVector;
            float pAccelMag = pAccel.magnitude;
            if (pAccelMag == 0)
            {
                myRenderer.enabled = false;
            } else
            {
                myRenderer.enabled = true;
                transform.forward = -pAccel / pAccelMag;
                transform.position = transform.forward * (float)state.SpeedOfLightSqrd / pAccelMag - state.playerTransform.position;
            }
        }
    }
}
