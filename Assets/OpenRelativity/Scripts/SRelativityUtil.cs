﻿using System;
using UnityEngine;

namespace OpenRelativity
{
    public static class SRelativityUtil
    {
        // It's acceptable if this is this is the difference between 1.0f and the immediate next higher representable value.
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
            return Math.Pow(sigmaPlanck * 8.0 * Math.PI * Math.Pow(rsp, 3.0), -1.0 / 4.0);
        }

        public static double PlanckScaleTempToSchwarzRadius(double temp)
        {
            return state.planckLength / Math.Pow(sigmaPlanck * 8.0 * Math.PI * Math.Pow(temp, 4.0), 1.0 / 3.0);
        }

        public static float EffectiveRaditiativeRadius(float radius, float backgroundPlanckTemp)
        {
            if (backgroundPlanckTemp <= FLT_EPSILON)
            {
                return radius;
            }

            double rsp = radius / state.planckLength;
            return (float)PlanckScaleTempToSchwarzRadius(
                4.0 * Math.PI * rsp * rsp * (
                    Math.Pow(SchwarzRadiusToPlanckScaleTemp(radius), 4.0) -
                    Math.Pow(backgroundPlanckTemp / state.planckTemperature, 4.0)
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
            perp = orig.InverseGamma() * perp / (1.0f + Vector3.Dot(orig, parra) / cSqrd);
            parra = (parra + orig) / (1.0f + Vector3.Dot(orig, parra) / cSqrd);
            return parra + perp;
        }

        public static Vector3 RelativeVelocityTo(this Vector3 myWorldVel, Vector3 otherWorldVel)
        {
            float speedSqr = myWorldVel.sqrMagnitude / cSqrd;
            //Get player velocity dotted with velocity of the object.
            float vuDot = Vector3.Dot(myWorldVel, otherWorldVel) / cSqrd;
            Vector3 vr;
            //If our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speedSqr > FLT_EPSILON)
            {
                //Get the parallel component of the object's velocity
                Vector3 uparra = (vuDot / speedSqr) * myWorldVel / c;
                //Get the perpendicular component of our velocity, just by subtraction
                Vector3 uperp = otherWorldVel / c - uparra;
                //relative velocity calculation
                vr = (myWorldVel / c - uparra - (Mathf.Sqrt(1 - speedSqr)) * uperp) / (1 + vuDot);
            }
            //If our speed is nearly zero, it could lead to infinities.
            else
            {
                //relative velocity calculation
                vr = -otherWorldVel / c;
            }

            return vr * c;
        }

        public static Vector3 ContractLengthBy(this Vector3 interval, Vector3 velocity)
        {
            float speedSqr = velocity.sqrMagnitude;
            if (speedSqr <= FLT_EPSILON)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f - speedSqr / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(velocity / Mathf.Sqrt(speedSqr), Vector3.forward);
            Vector3 rotInt = rot * interval;
            rotInt = new Vector3(rotInt.x, rotInt.y, rotInt.z * invGamma);
            return Quaternion.Inverse(rot) * rotInt;
        }

        public static Vector3 InverseContractLengthBy(this Vector3 interval, Vector3 velocity)
        {
            float speedSqr = velocity.sqrMagnitude;
            if (float.IsNaN(speedSqr) || float.IsInfinity(speedSqr) || speedSqr <= FLT_EPSILON)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f - speedSqr / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(velocity / Mathf.Sqrt(speedSqr), Vector3.forward);
            Vector3 rotInt = rot * interval;
            rotInt = new Vector3(rotInt.x, rotInt.y, rotInt.z / invGamma);
            return Quaternion.Inverse(rot) * rotInt;
        }

        public static Matrix4x4 GetLorentzTransformMatrix(Vector3 vpc)
        {
            float beta = vpc.magnitude;
            float gamma = 1.0f / Mathf.Sqrt(1 - beta * beta);
            Matrix4x4 vpcLorentzMatrix = Matrix4x4.identity;
            if (beta > FLT_EPSILON)
            {
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
            }

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

        public static float GetTisw(this Vector4 stpiw, Vector3 velocity, Vector4 aiw, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            return stpiw.GetTisw(velocity, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector, aiw, null, viwLorentzMatrix, intrinsicMetric);
        }

        public static float GetTisw(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Vector4 aiw, Matrix4x4? vpcLorentzMatrix = null, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            Vector3 vpc = -playerVel / c;
            Vector3 viw = velocity / c;

            //riw = location in world, for reference
            Vector4 riw = stpiw - (Vector4)origin;//Position that will be used in the output

            if (vpcLorentzMatrix == null)
            {
                vpcLorentzMatrix = GetLorentzTransformMatrix(vpc);
            }
            // Boost to rest frame of player
            Vector4 riwForMetric = vpcLorentzMatrix.Value * riw;

            //Find metric based on player acceleration and rest frame:
            Matrix4x4 metric = GetRindlerMetric(riwForMetric, pap, avp);

            //Lorentz boost back to world frame;
            vpcLorentzMatrix = vpcLorentzMatrix.Value.inverse;
            metric = vpcLorentzMatrix.Value.transpose * metric * vpcLorentzMatrix.Value;

            // Apply world coordinates intrinsic curvature:
            if (intrinsicMetric == null)
            {
                if (state.conformalMap)
                {
                    intrinsicMetric = state.conformalMap.GetMetric((Vector3)stpiw);
                }
                else
                {
                    intrinsicMetric = Matrix4x4.identity;
                }
            }
            metric = intrinsicMetric.Value.inverse * metric * intrinsicMetric.Value;

            //We'll also Lorentz transform the vectors:
            if (viwLorentzMatrix == null)
            {
                viwLorentzMatrix = GetLorentzTransformMatrix(viw);
            }

            //Apply Lorentz transform;
            metric = viwLorentzMatrix.Value.transpose * metric * viwLorentzMatrix.Value;
            Vector4 aiwTransformed = viwLorentzMatrix.Value * aiw;
            Vector4 riwTransformed = viwLorentzMatrix.Value * riw;
            //Translate in time:
            float tisw = riwTransformed.w;
            riwForMetric.w = 0;
            riw = vpcLorentzMatrix.Value * riwForMetric;
            riwTransformed = viwLorentzMatrix.Value * riw;
            riwTransformed.w = 0;

            //(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
            float riwDotRiw = -Vector4.Dot(riwTransformed, metric * riwTransformed);
            float aiwDotAiw = -Vector4.Dot(aiwTransformed, metric * aiwTransformed);
            float riwDotAiw = -Vector4.Dot(riwTransformed, metric * aiwTransformed);

            float sqrtArg = riwDotRiw * (cSqrd - riwDotAiw + aiwDotAiw * riwDotRiw / (4 * cSqrd)) / ((cSqrd - riwDotAiw) * (cSqrd - riwDotAiw));
            float aiwMag = aiwTransformed.magnitude;
            float t2 = 0;
            if (sqrtArg > 0)
            {
                t2 = -Mathf.Sqrt(sqrtArg);
            }

            tisw += t2;
            //add the position offset due to acceleration
            if (aiwMag > FLT_EPSILON)
            {
                riwTransformed = riwTransformed - aiwTransformed / aiwMag * cSqrd * (Mathf.Sqrt(1 + (aiwMag * t2 / c) * (aiwMag * t2 / c)) - 1);
            }
            riwTransformed.w = tisw;
            //Inverse Lorentz transform the position:
            viwLorentzMatrix = viwLorentzMatrix.Value.inverse;
            riw = viwLorentzMatrix.Value * riwTransformed;
            tisw = riw.w;

            return tisw;
        }

        public static Vector3 WorldToOptical(this Vector4 stpiw, Vector3 velocity, Vector4 aiw, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            return stpiw.WorldToOptical(velocity, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector, aiw, state.PlayerLorentzMatrix, viwLorentzMatrix, intrinsicMetric);
        }

        public static Vector3 WorldToOptical(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Vector4 aiw, Matrix4x4? vpcLorentzMatrix = null, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            Vector3 vpc = -playerVel / c;
            Vector3 viw = velocity / c;

            //riw = location in world, for reference
            Vector4 riw = stpiw - (Vector4)origin;//Position that will be used in the output

            if (vpcLorentzMatrix == null)
            {
                vpcLorentzMatrix = GetLorentzTransformMatrix(vpc);
            }
            // Boost to rest frame of player
            Vector4 riwForMetric = vpcLorentzMatrix.Value * riw;

            // Find metric based on player acceleration and rest frame:
            Matrix4x4 metric = GetRindlerMetric(riwForMetric, pap, avp);

            // Lorentz boost back to world frame:
            vpcLorentzMatrix = vpcLorentzMatrix.Value.inverse;
            metric = vpcLorentzMatrix.Value.transpose * metric * vpcLorentzMatrix.Value;

            // Apply world coordinates intrinsic curvature:
            if (intrinsicMetric == null)
            {
                if (state.conformalMap) {
                    intrinsicMetric = state.conformalMap.GetMetric((Vector3)stpiw);
                } else
                {
                    intrinsicMetric = Matrix4x4.identity;
                }
            }
            metric = intrinsicMetric.Value.inverse * metric * intrinsicMetric.Value;

            // We'll also Lorentz transform the vectors:
            if (viwLorentzMatrix == null)
            {
                viwLorentzMatrix = GetLorentzTransformMatrix(viw);
            }

            //Apply Lorentz transform;
            metric = viwLorentzMatrix.Value.transpose * metric * viwLorentzMatrix.Value;
            Vector4 aiwTransformed = viwLorentzMatrix.Value * aiw;
            Vector4 riwTransformed = viwLorentzMatrix.Value * riw;
            //Translate in time:
            float tisw = riwTransformed.w;
            riwForMetric.w = 0;
            riw = vpcLorentzMatrix.Value * riwForMetric;
            riwTransformed = viwLorentzMatrix.Value * riw;
            riwTransformed.w = 0;

            //(When we "dot" four-vectors, always do it with the metric at that point in space-time, like we do so here.)
            float riwDotRiw = -Vector4.Dot(riwTransformed, metric * riwTransformed);
            float aiwDotAiw = -Vector4.Dot(aiwTransformed, metric * aiwTransformed);
            float riwDotAiw = -Vector4.Dot(riwTransformed, metric * aiwTransformed);

            float sqrtArg = riwDotRiw * (cSqrd - riwDotAiw + aiwDotAiw * riwDotRiw / (4 * cSqrd)) / ((cSqrd - riwDotAiw) * (cSqrd - riwDotAiw));
            float aiwMag = aiwTransformed.magnitude;
            float t2 = 0;
            if (sqrtArg > 0)
            {
                t2 = -Mathf.Sqrt(sqrtArg);
            }

            tisw += t2;
            //add the position offset due to acceleration
            if (aiwMag > FLT_EPSILON)
            {
                riwTransformed = riwTransformed - aiwTransformed / aiwMag * cSqrd * (Mathf.Sqrt(1 + (aiwMag * t2 / c) * (aiwMag * t2 / c)) - 1);
            }
            riwTransformed.w = tisw;
            //Inverse Lorentz transform the position:
            viwLorentzMatrix = viwLorentzMatrix.Value.inverse;
            riw = viwLorentzMatrix.Value * riwTransformed;
            tisw = riw.w;
            riw = (Vector3)riw + tisw * velocity;

            float speed = vpc.magnitude;
            if (speed > FLT_EPSILON)
            {
                float newz = speed * c * tisw;
                Vector4 vpcUnit = vpc / speed;
                newz = (Vector4.Dot(riw, vpcUnit) + newz) / Mathf.Sqrt(1 - (speed * speed));
                riw = riw + (newz - Vector4.Dot(riw, vpcUnit)) * vpcUnit;
            }

            riw = (Vector3)riw + origin;

            return riw;
        }

        const int defaultOpticalToWorldMaxIterations = 5;
        const float defaultOpticalToWorldSqrErrorTolerance = 0.0001f;

        public static Vector4 OpticalToWorld(this Vector4 opticalPos, Vector3 velocity, Vector4 aiw, Matrix4x4? vpcLorentzMatrix = null, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            return opticalPos.OpticalToWorld(velocity, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector, aiw, vpcLorentzMatrix, viwLorentzMatrix, intrinsicMetric);
        }

        public static Vector4 OpticalToWorld(this Vector4 opticalPos, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector3 pap, Vector3 avp, Vector4 aiw, Matrix4x4? vpcLorentzMatrix = null, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            Vector3 vpc = -playerVel / c;
            Vector3 viw = velocity / c;

            //riw = location in world, for reference
            Vector4 riw = opticalPos - (Vector4)origin; //Position that will be used in the output
            Vector4 pos = (Vector3)riw;

            float tisw = -pos.magnitude / c;

            //Transform fails and is unecessary if relative speed is zero:
            float speed = vpc.magnitude;
            if (speed > FLT_EPSILON)
            {
                Vector4 vpcUnit = vpc / speed;
                float riwDotVpcUnit = Vector4.Dot(riw, vpcUnit);
                float newz = (riwDotVpcUnit + speed * c * tisw) / Mathf.Sqrt(1 - (speed * speed));
                riw -= (newz - riwDotVpcUnit) * vpcUnit;
            }

            //Rotate all our vectors so that velocity is entirely along z direction:
            Quaternion viwToZRot = viw.sqrMagnitude <= FLT_EPSILON ? Quaternion.identity : Quaternion.FromToRotation(viw, Vector3.forward);
            Vector4 riwTransformed = viwToZRot * ((Vector3)riw - velocity * tisw);
            riwTransformed.w = tisw;
            Vector3 aiwTransformed = viwToZRot * aiw;

            //We'll also Lorentz transform the vectors:
            if (viwLorentzMatrix == null)
            {
                viwLorentzMatrix = GetLorentzTransformMatrix(viw);
            }

            //Apply Lorentz transform;
            riwTransformed = viwLorentzMatrix.Value * riwTransformed;
            aiwTransformed = viwLorentzMatrix.Value * aiwTransformed;

            float t2 = riwTransformed.w;

            float aiwMag = aiwTransformed.magnitude;
            if (aiwMag > FLT_EPSILON)
            {
                //add the position offset due to acceleration
                riwTransformed += (Vector4)(aiwTransformed) / aiwMag * c * c * (Mathf.Sqrt(1 + (aiwMag * t2 / c) * (aiwMag * t2 / c)) - 1);
            }

            //Inverse Lorentz transform the position:
            riwTransformed = viwLorentzMatrix.Value.inverse * riwTransformed;
            riw = Quaternion.Inverse(viwToZRot) * riwTransformed;

            riw = (Vector3)riw + origin;
            riw.w = riwTransformed.w;

            return riw;
        }

        public static Vector3 OpticalToWorldHighPrecision(this Vector4 opticalPos, Vector3 velocity, Vector4 aiw, Matrix4x4? vpcLorentzMatrix = null, Matrix4x4? viwLorentzMatrix = null, Matrix4x4? intrinsicMetric = null)
        {
            return opticalPos.OpticalToWorldHighPrecision(velocity, state.playerTransform.position, state.PlayerVelocityVector, state.PlayerAccelerationVector, state.PlayerAngularVelocityVector, aiw, vpcLorentzMatrix, viwLorentzMatrix, intrinsicMetric);
        }

        public static Vector3 OpticalToWorldHighPrecision(this Vector4 opticalPos, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector4 pap, Vector3 avp, Vector4 aiw, Matrix4x4? vpcLorentz = null, Matrix4x4? viwLorentz = null, Matrix4x4? intrinsicMetric = null)
        {
            Vector4 startPoint = opticalPos;
            Vector3 est, offset, newEst;
            est = opticalPos.OpticalToWorld(velocity, origin, playerVel, pap, avp, aiw, vpcLorentz, viwLorentz, intrinsicMetric);
            offset = ((Vector4)est).WorldToOptical(velocity, origin, playerVel, pap, avp, aiw, vpcLorentz, viwLorentz, intrinsicMetric)- (Vector3)opticalPos;

            float sqrError = offset.sqrMagnitude;
            float oldSqrError = sqrError + 1.0f;
            float iterations = 1;
            while ((iterations < defaultOpticalToWorldMaxIterations)
                && (sqrError > defaultOpticalToWorldSqrErrorTolerance)
                && (sqrError < oldSqrError))
            {
                iterations++;
                startPoint += (Vector4)offset / 2.0f;
                newEst = startPoint.OpticalToWorld(velocity, origin, playerVel, pap, avp, aiw);
                offset = ((Vector4)newEst).WorldToOptical(velocity, origin, playerVel, pap, avp, aiw, vpcLorentz, viwLorentz, intrinsicMetric)- (Vector3)startPoint;
                oldSqrError = sqrError;
                sqrError = offset.sqrMagnitude;
                if (sqrError < oldSqrError)
                {
                    est = newEst;
                }
            }

            return est;
        }

        public static float Gamma(this Vector3 velocity, Matrix4x4? metric = null)
        {
            if (metric == null)
            {
                return 1.0f / Mathf.Sqrt(1.0f - velocity.sqrMagnitude / cSqrd);
            }
            
            return 1.0f / Mathf.Sqrt(1.0f - (Vector4.Dot(velocity, metric.Value * velocity) / state.SpeedOfLightSqrd));
        }

        public static float InverseGamma(this Vector3 velocity, Matrix4x4? metric = null)
        {
            if (metric == null)
            {
                return 1.0f / Mathf.Sqrt(1.0f + velocity.sqrMagnitude / cSqrd);
            }

            return 1.0f / Mathf.Sqrt(1.0f + (Vector4.Dot(velocity, metric.Value * velocity) / state.SpeedOfLightSqrd));
        }

        public static Vector3 RapidityToVelocity(this Vector3 rapidity, Matrix4x4? metric = null)
        {
            Vector3 flat3V = c * rapidity / Mathf.Sqrt(cSqrd + rapidity.sqrMagnitude);

            if (metric == null)
            {
                return flat3V;
            }

            return Mathf.Sqrt(-Vector4.Dot(flat3V, metric.Value.inverse * flat3V)) * rapidity.normalized;
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

            float sum = 0.0f;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 corner = vertices[triangles[i]];
                Vector3 a = vertices[triangles[i + 1]] - corner;
                Vector3 b = vertices[triangles[i + 2]] - corner;

                sum += Vector3.Cross(a, b).magnitude;
            }

            return sum / 2.0f;
        }

        // Strano 2019 monopole methods
        public static double SchwarzschildRadiusDecay(double deltaTime, double r)
        {
            double origR = r;

            if (r < state.planckLength)
            {
                r = state.planckLength;
            }

            double deltaR = -deltaTime * Math.Sqrt(state.hbarOverG * Math.Pow(c, 7.0f)) * 2.0 / r;

            if ((origR + deltaR) < 0)
            {
                deltaR = -origR;
            }

            return deltaR;
        }
    }
}