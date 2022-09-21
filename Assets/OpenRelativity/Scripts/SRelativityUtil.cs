using System;
using UnityEngine;

namespace OpenRelativity
{
    public static class SRelativityUtil
    {
        // It's acceptable if this is the difference between 1.0f and the immediate next higher representable value.
        // See https://stackoverflow.com/questions/12627534/is-there-a-flt-epsilon-defined-in-the-net-framework
        public const float FLT_EPSILON = 1.192092896e-07F;

        public static float c { get { return state.SpeedOfLight; } }
        public static float cSqrd { get { return state.SpeedOfLightSqrd; } }

        public static float sigmaPlanck { get { return 8 * Mathf.Pow(Mathf.PI, 2) / 120; } }

        public static float avogadroNumber = 6.02214e23f;

        private static GameState state
        {
            get
            {
                return GameState.Instance;
            }
        }

        public static double SchwarzRadiusToPlanckScaleTemp(double radius)
        {
            double rsp = radius / state.planckLength;
            return Math.Pow(sigmaPlanck * 8 * Math.PI * Math.Pow(rsp, 3), -1.0 / 4);
        }

        public static double PlanckScaleTempToSchwarzRadius(double temp)
        {
            return state.planckLength / Math.Pow(sigmaPlanck * 8 * Math.PI * Math.Pow(temp, 4), 1.0 / 3);
        }

        public static float EffectiveRaditiativeRadius(float radius, float backgroundPlanckTemp)
        {
            if (backgroundPlanckTemp <= FLT_EPSILON)
            {
                return radius;
            }

            double rsp = radius / state.planckLength;
            return (float)PlanckScaleTempToSchwarzRadius(
                4 * Math.PI * rsp * rsp * (
                    Math.Pow(SchwarzRadiusToPlanckScaleTemp(radius), 4) -
                    Math.Pow(backgroundPlanckTemp / state.planckTemperature, 4)
                )
            );
        }

        public static Vector3 AddVelocity(this Vector3 orig, Vector3 toAdd)
        {
            if (orig == Vector3.zero)
            {
                return toAdd;
            }

            if (toAdd == Vector3.zero)
            {
                return orig;
            }

            Vector3 parra = Vector3.Project(toAdd, orig);
            Vector3 perp = toAdd - parra;
            perp = orig.InverseGamma() * perp / (1 + Vector3.Dot(orig, parra) / cSqrd);
            parra = (parra + orig) / (1 + Vector3.Dot(orig, parra) / cSqrd);
            return parra + perp;
        }

        public static Vector3 ContractLengthBy(this Vector3 interval, Vector3 velocity)
        {
            float speedSqr = velocity.sqrMagnitude;
            if (speedSqr <= FLT_EPSILON)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1 - speedSqr / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(velocity / Mathf.Sqrt(speedSqr), Vector3.forward);
            Vector3 rotInt = rot * interval;
            rotInt = new Vector3(rotInt.x, rotInt.y, rotInt.z * invGamma);
            return Quaternion.Inverse(rot) * rotInt;
        }

        public static Matrix4x4 GetLorentzTransformMatrix(Vector3 vpc)
        {
            float beta = vpc.magnitude;
            if (beta <= FLT_EPSILON)
            {
                return Matrix4x4.identity;
            }

            float gamma = 1 / Mathf.Sqrt(1 - beta * beta);
            Matrix4x4 vpcLorentzMatrix = Matrix4x4.identity;
            Vector4 vpcTransUnit = -vpc / beta;
            vpcTransUnit.w = 1;
            Vector4 spatialComp = (gamma - 1) * vpcTransUnit;
            spatialComp.w = -gamma * beta;
            Vector4 tComp = -gamma * (new Vector4(beta, beta, beta, -1));
            tComp.Scale(vpcTransUnit);
            vpcLorentzMatrix.SetColumn(3, tComp);
            vpcLorentzMatrix.SetColumn(0, vpcTransUnit.x * spatialComp);
            vpcLorentzMatrix.SetColumn(1, vpcTransUnit.y * spatialComp);
            vpcLorentzMatrix.SetColumn(2, vpcTransUnit.z * spatialComp);
            vpcLorentzMatrix.m00 += 1;
            vpcLorentzMatrix.m11 += 1;
            vpcLorentzMatrix.m22 += 1;

            return vpcLorentzMatrix;
        }

        public static Matrix4x4 GetRindlerMetric(Vector4 piw)
        {
            return GetRindlerMetric(piw, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector);
        }

