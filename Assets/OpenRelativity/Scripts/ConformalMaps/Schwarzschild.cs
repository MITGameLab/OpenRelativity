using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public bool isExterior { get; set; }

        public bool doEvaporate = true;
        public float schwarzschildRadius = 0.5f;

        protected System.Random rng = new System.Random();

        virtual public void SetEffectiveRadius(Vector3 piw)
        {
            //Intentionally left blank as a virtual interface.
        }

        virtual public void ResetSchwarschildRadius()
        {
            //Intentionally left blank as a virtual interface.
        }

        protected float fold
        {
            get
            {
                // Can we actually back-track to perfect 0 folds on the basis of exterior time?
                // We don't know exactly how long the evaporation will take, in the quantum limit.
                // If the black hole is "hairless," shouldn't this only depend on radius, rather than time?
                return (float)(Math.Log(schwarzschildRadius / state.planckLength) / Math.Log(2));
            }
        }

        virtual public void Start()
        {
            float dist = state.playerTransform.position.magnitude;
            isExterior = (dist > schwarzschildRadius);
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

        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            // Assume that the spatial component is in world coordinates, and the time is a local time differential
            double tau = properTDiff;
            double rsCubeRoot = Math.Pow(schwarzschildRadius, 1 / 3.0);
            double r;
            double rho;
            if (isExterior)
            {
                r = piw.magnitude;
                rho = (2 * r * Math.Sqrt(r / rsCubeRoot)) / (3 * rsCubeRoot);
            } else {
                tau *= -1;
                rho = state.SpeedOfLight * state.TotalTimeWorld;
                r = Math.Pow(rho / (2 * rsCubeRoot), 2 / 3.0);
            }

            // Partial differential, finite difference approach:
            //double diffR = Mathf.Pow(2 * radius / (rho - tau), 1 / 3.0f);
            //r -= diffR;
            // Unless we have a really small and/or adaptive finite difference time step, the above approximation fails close to the event horizon.

            // We can try the integral form, instead, with a major caveat...
            double nR = Math.Pow(schwarzschildRadius * Math.Pow(3.0 / 2 * (rho - tau), 2), 1 / 3.0);
            double diffR = nR - r;
            // The equation we derive this closed-form integral from has many roots.
            // Some of these roots are not admissible without the existence of complex numbers.
            // Some are valid (real) when rho > tau, and some are valid when rho < tau.
            // (The above is real only when rho > tau, so rho better be greater than tau, in our inputs, at least.)
            // This neglects the change in "radius," the Schwarzschild radius, but we approximate that with a finite difference loop, in the FixedUpdate() method.

            // Remember that differential geometry gives us coordinate systems that are valid only over LOCAL regions, i.e. specifically NOT GLOBAL coordinate systems.
            // The coordinates are not "intrinsically" significant. "Intrinsic" properties, i.e. intrinsic curvature, an intrinsic property of the METRIC, is independent of coordinate systems.

            // All that said, the above should serve our purposes in the local region of interest.

            // The integral isn't as "nice" for time, and we approximate to lowest order:
            double diffT = Math.Log((schwarzschildRadius - r) / diffR);

            if (!isExterior)
            {
                double temp = diffT;
                diffT = diffR / state.SpeedOfLight;
                diffR = temp * state.SpeedOfLight;
            }

            Vector4 piw4 = piw + piw.normalized * (float)diffR;
            piw4.w = (float)diffT;

            return new Comovement
            {
                piw = piw4,
                riw = riw
            };
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            if (isExterior)
            {
                return schwarzschildRadius * SRelativityUtil.cSqrd / (2 * piw.sqrMagnitude) * piw.normalized;
            }

            return Vector3.zero;
        }

        public override Vector3 GetFreeFallVelocity(Vector3 piw)
        {
            if (!isExterior)
            {
                return Vector3.zero;
            }

            float r = piw.magnitude;

            return -state.SpeedOfLight * Mathf.Sqrt(schwarzschildRadius / r) * (piw / r);
        }

        public void EnforceHorizon()
        {
            if (!isExterior && (state.TotalTimeWorld >= schwarzschildRadius))
            {
                state.TotalTimeWorld = schwarzschildRadius / state.SpeedOfLight;
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
                double r = SRelativityUtil.EffectiveRaditiativeRadius(schwarzschildRadius, state.gravityBackgroundPlanckTemperature);

                double diffR;
                if (r > state.planckLength)
                {
                    diffR = SRelativityUtil.SchwarzschildRadiusDecay(state.DeltaTimeWorld, r);
                }
                else
                {
                    diffR = -state.DeltaTimeWorld * state.planckLength / (2 * state.planckTime);
                }

                if (!isExterior)
                {
                    diffR = -diffR;
                }

                if ((schwarzschildRadius + diffR) < 0)
                {
                    diffR = -schwarzschildRadius;
                }

                return (float)diffR;
            }
        }

        virtual public void Update()
        {
            EnforceHorizon();

            if (schwarzschildRadius <= 0)
            {
                schwarzschildRadius = 0;
                return;
            }

            if (!doEvaporate || state.isMovementFrozen)
            {
                return;
            }

            float deltaT = state.FixedDeltaTimeWorld;
            float powf = Mathf.Pow(2, fold);
            float deltaF = (float)((isExterior ? -deltaT : deltaT) / (state.planckTime * powf));
            float deltaR = powf * deltaF * (float)rng.NextDouble() / 2;
            float thermoDeltaR = deltaRadius;

            schwarzschildRadius += (isExterior != (deltaR > thermoDeltaR)) ? thermoDeltaR : deltaR;

            if (schwarzschildRadius < 0)
            {
                schwarzschildRadius = 0;
            }
        }
    }
}
