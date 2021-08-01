using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public bool isExterior { get; set; }

        public bool doEvaporate = true;
        public float radius = 0.5f;

        virtual public void Start()
        {
            float dist = state.playerTransform.position.magnitude;
            isExterior = (dist > radius);
            if (!isExterior)
            {
                // From the exterior Schwarzschild perspective, the player's coordinate radius from origin (less than the
                // coordinate distance from origin to event horizon) corresponds with the interior time.
                state.playerTransform.position = state.SpeedOfLight * state.TotalTimeWorld * state.playerTransform.position.normalized;

                // Theoretically, "first-person" local time extends infinitely back into the past and forward into the future,
                // but the limit points for 0 Hubble sphere radius at either extreme might have a finite total time
                // between the limit points.
                state.TotalTimeWorld = dist / state.SpeedOfLight;
                state.TotalTimePlayer = state.TotalTimeWorld;
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
            float r;
            float tau = properTDiff;
            float rsCubeRoot = Mathf.Pow(radius, 1.0f / 3.0f);
            float rho;

            if (isExterior)
            {
                r = piw.magnitude;
                rho = (2.0f * r * Mathf.Sqrt(r / rsCubeRoot)) / (3.0f * rsCubeRoot);
            } else {
                tau *= -1;
                rho = state.SpeedOfLight * state.TotalTimeWorld;
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
                diffT = diffR / state.SpeedOfLight;
                diffR = temp * state.SpeedOfLight;
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

        public void EnforceHorizon()
        {
            if (!isExterior && (state.TotalTimeWorld >= radius))
            {
                state.TotalTimeWorld = radius / state.SpeedOfLight;
                state.isMovementFrozen = true;
            }
        }

        virtual protected float deltaRadius
        {
            get
            {
                // This is speculative, but it can simply be turned off, in the editor.
                // This attempts to simulate black hole evaporation at a rate inversely proportional to Schwarzschild radius.
                // It's not properly Hawking radition, but this could be easily modified to approximate that instead.
                if (float.IsInfinity(state.DeltaTimeWorld) || float.IsNaN(state.DeltaTimeWorld))
                {
                    return 0;
                }

                float diffR;
                if (radius > state.planckLength)
                {
                    float cTo7 = Mathf.Pow(SRelativityUtil.c, 7.0f);
                    diffR = -state.DeltaTimeWorld * Mathf.Sqrt(state.hbarOverG * cTo7) * 2.0f / radius;
                }
                else if (isExterior)
                {
                    float timeToPlanck = Mathf.Sqrt(radius * 4.0f / state.SpeedOfLight - 4.0f * state.planckTime);
                    if (timeToPlanck <= state.DeltaTimeWorld)
                    {
                        radius = state.planckLength;

                        float cTo7 = Mathf.Pow(SRelativityUtil.c, 7.0f);
                        diffR = -(state.DeltaTimeWorld - timeToPlanck) * Mathf.Sqrt(state.hbarOverG * cTo7) * 2.0f / radius;
                    }
                    else
                    {
                        diffR = -state.DeltaTimeWorld * state.planckLength / (2.0f * state.planckTime);
                    }
                }
                else
                {
                    diffR = -state.DeltaTimeWorld * state.planckLength / (2.0f * state.planckTime);
                }

                if (!isExterior)
                {
                    diffR = -diffR;
                }

                return diffR;
            }
        }

        virtual public void Update()
        {
            EnforceHorizon();

            if (radius <= 0 || !doEvaporate || state.isMovementFrozen)
            {
                return;
            }

            radius = radius + deltaRadius;

            if (radius < 0)
            {
                radius = 0;
            }
        }
    }
}
