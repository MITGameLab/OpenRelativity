using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class ReissnerNordstroem : Schwarzschild
    {
        public float electricCharge;

        protected float chargeRadiusDiff;

        override public void SetEffectiveRadius(Vector3 piw)
        {
            if (electricCharge <= SRelativityUtil.FLT_EPSILON)
            {
                chargeRadiusDiff = 0.0f;
                return;
            }

            chargeRadiusDiff = state.gConst * electricCharge * electricCharge / (state.SpeedOfLightSqrd * piw.magnitude);
            schwarzschildRadius -= chargeRadiusDiff;
        }

        override public void ResetSchwarschildRadius()
        {
            schwarzschildRadius += chargeRadiusDiff;
            chargeRadiusDiff = 0.0f;
        }

        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            SetEffectiveRadius(piw);

            Comovement schwarzComovement = base.ComoveOptical(properTDiff, piw, riw);

            ResetSchwarschildRadius();

            return schwarzComovement;
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            SetEffectiveRadius(piw);

            Vector3 schwarzAccel = base.GetRindlerAcceleration(piw);

            ResetSchwarschildRadius();

            return schwarzAccel;
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
                return;
            }

            if (electricCharge <= 0)
            {
                electricCharge = 0;
                return;
            }

            float constRatio = state.planckCharge / state.planckLength;
            float extremalFrac = electricCharge / (schwarzschildRadius * constRatio);
            electricCharge += extremalFrac * deltaR * constRatio;
        }
    }
}