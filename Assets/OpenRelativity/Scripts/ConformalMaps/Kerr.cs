using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Kerr : Schwarzschild
    {
        public float spinMomentum;
        public Vector3 spinAxis = Vector3.up;

        public float GetOmega(Vector3 piw)
        {
            float rSqr = piw.sqrMagnitude;
            // Radius:
            float r = Mathf.Sqrt(rSqr);
            // Inclination:
            float inc = Mathf.Acos(piw.z / r);
            // Azimuth:
            float azi = Mathf.Atan2(piw.y, piw.x);
            // Time: piw.w

            float a = spinMomentum / (radius * state.planckMass / state.planckLength);
            float aSqr = a * a;
            float cosAzi = Mathf.Cos(azi);
            float sigma = rSqr + aSqr * cosAzi * cosAzi;

            float cosInc = Mathf.Cos(inc);
            float omega = (radius * r * a * state.SpeedOfLight) / (sigma * (rSqr + aSqr) + radius * r * aSqr * cosInc * cosInc);

            return omega;
        }

        override public Comotion ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            if ((spinMomentum <= SRelativityUtil.divByZeroCutoff) || (piw.sqrMagnitude <= SRelativityUtil.divByZeroCutoff))
            {
                return base.ComoveOptical(properTDiff, piw, riw);
            }

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
            Quaternion frameDragRot = Quaternion.AngleAxis(frameDragAngle / 2.0f, spinAxis);

            // If interior... (reverse signature change).
            if (!isExterior)
            {
                piw = r * piw / (state.SpeedOfLight * state.TotalTimeWorld);
            }

            // Apply (half) the frame-dragging rotation.
            piw = frameDragRot * piw;
            riw = frameDragRot * riw;

            // Apply (full) Schwarzschild ComoveOptical() step.
            piw = Quaternion.Inverse(rot) * piw;
            Comotion forwardComotion = base.ComoveOptical(properTDiff, piw, riw);
            piw = forwardComotion.piw;
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
            frameDragRot = Quaternion.AngleAxis(frameDragAngle / 2.0f, spinAxis);

            // If interior... (reverse signature change).
            if (!isExterior)
            {
                piw = r * piw / (state.SpeedOfLight * state.TotalTimeWorld);
            }

            piw = frameDragRot * piw;
            riw = frameDragRot * riw;

            // Reverse spin axis rotation.
            piw = Quaternion.Inverse(rot) * piw;

            // Load the return object.
            forwardComotion.piw = piw;
            forwardComotion.riw = riw;

            return forwardComotion;
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            if ((spinMomentum <= SRelativityUtil.divByZeroCutoff) || (piw.sqrMagnitude <= SRelativityUtil.divByZeroCutoff))
            {
                return base.GetRindlerAcceleration(piw);
            }

            Quaternion rot = Quaternion.FromToRotation(spinAxis, Vector3.up);
            Vector3 lpiw = rot * piw;

            float r = lpiw.magnitude;
            if (!isExterior)
            {
                lpiw = state.SpeedOfLight * state.TotalTimeWorld * lpiw / r;
            }

            float omega = GetOmega(lpiw);
            Vector3 frameDragAccel = (omega * omega / lpiw.magnitude) * spinAxis;

            return frameDragAccel + base.GetRindlerAcceleration(piw);
        }

        override public void Update()
        {
            EnforceHorizon();

            if (radius <= 0 || !doEvaporate || state.isMovementFrozen)
            {
                return;
            }

            float deltaR = deltaRadius;

            radius += deltaR;

            if (radius < 0)
            {
                radius = 0;
            }

            if (spinMomentum <= 0)
            {
                spinMomentum = 0;
                return;
            }

            // These happen to be equal:
            // float constRatio = state.planckAngularMomentum / state.planckLength;
            float constRatio = state.planckMomentum;

            float extremalFrac = spinMomentum / (radius * constRatio);

            spinMomentum += extremalFrac * deltaRadius * constRatio;
        }
    }
}