        public static Matrix4x4 GetRindlerMetric(Vector4 piw, Vector4 pap, Vector3 avp)
        {
            //Find metric based on player acceleration and rest frame:
            float linFac = 1 + Vector3.Dot(pap, piw) / cSqrd;
            linFac *= linFac;
            float angFac = Vector3.Dot(avp, piw) / c;
            angFac *= angFac;
            float avpMagSqr = avp.sqrMagnitude;
            Vector3 angVec = avpMagSqr <= FLT_EPSILON ? Vector3.zero : 2 * angFac / (c * avpMagSqr) * avp.normalized;

            Matrix4x4 metric = new Matrix4x4(
                new Vector4(-1, 0, 0, -angVec.x),
                new Vector4(0, -1, 0, -angVec.y),
                new Vector4(0, 0, -1, -angVec.z),
                new Vector4(-angVec.x, -angVec.y, -angVec.z, (linFac * (1 - angFac) - angFac))
            );

            return metric;
        }

        public static float GetTisw(this Vector3 stpiw, Vector3 velocity, Vector4 aiw)
        {
            return stpiw.GetTisw(velocity, aiw, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector);
        }
        public static float GetTisw(this Vector3 stpiw, Vector3 velocity, Vector4 aiw, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp)
        {
            return stpiw.GetTisw(velocity, aiw, origin, playerVel, pap, avp, GetLorentzTransformMatrix(-playerVel / c), GetLorentzTransformMatrix(velocity / c), state.conformalMap.GetMetric(stpiw));
        }
        public static float GetTisw(this Vector3 stpiw, Vector3 velocity, Vector4 aiw, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Matrix4x4 vpcLorentzMatrix, Matrix4x4 viwLorentzMatrix, Matrix4x4 intrinsicMetric)
        {
            Vector3 vpc = -playerVel / c;
            Vector3 viw = velocity / c;

            //riw = location in world, for reference
            Vector4 riw = (Vector4)(stpiw - origin);//Position that will be used in the output

            // Boost to rest frame of player
            Vector4 riwForMetric = vpcLorentzMatrix * riw;

            //Find metric based on player acceleration and rest frame:
            Matrix4x4 metric = GetRindlerMetric(riwForMetric, pap, avp);

            //Lorentz boost back to world frame;
            vpcLorentzMatrix = vpcLorentzMatrix.inverse;
            metric = vpcLorentzMatrix.transpose * metric * vpcLorentzMatrix;

            // Apply world coordinates intrinsic curvature:
            metric = intrinsicMetric.inverse * metric * intrinsicMetric;

            //Apply Lorentz transform;
            metric = viwLorentzMatrix.transpose * metric * viwLorentzMatrix;
            Vector4 aiwTransformed = viwLorentzMatrix * aiw;
            Vector4 riwTransformed = viwLorentzMatrix * riw;
            //Translate in time:
            float tisw = riwTransformed.w;
            riwForMetric.w = 0;
            riw = vpcLorentzMatrix * riwForMetric;
            riwTransformed = viwLorentzMatrix * riw;
            riwTransformed.w = 0;

            //(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
            float riwDotRiw = -Vector4.Dot(riwTransformed, metric * riwTransformed);
            float aiwDotAiw = -Vector4.Dot(aiwTransformed, metric * aiwTransformed);
            float riwDotAiw = -Vector4.Dot(riwTransformed, metric * aiwTransformed);

            float sqrtArg = riwDotRiw * (cSqrd - riwDotAiw + aiwDotAiw * riwDotRiw / (4 * cSqrd)) / ((cSqrd - riwDotAiw) * (cSqrd - riwDotAiw));
            float aiwMagSqr = aiwTransformed.sqrMagnitude;
            float aiwMag = Mathf.Sqrt(aiwMagSqr);
            tisw += (sqrtArg > 0) ? -Mathf.Sqrt(sqrtArg) : 0;
            //add the position offset due to acceleration
            if (aiwMag > FLT_EPSILON)
            {
                riwTransformed = riwTransformed - aiwTransformed * cSqrd * (Mathf.Sqrt(1 + sqrtArg * aiwMagSqr / cSqrd) - 1) / aiwMag;
            }
            riwTransformed.w = (float)tisw;
            //Inverse Lorentz transform the position:
            viwLorentzMatrix = viwLorentzMatrix.inverse;
            riw = viwLorentzMatrix * riwTransformed;

            return riw.w;
        }

