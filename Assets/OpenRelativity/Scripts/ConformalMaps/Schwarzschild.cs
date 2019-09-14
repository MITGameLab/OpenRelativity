using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public bool isExterior { get; set; }

        public Transform eventHorizon;
        public bool doEvaporate = true;
        public float radius = 1;

        public void Start()
        {
            float dist = state.playerTransform.position.magnitude;
            isExterior = (dist > radius);
            if (!isExterior)
            {
                state.TotalTimeWorld = Math.Tan(dist / radius * Math.PI / 2) * radius / state.SpeedOfLight;
                state.TotalTimePlayer = state.TotalTimeWorld;
                state.playerTransform.position = Vector3.zero;
            }
        }

        override public Vector4 ComoveOptical(float properTDiff, Vector3 piw)
        {
            if (radius <= 0)
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
            float rho;

            if (isExterior)
            {
                r = piw.magnitude;
                rho = (2.0f * r * Mathf.Sqrt(r / rsCubeRoot)) / (3.0f * rsCubeRoot);
            } else {
                tau *= -1;
                rho = (float)(state.SpeedOfLight * state.TotalTimeWorld);
                r = Mathf.Pow(rho / (2 * rsCubeRoot), 2.0f / 3.0f);
            }

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

            if (!isExterior)
            {
                float temp = diffT;
                diffT = diffR / (float)state.SpeedOfLight;
                diffR = temp * (float)state.SpeedOfLight;
            }

            Vector4 piw4 = piw + piw.normalized * diffR;
            piw4.w = diffT;

            return piw4;
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            if (isExterior)
            {
                return radius * SRelativityUtil.cSqrd / (2 * piw.sqrMagnitude) * piw.normalized;
            }

            return Vector3.zero;
        }

        void FixedUpdate()
        {
            if (radius <= 0 || !doEvaporate || state.MovementFrozen)
            {
                return;
            }

            // This is speculative, but it can simply be turned off, in the editor.
            // This attempts to simulate black hole evaporation at a rate inversely proportional to Schwarzschild radius.
            // It's not properly Hawking radition, but this could be easily modified to approximate that instead.
            if (!double.IsInfinity(state.FixedDeltaTimeWorld) && !double.IsNaN(state.FixedDeltaTimeWorld))
            {
                float diffR;
                if (radius > state.planckLength)
                {
                    float cTo7 = Mathf.Pow(SRelativityUtil.c, 7.0f);
                    diffR = (float)-state.FixedDeltaTimeWorld * Mathf.Sqrt(state.hbarOverG * cTo7) * 2.0f / radius;
                } else
                {
                    diffR = (float)-state.FixedDeltaTimeWorld * state.planckLength / (2.0f * state.planckTime);
                }

                if (!isExterior)
                {
                    diffR *= -1;
                }
                radius = radius + diffR;
            }

            if (radius < 0)
            {
                radius = 0;
            }

            eventHorizon.localScale = new Vector3(radius, radius, radius);
        }
    }
}
