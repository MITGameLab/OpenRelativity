using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class MonopoleSchwarzschild : ConformalMap
    {
        public Transform eventHorizon;
        public float radius = 1;
        public float radiusCutoff = 1;
        public float hbarOverG = 7.038e-45f; // m^5/s^3

        override public Vector3 ComovePlayer(float properTDiff, Vector3 piw)
        {
            if (radius < radiusCutoff)
            {
                return piw;
            }

            // Assume that the spatial component is in world coordinates, and the time is in local time 
            float r = piw.magnitude;
            float tau = properTDiff;
            float rho = 2.0f / 3.0f * Mathf.Sqrt(Mathf.Pow(r, 3.0f) / radius) + tau;
            float diffR = Mathf.Pow(2 * radius / (rho - tau), 1.0f / 3.0f);
            r -= diffR;

            return piw.normalized * r;
        }

        void FixedUpdate()
        {
            if (!double.IsInfinity(state.FixedDeltaTimeWorld) && !double.IsNaN(state.FixedDeltaTimeWorld))
            {
                float cTo7 = Mathf.Pow(SRelativityUtil.c, 7.0f);
                radius = radius - ((float)state.FixedDeltaTimeWorld * Mathf.Sqrt(hbarOverG * cTo7) * 2.0f / radius);
            }

            if (radius < radiusCutoff)
            {
                radius = 0;
            }

            eventHorizon.localScale = new Vector3(radius, radius, radius);
        }
    }
}
