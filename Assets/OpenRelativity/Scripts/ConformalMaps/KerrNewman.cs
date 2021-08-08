using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class KerrNewman : Kerr
    {
        public float electricCharge;

        protected float chargeRadiusDiff = 0.0f;

        override public void SetEffectiveRadius(Vector3 piw)
        {
            // Calculate and set spin difference, based on invariant radius.
            base.SetEffectiveRadius(piw);

            if (electricCharge <= SRelativityUtil.divByZeroCutoff)
            {
                chargeRadiusDiff = 0.0f;
                return;
            }

            // Calculate and set charge difference, (which does not depend on invariant radius).
            chargeRadiusDiff = state.gConst * electricCharge * electricCharge / (state.SpeedOfLightSqrd * piw.magnitude);
            schwarzschildRadius -= chargeRadiusDiff;
        }

        override public void ResetSchwarschildRadius()
        {
            schwarzschildRadius += chargeRadiusDiff;
            chargeRadiusDiff = 0.0f;
            base.ResetSchwarschildRadius();
        }

        override public void Update()
        {
            EnforceHorizon();

            if (schwarzschildRadius <= 0 || !doEvaporate || state.isMovementFrozen)
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

            float constRatio = state.planckCharge / state.planckLength;
            float extremalFrac = electricCharge / (schwarzschildRadius * constRatio);
            electricCharge += extremalFrac * deltaR * constRatio;

            constRatio = state.planckMomentum;
            extremalFrac = spinMomentum / (schwarzschildRadius * constRatio);
            spinMomentum += extremalFrac * deltaR * constRatio;
        }
    }
}