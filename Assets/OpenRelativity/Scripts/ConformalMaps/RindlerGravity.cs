using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class RindlerGravity : ConformalMap
    {
        public float horizonUpOffset = 0.0f;
        override public Comovement ComoveOptical(float properTDiff, Vector3 piw, Quaternion riw)
        {
            if (Physics.gravity.sqrMagnitude <= SRelativityUtil.divByZeroCutoff)
            {
                return new Comovement
                {
                    piw = new Vector4(piw.x, piw.y, piw.z, properTDiff),
                    riw = riw
                };
            }

            float c = state.SpeedOfLight;
            float cSqr = state.SpeedOfLightSqrd;

            float g = Physics.gravity.magnitude;
            Vector3 unitVec = Physics.gravity / g;
            Vector3 projD = Vector3.Project(piw, unitVec);
            float d = projD.magnitude - horizonUpOffset;
            float arg = 1.0f + g * d / cSqr;
            float properT = (c / g) * Mathf.Log(arg + Mathf.Sqrt((arg + 1.0f) * (arg - 1.0f)));
            arg = g * properT / c;
            float expArg = Mathf.Exp(arg);
            float t = (c / g) * (expArg - 1.0f / expArg) / 2.0f;

            properT = properTDiff + ((Vector3.Dot(projD.normalized, unitVec) < 0) ? -properT : properT);
            if (properT < 0)
            {
                properT = -properT;
                unitVec = -unitVec;
            }

            arg = g * properT / c;
            expArg = Mathf.Exp(arg); 
            d = (cSqr / g) * ((expArg + 1.0f / expArg) / 2.0f - 1.0f) + horizonUpOffset;
            piw = d * unitVec + piw - projD;

            arg = g * properT / c;
            expArg = Mathf.Exp(arg);
            float deltaT = (c / g) * (expArg - 1.0f / expArg) / 2.0f - t;

            Vector4 piw4 = piw;
            piw4.w = deltaT;
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
            if ((Physics.gravity.sqrMagnitude <= SRelativityUtil.divByZeroCutoff) || (piw.sqrMagnitude <= SRelativityUtil.divByZeroCutoff))
            {
                return Vector3.zero;
            }

            float g = Physics.gravity.magnitude;
            Vector3 unitVec = Physics.gravity / g;
            Vector3 projD = Vector3.Project(piw, unitVec);
            if (Vector3.Dot(projD.normalized, unitVec) < 0)
            {
                unitVec = -unitVec;
            }
            float d = projD.magnitude - horizonUpOffset;
            float t = Mathf.Sqrt((d * d / state.SpeedOfLightSqrd) + 2.0f * d / g);

            return g * t / Mathf.Sqrt(1.0f + (g * t) * (g * t) / state.SpeedOfLightSqrd) * unitVec;
        }
    }
}
