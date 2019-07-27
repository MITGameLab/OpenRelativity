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

        override public Vector4 ComoveOptical(float properTDiff, Vector3 piw)
        {
            if (radius < radiusCutoff)
            {
                Vector4 toRet = piw;
                toRet.w = properTDiff;
                return toRet;
            }

            // Assume that the spatial component is in world coordinates, and the time is a local time differential 
            float r = piw.magnitude;
            float tau = properTDiff;
            float rsCubeRoot = Mathf.Pow(radius, 1.0f / 3.0f);
            // To begin, "tau" = 0;
            float rho = (2.0f * r * Mathf.Sqrt(r / rsCubeRoot)) / (3.0f * rsCubeRoot);

            // Partial differential, finite difference approach:
            //float diffR = Mathf.Pow(2 * radius / (rho - tau), 1.0f / 3.0f);
            //r -= diffR;
            // Unless we have a really small and/or adaptive finite difference time step, the above approximation fails close to the event horizon.

            // We can try the integral form, instead, with a major caveat...
            float nR = Mathf.Pow(radius * Mathf.Pow(3.0f / 2.0f * (rho - tau), 2.0f), 1.0f / 3.0f);
            float diffR = nR - r;
            // The equation we derive this closed-form integral from has many roots.
            // Some of these roots are not admissible without the existence of complex numbers.
            // Some are valid (real) when rho > tau, and some are valid when rho < tau.
            // (The above is real only when rho > tau, so rho better be greater than tau, in our inputs, at least.)
            // This neglects the change in "radius," the Schwarzschild radius, but we approximate that with a finite difference loop, in the FixedUpdate() method.

            // Remember that differential geometry gives us coordinate systems that are valid only over LOCAL regions, i.e. specifically NOT GLOBAL coordinate systems.
            // The coordinates are not "intrinsically" significant. "Intrinsic" properties, i.e. intrinsic curvature, an intrinsic property of the METRIC, is independent of coordinate systems.

            // All that said, the above should serve our purposes in the local region of interest.

            // The integral isn't as "nice" for time, and we approximate to lowest order:
            float diffT = Mathf.Log((radius - r) / diffR);

            Vector4 piw4 = piw.normalized * nR;
            piw4.w = diffT;

            return piw4;
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
