using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class KerrNewman : Kerr
    {
        public float electricCharge;

        protected float chargeRadiusDiff = 0;

        override public void SetEffectiveRadius(Vector3 piw)
        {
            // Calculate and set spin difference, based on invariant radius.
            base.SetEffectiveRadius(piw);

            if (electricCharge <= SRelativityUtil.FLT_EPSILON)
            {
                chargeRadiusDiff = 0;
                return;
            }

            // Calculate and set charge difference, (which does not depend on invariant radius).
            chargeRadiusDiff = (float)(state.gConst * electricCharge * electricCharge / (state.SpeedOfLightSqrd * piw.magnitude));
            schwarzschildRadius -= chargeRadiusDiff;
        }

        override public void ResetSchwarschildRadius()
        {
            schwarzschildRadius += chargeRadiusDiff;
            chargeRadiusDiff = 0;
            base.ResetSchwarschildRadius();
        }

        override public void Start()
        {
            float dist = state.playerTransform.position.magnitude;
            float chargeRadius = (float)Math.Sqrt(electricCharge * electricCharge * state.gConst / (4 * Math.PI * state.vacuumPermittivity * state.SpeedOfLightSqrd * state.SpeedOfLightSqrd));
            float radiusRoot = Mathf.Sqrt(schwarzschildRadius * schwarzschildRadius - 4 * chargeRadius * chargeRadius);
            float exteriorRadius = schwarzschildRadius + radiusRoot;
            float cauchyRadius = schwarzschildRadius - radiusRoot;
            isExterior = (dist > exteriorRadius) || (dist < cauchyRadius);

            if (!isExterior)
            {
                SetEffectiveRadius((dist - cauchyRadius) * state.playerTransform.position / dist);

                // From the exterior Schwarzschild perspective, the player's coordinate radius from origin (less than the
                // coordinate distance from origin to event horizon) corresponds with the interior time.
                state.playerTransform.position = state.SpeedOfLight * state.TotalTimeWorld * state.playerTransform.position.normalized;

                // Theoretically, "first-person" local time extends infinitely back into the past and forward into the future,
                // but the limit points for 0 Hubble sphere radius at either extreme might have a finite total time
                // between the limit points.
                state.TotalTimeWorld = dist / state.SpeedOfLight;
                state.TotalTimePlayer = state.TotalTimeWorld;

                ResetSchwarschildRadius();
            }
        }

        override public void Update()
        {
            EnforceHorizon();

            if (schwarzschildRadius == 0 || !doEvaporate || state.isMovementFrozen)
            {
                return;
            }

            float deltaR = deltaRadius;

            schwarzschildRadius += deltaR;

            if (schwarzschildRadius <= 0)
            {
                schwarzschildRadius = 0;
                electricCharge = 0;
                spinMomentum = 0;

                return;
            }

            if (electricCharge <= 0)
            {
                electricCharge = 0;
                return;
            }

            if (spinMomentum <= 0)
            {
                spinMomentum = 0;
                return;
            }

            float constRatio = (float)(state.planckCharge / state.planckLength);
            float extremalFrac = electricCharge / (schwarzschildRadius * constRatio);
            electricCharge += extremalFrac * deltaR * constRatio;

            constRatio = (float)state.planckMomentum;
            extremalFrac = spinMomentum / (schwarzschildRadius * constRatio);
            spinMomentum += extremalFrac * deltaR * constRatio;
        }
    }
}