        public static Vector3 WorldToOptical(this Vector3 stpiw, Vector3 velocity, Vector4 aiw)
        {
            return stpiw.WorldToOptical(velocity, aiw, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector);
        }
        public static Vector3 WorldToOptical(this Vector3 stpiw, Vector3 velocity, Vector4 aiw, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp)
        {
            return stpiw.WorldToOptical(velocity, aiw, origin, playerVel, pap, avp, GetLorentzTransformMatrix(-playerVel / c), GetLorentzTransformMatrix(velocity / c), state.conformalMap.GetMetric(stpiw));
        }
        public static Vector3 WorldToOptical(this Vector3 stpiw, Vector3 velocity, Vector4 aiw, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Matrix4x4 vpcLorentzMatrix, Matrix4x4 viwLorentzMatrix, Matrix4x4 intrinsicMetric)
        {
            Vector3 vpc = -playerVel / c;
            Vector3 viw = velocity / c;

            //riw = location in world, for reference
            Vector4 riw = (Vector4)(stpiw - origin);//Position that will be used in the output

            // Boost to rest frame of player
            Vector4 riwForMetric = vpcLorentzMatrix * riw;

            // Find metric based on player acceleration and rest frame:
            Matrix4x4 metric = GetRindlerMetric(riwForMetric, pap, avp);

            // Lorentz boost back to world frame:
            vpcLorentzMatrix = vpcLorentzMatrix.inverse;
            metric = vpcLorentzMatrix.transpose * metric * vpcLorentzMatrix;

            // Apply world coordinates intrinsic curvature:
            metric = intrinsicMetric.inverse * metric * intrinsicMetric;

            //Apply Lorentz transform;
            metric = viwLorentzMatrix.transpose * metric * viwLorentzMatrix;
            Vector4 aiwTransformed = viwLorentzMatrix * aiw;
            Vector4 riwTransformed = viwLorentzMatrix * riw;
            //Translate in time:
            float tisw = riwTransformed.w;
            riwForMetric.w = 0;
            riw = vpcLorentzMatrix * riwForMetric;
            riwTransformed = viwLorentzMatrix * riw;
            riwTransformed.w = 0;

            //(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
            float riwDotRiw = -Vector4.Dot(riwTransformed, metric * riwTransformed);
            float aiwDotAiw = -Vector4.Dot(aiwTransformed, metric * aiwTransformed);
            float riwDotAiw = -Vector4.Dot(riwTransformed, metric * aiwTransformed);

            float sqrtArg = riwDotRiw * (cSqrd - riwDotAiw + aiwDotAiw * riwDotRiw / (4 * cSqrd)) / ((cSqrd - riwDotAiw) * (cSqrd - riwDotAiw));
            float aiwMagSqr = aiwTransformed.sqrMagnitude;
            float aiwMag = Mathf.Sqrt(aiwMagSqr);
            tisw += (sqrtArg > 0) ? -Mathf.Sqrt(sqrtArg) : 0;
            //add the position offset due to acceleration
            if (aiwMag > FLT_EPSILON)
            {
                riwTransformed = riwTransformed - aiwTransformed * cSqrd * (Mathf.Sqrt(1 + sqrtArg * aiwMagSqr / cSqrd) - 1) / aiwMag;
            }
            riwTransformed.w = (float)tisw;
            //Inverse Lorentz transform the position:
            viwLorentzMatrix = viwLorentzMatrix.inverse;
            riw = viwLorentzMatrix * riwTransformed;
            tisw = riw.w;
            riw = (Vector3)riw + (float)tisw * velocity;

            float speed = vpc.magnitude;
            if (speed > FLT_EPSILON)
            {
                float newz = speed * c * (float)tisw;
                Vector4 vpcUnit = vpc / speed;
                newz = (Vector4.Dot(riw, vpcUnit) + newz) / Mathf.Sqrt(1 - vpc.sqrMagnitude);
                riw = riw + (newz - Vector4.Dot(riw, vpcUnit)) * vpcUnit;
            }

            riw = (Vector3)riw + origin;

            return riw;
        }

