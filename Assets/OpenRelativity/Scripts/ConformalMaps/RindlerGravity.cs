using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class RindlerGravity : ConformalMap
    {
        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            if (Physics.gravity.sqrMagnitude <= SRelativityUtil.FLT_EPSILON)
            {
                return new Comovement
                {
                    piw = new Vector4(piw.x, piw.y, piw.z, properTDiff),
                    riw = riw
                };
            }

            double c = state.SpeedOfLight;
            double cSqr = state.SpeedOfLightSqrd;

            Vector3 origin = state.playerTransform.position;
            double g = Physics.gravity.magnitude;
            Vector3 unitVec = Physics.gravity / (float)g;
            Vector3 projD = Vector3.Project(piw - origin, unitVec);
            double d = projD.magnitude;
            double arg = 1 + g * d / cSqr;
            double properT = (c / g) * Math.Log(arg + Math.Sqrt((arg + 1) * (arg - 1)));
            arg = g * properT / c;
            double expArg = Math.Exp(arg);
            double t = (c / g) * (expArg - 1 / expArg) / 2;

            properT = properTDiff + ((Vector3.Dot(projD.normalized, unitVec) < 0) ? -properT : properT);
            if (properT < 0)
            {
                properT = -properT;
                unitVec = -unitVec;
            }

            arg = g * properT / c;
            expArg = Math.Exp(arg); 
            d = (cSqr / g) * ((expArg + 1 / expArg) / 2 - 1);
            piw = (piw - projD) + (float)d * unitVec;

            arg = g * properT / c;
            expArg = Math.Exp(arg);
            double deltaT = (c / g) * (expArg - 1 / expArg) / 2 - t;

            Vector4 piw4 = piw;
            piw4.w = (float)deltaT;
            return new Comovement
            {
                piw = piw4,
                riw = riw
            };
        }

        override public Vector3 GetRindlerAcceleration(Vector3 piw)
        {
            return Physics.gravity;
        }

        public override Vector3 GetFreeFallVelocity(Vector3 piw)
        {
            if ((Physics.gravity.sqrMagnitude <= SRelativityUtil.FLT_EPSILON) || (piw.sqrMagnitude <= SRelativityUtil.FLT_EPSILON))
            {
                return Vector3.zero;
            }

            Vector3 origin = state.playerTransform.position;
            double g = Physics.gravity.magnitude;
            Vector3 unitVec = Physics.gravity / (float)g;
            Vector3 projD = Vector3.Project(piw - origin, unitVec);
            double d = projD.magnitude;

            if (d <= SRelativityUtil.FLT_EPSILON)
            {
                return Vector3.zero;
            }

            if (Vector3.Dot(projD / (float)d, unitVec) < 0)
            {
                unitVec = -unitVec;
            }

            double t = Math.Sqrt((d * d / state.SpeedOfLightSqrd) + 2 * d / g);

            return (float)(g * t / Math.Sqrt(1 + (g * t) * (g * t) / state.SpeedOfLightSqrd)) * unitVec;
        }
    }
}
