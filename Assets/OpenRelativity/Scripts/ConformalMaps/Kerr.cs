using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Kerr : Schwarzschild
    {
        public float spinMomentum;
        public Vector3 spinAxis = Vector3.up;

        protected float spinRadiusDiff = 0;
        protected float timeScale = 1;

        virtual public float aParam
        {
            get
            {
                return (float)(spinMomentum / (2 * state.gConst * Math.Pow(state.SpeedOfLight, 4) * schwarzschildRadius));
            }
        }

        virtual public void SetTimeCoordScale(Vector3 piw)
        {
            // If our "SetEffectiveRadius(piw)" is expected to be exact at the equator, but we use it in all cases,
            // then we can better our overall approximation by assuming an inclincation-dependent time coordinate scaling.

            if (spinMomentum <= SRelativityUtil.FLT_EPSILON)
            {
                return;
            }

            float a = aParam;
            float aSqr = a * a;
            float rSqr = piw.sqrMagnitude;
            // Radius:
            float r = Mathf.Sqrt(rSqr);
            // Inclination:
            float cosInc = piw.z / r;
            float cosIncSqr = cosInc * cosInc;
            float sinIncSqr = 1 - cosIncSqr;

            float sigma = rSqr + aSqr * cosIncSqr;
            float delta = rSqr - schwarzschildRadius * r + aSqr;

            float effectiveR = (schwarzschildRadius * rSqr) / (rSqr + aSqr * cosIncSqr);

            float kerrScale = Mathf.Sqrt(((aSqr + rSqr) * (aSqr + rSqr) - aSqr * delta * sinIncSqr) / (delta * sigma));
            float schwarzScale = 1 / Mathf.Sqrt(1 - effectiveR / r);

            timeScale = kerrScale / schwarzScale;
        }

        override public void SetEffectiveRadius(Vector3 piw)
        {
            if (spinMomentum <= SRelativityUtil.FLT_EPSILON)
            {
                spinRadiusDiff = 0;
                timeScale = 1;
                return;
            }

            Quaternion rot = Quaternion.FromToRotation(spinAxis, Vector3.up);
            piw = rot * piw;

            SetTimeCoordScale(piw);

            float rs = schwarzschildRadius;
            float rSqr = piw.sqrMagnitude;
            float cosIncSqr = piw.z * piw.z / rSqr;
            float a = aParam;

            // I'm forced to approximate, for now. This might be avoided with tractable free fall coordinates.
            // This is a more accurate approximation, as (rs * r) tends >> (a * a * sinInc * sinInc),
            // such as at the equator or long radial distances.
            spinRadiusDiff = rs - (rs * rSqr) / (rSqr + a * a * cosIncSqr);

            schwarzschildRadius -= spinRadiusDiff;
        }

        override public void ResetSchwarschildRadius()
        {
            schwarzschildRadius += spinRadiusDiff;
            spinRadiusDiff = 0;
            timeScale = 1;
        }

        virtual public float GetOmega(Vector3 piw)
        {
            float rSqr = piw.sqrMagnitude;
            // Radius:
            float r = Mathf.Sqrt(rSqr);
            // Azimuth:
            float cosAzi = Mathf.Cos(Mathf.Atan2(piw.y, piw.x));
            // Inclination:
            float cosInc = Mathf.Cos(Mathf.Acos(piw.z / r));

            float a = (float)(spinMomentum / (schwarzschildRadius * state.planckMass / state.planckLength));
            float aSqr = a * a;

            float sigma = rSqr + aSqr * cosAzi * cosAzi;

            float omega = (schwarzschildRadius * r * a * state.SpeedOfLight) / (sigma * (rSqr + aSqr) + schwarzschildRadius * r * aSqr * cosInc * cosInc);

            return omega;
        }

        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            if ((spinMomentum <= SRelativityUtil.FLT_EPSILON) || (piw.sqrMagnitude <= SRelativityUtil.FLT_EPSILON))
            {
                return base.ComoveOptical(properTDiff, piw, riw);
            }

            SetEffectiveRadius(piw);

            // Adjust the global spin axis
            Quaternion rot = Quaternion.FromToRotation(spinAxis, Vector3.up);
            piw = rot * piw;

            // If interior, flip the metric signature between time-like and radial coordinates.
            float r = piw.magnitude;
            float tDiff = properTDiff;
            if (!isExterior)
            {
                piw = state.SpeedOfLight * state.TotalTimeWorld * piw / r;
                tDiff = -r;
            }

            // Get the angular frame-dragging velocity at the START of the finite difference time step.
            float omega = GetOmega(piw);
            float frameDragAngle = omega * tDiff;
            // We will apply HALF the rotation, at BOTH ends of the finite difference time interval.
            Quaternion frameDragRot = Quaternion.AngleAxis(frameDragAngle / 2, spinAxis);

            // If interior... (reverse signature change).
            if (!isExterior)
            {
                piw = r * piw / (state.SpeedOfLight * state.TotalTimeWorld);
            }

            // Apply (half) the frame-dragging rotation.
            piw = frameDragRot * piw;
            // Think of the observer as absorbing same-sign radiated angular momentum from the black hole.
            riw = frameDragRot * riw;

            // Apply (full) Schwarzschild ComoveOptical() step.
            piw = Quaternion.Inverse(rot) * piw;
            Comovement forwardComovement = base.ComoveOptical(properTDiff, piw, riw);
            float tResult = timeScale * forwardComovement.piw.w;
            piw = forwardComovement.piw;
            piw = rot * piw;

            // If interior, flip the metric signature between time-like and radial coordinates.
            if (!isExterior)
            {
                piw = state.SpeedOfLight * state.TotalTimeWorld * piw / r;
                tDiff = -r;
            }

            // Get the angular frame-dragging velocity at the END of the finite difference time step.
            omega = GetOmega(piw);
            frameDragAngle = omega * tDiff;
            // We will apply HALF the rotation, at BOTH ends of the finite difference time interval.
            frameDragRot = Quaternion.AngleAxis(frameDragAngle / 2, spinAxis);

            // If interior... (reverse signature change).
            if (!isExterior)
            {
                piw = r * piw / (state.SpeedOfLight * state.TotalTimeWorld);
            }

            // Apply (half) the frame-dragging rotation.
            piw = frameDragRot * piw;
            // Think of the observer as absorbing same-sign radiated angular momentum from the black hole.
            riw = frameDragRot * riw;

            // Reverse spin axis rotation.
            piw = Quaternion.Inverse(rot) * piw;

            ResetSchwarschildRadius();

            // Load the return object.
            forwardComovement.piw = new Vector4(piw.x, piw.y, piw.z, tResult);
            forwardComovement.riw = riw;

            return forwardComovement;
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            if ((spinMomentum <= SRelativityUtil.FLT_EPSILON) || (piw.sqrMagnitude <= SRelativityUtil.FLT_EPSILON))
            {
                return base.GetRindlerAcceleration(piw);
            }

            SetEffectiveRadius(piw);

            Quaternion rot = Quaternion.FromToRotation(spinAxis, Vector3.up);
            Vector3 lpiw = rot * piw;
            float r = lpiw.magnitude;
            if (!isExterior)
            {
                // This is an exterior coordinates postion:
                lpiw = state.SpeedOfLight * state.TotalTimeWorld * lpiw / r;
            }
            float omega = GetOmega(lpiw);

            Vector3 frameDragAccel = (omega * omega * lpiw.magnitude) * Vector3.ProjectOnPlane(lpiw, spinAxis).normalized;

            Vector3 totalAccel = 1 / (timeScale * timeScale) * (frameDragAccel + base.GetRindlerAcceleration(piw));

            ResetSchwarschildRadius();

            return totalAccel;
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
                spinMomentum = 0;
                return;
            }

            if (spinMomentum <= 0)
            {
                spinMomentum = 0;
                return;
            }

            // These happen to be equal:
            // float constRatio = state.planckAngularMomentum / state.planckLength;
            float constRatio = (float)state.planckMomentum;
            float extremalFrac = spinMomentum / (schwarzschildRadius * constRatio);
            spinMomentum += extremalFrac * deltaR * constRatio;
        }
    }
}