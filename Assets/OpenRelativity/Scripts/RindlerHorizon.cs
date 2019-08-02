using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity
{
    public class RindlerHorizon : MonoBehaviour
    {
        public GameState state;

        private Renderer myRenderer;

        private const float MIN_ACCEL = 0.01f;

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

            if (pAccelMag < MIN_ACCEL)
            {
                myRenderer.enabled = false;
                return;
            }

            // TODO: Make this for kinematic RelativisticObject

            myRenderer.enabled = true;
            // Quads face "backwards," if we use a default Unity quad.
            Vector3 frwd = -pAccel / pAccelMag;
            transform.forward = frwd;
            Vector3 pos = (frwd * (float)state.SpeedOfLightSqrd / pAccelMag) + state.playerTransform.position;
            //transform.Translate(transform.InverseTransformPoint(pos - transform.position));
            transform.position = pos;
        }
    }
}
