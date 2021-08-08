using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class KerrNewman : Kerr
    {
        public float electricCharge;

        protected float chargeRadiusDiff = 0.0f;

        override public void SetEffectiveRadius(Vector3 piw)
        {
            if (electricCharge <= SRelativityUtil.divByZeroCutoff)
            {
                base.SetEffectiveRadius(piw);
                return;
            }

            chargeRadiusDiff = state.gConst * electricCharge * electricCharge / (state.SpeedOfLightSqrd * piw.magnitude);
            schwarzschildRadius -= chargeRadiusDiff;

            base.SetEffectiveRadius(piw);
        }

        override public void ResetSchwarschildRadius()
        {
            base.ResetSchwarschildRadius();

            schwarzschildRadius += chargeRadiusDiff;
            chargeRadiusDiff = 0.0f;
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