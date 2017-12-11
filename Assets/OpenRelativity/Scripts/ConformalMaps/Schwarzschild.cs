using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public float radius = 1;
        public float radiusCutoff = 1;

        override public Matrix4x4 GetConformalFactor(Vector4 stpiw, Vector4 pstpiw)
        {
            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            //So, let's just convert to spherical to get the conformal factor:
            Vector4 cartesianPos = stpiw - origin;
            Vector4 sphericalPos = cartesianPos.Cartesian4ToSpherical4();
            //Assume that spherical transform of input are Lemaître coordinates, since they "co-move" with the gravitational pull of the black hole:
            double rho = cartesianPos.magnitude;
            double tau = cartesianPos.w;

            //Convert to usual Schwarzschild solution r:
            double r = Math.Pow(Math.Pow(3.0 / 2.0 * (rho - SRelativityUtil.c * tau), 2) * radius, 1.0 / 3.0);

            //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
            // so we need to cut-off the interior metric at some point, for numerical sanity
            if (r <= radiusCutoff)
            {
                return Matrix4x4.identity;
            }
            else
            {
                double schwarzFac = 1 - radius / r;
                double playerR = ((Vector3)(pstpiw - origin)).magnitude;
                double playerSchwarzFac = 1;
                if (playerR > radiusCutoff)
                {
                    playerSchwarzFac = 1 - radius / playerR;
                }
                double sqrtRDivRs = Math.Sqrt(r / radius);
                double denomFac = (sqrtRDivRs - 1.0 / sqrtRDivRs) * (sqrtRDivRs - 1.0 / sqrtRDivRs);

                //Here's the value of the conformal factor at this distance in spherical coordinates with the orig at zero:
                Matrix4x4 sphericalConformalFactor = Matrix4x4.zero;
                //(For the metric, rather than the conformal factor, the time coordinate would have its sign flipped relative to the spatial components,
                // either positive space and negative time, or negative time and positive space.)
                sphericalConformalFactor[3, 3] = (float)(playerSchwarzFac * (r - radius) / (radius * denomFac));
                sphericalConformalFactor[0, 0] = (float)(schwarzFac / (denomFac * playerSchwarzFac));
                sphericalConformalFactor[1, 1] = (float)(r * r);
                sphericalConformalFactor[2, 2] = (float)(r * r * Mathf.Pow(Mathf.Sin(sphericalPos.y), 2));

                //A particular useful "tensor" (which we can think of loosely here as "just a matrix") called the "Jacobian"
                // lets us convert the "metric tensor" (and other tensors) between coordinate systems, like from spherical back to Cartesian:
                Matrix4x4 jacobian  = Matrix4x4.identity;
                double x = cartesianPos.x;
                double y = cartesianPos.y;
                double z = cartesianPos.z;
                rho = Math.Sqrt(x * x + y * y + z * z);
                double sqrtXSqrYSqr = Math.Sqrt(x * x + y * y);
                if (rho != 0 && sqrtXSqrYSqr != 0)
                {
                    // This is the Jacobian from spherical to Cartesian coordinates:
                    jacobian.m00 = (float)(x / rho);
                    jacobian.m01 = (float)(y / rho);
                    jacobian.m02 = (float)(z / rho);
                    jacobian.m10 = (float)(x * z / (rho * rho * sqrtXSqrYSqr));
                    jacobian.m11 = (float)(y * z / (rho * rho * sqrtXSqrYSqr));
                    jacobian.m20 = (float)(-y / (x * x + y * y));
                    jacobian.m21 = (float)(x / (x * x + y * y));
                    jacobian.m12 = (float)(-sqrtXSqrYSqr / (rho * rho));
                    jacobian.m22 = 0;
                    jacobian.m33 = 1;

                    //To convert the coordinate system of the metric (or the "conformal factor," in this case,) we multiply this way by the Jacobian and its transpose.
                    //(*IMPORTANT NOTE: I'm assuming this "conformal factor" transforms like a true tensor, which not all matrices are. I need to do more research to confirm that
                    // it transforms the same way as the metric, but given that the conformal factor maps from Minkowski to another metric, I think this is a safe bet.)
                    return jacobian.transpose * sphericalConformalFactor * jacobian;
                }
                else
                {
                    sphericalConformalFactor.m22 = sphericalConformalFactor.m00;
                    sphericalConformalFactor.m00 = 1;
                    sphericalConformalFactor.m11 = 1;
                    return sphericalConformalFactor;
                }
            }
        }

        override public Matrix4x4 GetMetric(Vector4 stpiw, Vector4 pstpiw)
        {
            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            //So, let's just convert to spherical to get the conformal factor:
            Vector4 cartesianPos = stpiw - origin;
            Vector4 sphericalPos = cartesianPos.Cartesian4ToSpherical4();
            //Assume that spherical transform of input are Lemaître coordinates, since they "co-move" with the gravitational pull of the black hole:
            double rho = cartesianPos.magnitude;
            double tau = cartesianPos.w;

            //Convert to usual Schwarzschild solution r:
            double r = Math.Pow(Math.Pow(3.0 / 2.0 * (rho - SRelativityUtil.c * tau), 2) * radius, 1.0 / 3.0);
            
            //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
            // so we need to cut-off the interior metric at some point, for numerical sanity
            if (r <= radiusCutoff)
            {
                Matrix4x4 minkowski = Matrix4x4.identity;
                minkowski.m33 = SRelativityUtil.cSqrd;
                minkowski.m00 = -1;
                minkowski.m11 = -1;
                minkowski.m22 = -1;

                return minkowski;
            }
            else
            {
                double schwarzFac = 1 - radius / r;
                double playerR = ((Vector3)(pstpiw - origin)).magnitude;
                double playerSchwarzFac = 1;
                if (playerR > radiusCutoff)
                {
                    playerSchwarzFac = 1 - radius / playerR;
                }
                double sqrtRDivRs = Math.Sqrt(r / radius);
                double denomFac = (sqrtRDivRs - 1.0 / sqrtRDivRs) * (sqrtRDivRs - 1.0 / sqrtRDivRs);

                //Here's the value of the conformal factor at this distance in spherical coordinates with the orig at zero:
                Matrix4x4 sphericalMetric = Matrix4x4.zero;
                //(For the metric, rather than the conformal factor, the time coordinate would have its sign flipped relative to the spatial components,
                // either positive space and negative time, or negative time and positive space.)
                sphericalMetric[3, 3] = (float)(SRelativityUtil.cSqrd * playerSchwarzFac * (r - radius) / (radius * denomFac));
                sphericalMetric[0, 0] = (float)(-schwarzFac / (denomFac * playerSchwarzFac));
                sphericalMetric[1, 1] = (float)(-r * r);
                sphericalMetric[2, 2] = (float)(-r * r * Mathf.Pow(Mathf.Sin(sphericalPos.y), 2));

                //A particular useful "tensor" (which we can think of loosely here as "just a matrix") called the "Jacobian"
                // lets us convert the "metric tensor" (and other tensors) between coordinate systems, like from spherical back to Cartesian:
                Matrix4x4 jacobian = Matrix4x4.identity;
                double x = cartesianPos.x;
                double y = cartesianPos.y;
                double z = cartesianPos.z;
                rho = Math.Sqrt(x * x + y * y + z * z);
                double sqrtXSqrYSqr = Math.Sqrt(x * x + y * y);
                if (rho != 0 && sqrtXSqrYSqr != 0)
                {
                    // This is the Jacobian from spherical to Cartesian coordinates:
                    jacobian.m00 = (float)(x / rho);
                    jacobian.m01 = (float)(y / rho);
                    jacobian.m02 = (float)(z / rho);
                    jacobian.m10 = (float)(x * z / (rho * rho * sqrtXSqrYSqr));
                    jacobian.m11 = (float)(y * z / (rho * rho * sqrtXSqrYSqr));
                    jacobian.m20 = (float)(-y / (x * x + y * y));
                    jacobian.m21 = (float)(x / (x * x + y * y));
                    jacobian.m12 = (float)(-sqrtXSqrYSqr / (rho * rho));
                    jacobian.m22 = 0;
                    jacobian.m33 = 1;

                    //To convert the coordinate system of the metric (or the "conformal factor," in this case,) we multiply this way by the Jacobian and its transpose.
                    //(*IMPORTANT NOTE: I'm assuming this "conformal factor" transforms like a true tensor, which not all matrices are. I need to do more research to confirm that
                    // it transforms the same way as the metric, but given that the conformal factor maps from Minkowski to another metric, I think this is a safe bet.)
                    return jacobian.transpose * sphericalMetric * jacobian;
                }
                else
                {
                    sphericalMetric.m22 = sphericalMetric.m00;
                    sphericalMetric.m00 = 1;
                    sphericalMetric.m11 = 1;
                    return sphericalMetric;
                }
            }
        }

        override public Vector4 GetWorldAcceleration(Vector3 piw, Vector3 playerPiw)
        {
            //We'll assume the player is close enough to being at rest with respect to the singularity at a great distance away, at first,
            // so that we can ignore the player position, here.

            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            //So, let's just convert to spherical to get the conformal factor:
            Vector3 cartesianPos = piw - (Vector3)origin;
            Vector3 sphericalPos = cartesianPos.CartesianToSpherical();
            //Assume that spherical transform of input are Lemaître coordinates, since they "co-move" with the gravitational pull of the black hole:
            double rho = cartesianPos.magnitude;
            double tau = 0;

            //Convert to usual Schwarzschild solution r:
            double r = Math.Pow(Math.Pow(3.0 / 2.0 * (rho - SRelativityUtil.c * tau), 2) * radius, 1.0 / 3.0);

            if (r <= radiusCutoff)
            {
                return Vector4.zero;
            }
            else
            {
                //Initialize the Christoffel output as zero:
                Matrix4x4[] christoffels = new Matrix4x4[4];
                for (int i = 0; i < christoffels.Length; i++)
                {
                    christoffels[i] = Matrix4x4.zero;
                }


                double sqrtR = Math.Sqrt(r);
                double sqrtRs = Math.Sqrt(radius);
                //double t = 2 * sqrtR / sqrtRs * (sqrtR * (r + 3 * radius) - 3 * Math.Pow(radius, 3.0 / 2.0) * Math.Atan2(sqrtR, sqrtRs)) / (2 * sqrtR);

                double sinTheta = Math.Sin(sphericalPos.y);
                christoffels[3].m03 = (float)(radius / (2 * r * (r - radius)));
                christoffels[3].m30 = (float)(radius / (2 * r * (r - radius)));
                christoffels[0].m00 = -christoffels[3].m03;
                christoffels[0].m33 = (float)(radius * (r - radius) / (2 * r * r * r));
                christoffels[0].m22 = (float)(sinTheta * sinTheta * (radius - r));
                christoffels[0].m11 = (float)(radius - r);
                christoffels[1].m01 = (float)(1 / r);
                christoffels[1].m10 = (float)(1 / r);
                christoffels[2].m02 = (float)(1 / r);
                christoffels[2].m20 = (float)(1 / r);
                christoffels[1].m22 = (float)(-sinTheta * Math.Cos(sphericalPos.y));
                christoffels[2].m12 = (float)(1 / Math.Tan(sphericalPos.y));

                Vector4 fullDeriv = new Vector4((float)(-sqrtRs / (sqrtR * SRelativityUtil.c)), 0, 0, (float)(1 / (1 - radius / r)));
                Vector4 sphericalAccel = new Vector4(Vector4.Dot(fullDeriv, christoffels[0] * fullDeriv), Vector4.Dot(fullDeriv, christoffels[1] * fullDeriv), Vector4.Dot(fullDeriv, christoffels[2] * fullDeriv), Vector4.Dot(fullDeriv, christoffels[3] * fullDeriv));
                double playerR = (playerPiw - (Vector3)origin).magnitude;
                double playerSchwarzFac = 1;
                if (playerR > radiusCutoff)
                {
                    playerSchwarzFac = 1 - radius / playerR;
                }
                sphericalAccel = new Vector4((float)(sphericalAccel.x / playerSchwarzFac), sphericalAccel.y, sphericalAccel.z, (float)(sphericalAccel.w * playerSchwarzFac));
                Vector3 towardCenter = ((Vector3)sphericalAccel).magnitude * (cartesianPos - (Vector3)origin).normalized;
                return new Vector4(towardCenter.x, towardCenter.y, towardCenter.z, sphericalAccel.w);
            }
        }

        //override public Vector3 GetPlayerComovingPseudoVelocity(Vector3 piw)
        //{
        //    //We'll assume the player is close enough to being at rest with respect to the singularity at a great distance away, at first,
        //    // so that we can ignore the player position, here.

        //    Vector4 origin = transform.position;

        //    //We assume all input space-time-position-in-world vectors are Cartesian.
        //    //The Schwarzschild metric is most naturally expressed in spherical coordinates.
        //    //So, let's just convert to spherical to get the conformal factor:
        //    Vector3 cartesianPos = piw - (Vector3)origin;
        //    //Assume that spherical transform of input are Lemaître coordinates, since they "co-move" with the gravitational pull of the black hole:
        //    double rho = cartesianPos.magnitude;

        //    //Convert to usual Schwarzschild solution r:
        //    double r = Math.Pow(Math.Pow(3.0 / 2.0 * rho, 2) * radius, 1.0 / 3.0);

        //    //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
        //    // so we need to cut-off the interior metric at some point, for numerical sanity
        //    if (r <= radiusCutoff)
        //    {
        //        return Vector3.zero;
        //    }
        //    else
        //    {
        //        float sphericalVel = (float)(-SRelativityUtil.c * Math.Pow(2.0 * radius / (3.0f * rho), 1.0 / 3.0));
        //        return sphericalVel * cartesianPos.normalized;
        //    }
        //}
    }
}