        public static Vector3 OpticalToWorld(this Vector3 stpiw, Vector3 velocity, Vector4 aiw)
        {
            return stpiw.OpticalToWorld(velocity, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector, aiw);
        }
        public static Vector3 OpticalToWorld(this Vector3 stpiw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Vector4 aiw)
        {
            return stpiw.OpticalToWorld(velocity, origin, playerVel, pap, avp, aiw, GetLorentzTransformMatrix(velocity / c));
        }
        public static Vector3 OpticalToWorld(this Vector3 opticalPos, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Vector4 aiw, Matrix4x4 viwLorentzMatrix)
        {
            Vector3 vpc = -playerVel / c;
            Vector3 viw = velocity / c;

            //riw = location in world, for reference
            Vector4 riw = (Vector4)(opticalPos - origin); //Position that will be used in the output
            Vector4 pos = (Vector3)riw;

            float tisw = -pos.magnitude / c;

            //Transform fails and is unecessary if relative speed is zero:
            float speed = vpc.magnitude;
            if (speed > FLT_EPSILON)
            {
                Vector4 vpcUnit = vpc / speed;
                float riwDotVpcUnit = Vector4.Dot(riw, vpcUnit);
                float newz = (riwDotVpcUnit + speed * c * tisw) / Mathf.Sqrt(1 - vpc.sqrMagnitude);
                riw -= (newz - riwDotVpcUnit) * vpcUnit;
            }

            //Rotate all our vectors so that velocity is entirely along z direction:
            Quaternion viwToZRot = viw.sqrMagnitude <= FLT_EPSILON ? Quaternion.identity : Quaternion.FromToRotation(viw, Vector3.forward);
            Vector4 riwTransformed = viwToZRot * ((Vector3)riw - velocity * tisw);
            riwTransformed.w = tisw;
            Vector3 aiwTransformed = viwToZRot * aiw;

            //Apply Lorentz transform;
            riwTransformed = viwLorentzMatrix * riwTransformed;
            aiwTransformed = viwLorentzMatrix * aiwTransformed;

            float t2 = riwTransformed.w;
            float aiwMagSqr = aiwTransformed.sqrMagnitude;
            float aiwMag = Mathf.Sqrt(aiwMagSqr);
            if (aiwMag > FLT_EPSILON)
            {
                //add the position offset due to acceleration
                riwTransformed += (Vector4)aiwTransformed * cSqrd * (Mathf.Sqrt(1 + t2 * t2 * aiwMagSqr / cSqrd) - 1) / aiwMag;
            }

            //Inverse Lorentz transform the position:
            riwTransformed = viwLorentzMatrix.inverse * riwTransformed;
            riw = Quaternion.Inverse(viwToZRot) * riwTransformed;

            riw = (Vector3)riw + origin;
            riw.w = riwTransformed.w;

            return riw;
        }

        public static float Gamma(this Vector3 velocity)
        {
            return 1 / Mathf.Sqrt(1 - velocity.sqrMagnitude / cSqrd);
        }

        public static float Gamma(this Vector3 velocity, Matrix4x4 metric)
        {
            return 1 / Mathf.Sqrt(1 - (Vector4.Dot(velocity, metric * velocity) / cSqrd));
        }

        public static float InverseGamma(this Vector3 velocity)
        {
            return 1 / Mathf.Sqrt(1 + velocity.sqrMagnitude / cSqrd);
        }

        public static float InverseGamma(this Vector3 velocity, Matrix4x4 metric)
        {
            return 1 / Mathf.Sqrt(1 + (Vector4.Dot(velocity, metric * velocity) / cSqrd));
        }

        public static Vector3 RapidityToVelocity(this Vector3 rapidity)
        {
            return c * rapidity / Mathf.Sqrt(cSqrd + rapidity.sqrMagnitude);
        }

        public static Vector3 RapidityToVelocity(this Vector3 rapidity, Matrix4x4 metric)
        {
            Vector3 flat3V = c * rapidity / Mathf.Sqrt(cSqrd + rapidity.sqrMagnitude);

            return Mathf.Sqrt(-Vector4.Dot(flat3V, metric.inverse * flat3V)) * rapidity.normalized;
        }

        public static Vector4 ToMinkowski4Viw(this Vector3 viw)
        {
            if (c <= FLT_EPSILON)
            {
                return Vector4.zero;
            }

            return new Vector4(viw.x, viw.y, viw.z, c) * viw.Gamma();
        }

        public static Vector4 ProperToWorldAccel(this Vector3 propAccel, Vector3 viw, float gamma)
        {
            float gammaSqrd = gamma * gamma;
            float gammaFourthADotVDivCSqrd = Vector3.Dot(propAccel, viw) * gammaSqrd * gammaSqrd / cSqrd;
            Vector4 fourAccel = gammaSqrd * propAccel + gammaFourthADotVDivCSqrd * viw;
            fourAccel.w = gammaFourthADotVDivCSqrd * c;
            return fourAccel;
        }

        // From https://gamedev.stackexchange.com/questions/165643/how-to-calculate-the-surface-area-of-a-mesh
        public static float SurfaceArea(this Mesh mesh)
        {
            var triangles = mesh.triangles;
            var vertices = mesh.vertices;

            float sum = 0;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 corner = vertices[triangles[i]];
                Vector3 a = vertices[triangles[i + 1]] - corner;
                Vector3 b = vertices[triangles[i + 2]] - corner;

                sum += Vector3.Cross(a, b).magnitude;
            }

            return sum / 2;
        }

        // Strano 2019 monopole methods
        public static double SchwarzschildRadiusDecay(double deltaTime, double r)
        {
            double origR = r;

            if (r < state.planckLength)
            {
                r = state.planckLength;
            }

            double deltaR = -deltaTime * Math.Sqrt(state.hbarOverG * Math.Pow(c, 7)) * 2 / r;

            if ((origR + deltaR) < 0)
            {
                deltaR = -origR;
            }

            return deltaR;
        }
    }
}