using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public GameState state;
        public Transform eventHorizon;
        public float radius = 1;
        public float radiusCutoff = 1;

        override public Matrix4x4 LocalToWorld(Vector4 stpiw)
        {
            if (radius < radiusCutoff)
            {
                return Matrix4x4.identity;
            }

            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            Vector4 cartesianPos = stpiw - origin;

            //Assume that spherical transform of input are Lemaître coordinates, since they "co-move" with the gravitational pull of the black hole:
            float rho = ((Vector3)cartesianPos).magnitude;
            float tau = cartesianPos.w;

            //Convert to usual Schwarzschild solution r:
            float r = Mathf.Pow(Mathf.Pow(3.0f / 2.0f * (rho - SRelativityUtil.c * tau), 2) * radius, 1.0f / 3.0f);

            //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
            // so we need to cut-off the interior metric at some point, for numerical sanity
            if (r <= radiusCutoff)
            {
                return Matrix4x4.identity;
            }

            float sqrtRDivRs = Mathf.Sqrt(r / radius);

            //Here's the value of the Lemaitre-to-Schwarzschild Jacobian ("comoving"-to-"world") at the object's position in spherical coordinates:
            Matrix4x4 objectToWorldJacobian = Matrix4x4.zero;
            objectToWorldJacobian[3, 3] = sqrtRDivRs / (sqrtRDivRs - 1.0f / sqrtRDivRs);
            objectToWorldJacobian[3, 0] = (1.0f / sqrtRDivRs) / (-sqrtRDivRs + 1.0f / sqrtRDivRs) / SRelativityUtil.c;
            objectToWorldJacobian[0, 3] = SRelativityUtil.c * (radius - r) / (r * (sqrtRDivRs - 1.0f / sqrtRDivRs));
            objectToWorldJacobian[0, 0] = (r - radius) / (r * (1.0f / sqrtRDivRs - sqrtRDivRs));
            objectToWorldJacobian[1, 1] = 1;
            objectToWorldJacobian[2, 2] = 1;

            return SphericalToCartesian(cartesianPos) * objectToWorldJacobian;
        }

        override public Matrix4x4 WorldToLocal(Vector4 pstpiw)
        {
            if (radius < radiusCutoff)
            {
                return Matrix4x4.identity;
            }

            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            Vector4 cartesianPos = pstpiw - origin;
            //Assume that spherical transform of input are "Unity world coordinates," and we're converting to local (player) coordinates:
            float r = ((Vector3)cartesianPos).magnitude;
            float schwarzFac = 1 - radius / r;
            float sqrtRDivRs = Mathf.Sqrt(r / radius);
            //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
            // so we need to cut-off the interior metric at some point, for numerical sanity
            if (r <= radiusCutoff)
            {
                return Matrix4x4.identity;
            }

            //Here's the value of the Lemaitre-to-Schwarzschild Jacobian ("comoving"-to-"world") at the object's position in spherical coordinates:
            Matrix4x4 worldToPlayerJacobian = Matrix4x4.zero;
            worldToPlayerJacobian[3, 3] = 1;
            worldToPlayerJacobian[3, 0] = ((1.0f / sqrtRDivRs) / schwarzFac) / SRelativityUtil.c;
            worldToPlayerJacobian[0, 3] = SRelativityUtil.c;
            worldToPlayerJacobian[0, 0] = sqrtRDivRs / schwarzFac;
            worldToPlayerJacobian[1, 1] = 1;
            worldToPlayerJacobian[2, 2] = 1;

            return SphericalToCartesian(pstpiw) * worldToPlayerJacobian;
        }

        public override Matrix4x4 WorldCoordMetric(Vector4 stpiw)
        {
            Vector4 origin = transform.position;
            Vector4 cartesianPos = stpiw - origin;

            float r = ((Vector3)cartesianPos).magnitude;
            float schwarzFac = 1 - radius / r;
            float sqrtSchwarzFac = Mathf.Sqrt(schwarzFac);

            // By convention, all of our metrics map to proper DISTANCE, as opposed to proper TIME.
            // (The metric is "intrinsic," i.e. proper distances are independent of coordinates,
            // except, numerically, we're actually always expecting inputs in a particular coordinate system,
            // and this is specifically the Unity "world" coordinate system, for this abstract method.)
            Matrix4x4 metric = Matrix4x4.identity;

            // "3" or "w" is the time index, and seconds * (meters / second) gives a quantity with units of "meters", (i.e. "distance")
            metric[3, 3] = -SRelativityUtil.c * sqrtSchwarzFac;
            metric[0, 0] = sqrtSchwarzFac;

            Matrix4x4 stc = SphericalToCartesian(stpiw);

            return stc * metric * stc.inverse;
        }

        public Matrix4x4 SphericalToCartesian(Vector4 stpiw)
        {
            Matrix4x4 sphericalToCartesionJacobian = Matrix4x4.identity;
            float x = stpiw.x;
            float y = stpiw.y;
            float z = stpiw.z;
            float rho = Mathf.Sqrt(x * x + y * y + z * z);
            float sqrtXSqrYSqr = Mathf.Sqrt(x * x + y * y);

            // If we're numerically effectively at the origin, we can't divide by zero, and this is correct anyway.
            if (rho == 0 || sqrtXSqrYSqr == 0)
            {
                return Matrix4x4.identity;
            }

            // This is the Jacobian from spherical to Cartesian coordinates:
            sphericalToCartesionJacobian.m00 = x / rho;
            sphericalToCartesionJacobian.m01 = y / rho;
            sphericalToCartesionJacobian.m02 = z / rho;
            sphericalToCartesionJacobian.m10 = x * z / (rho * rho * sqrtXSqrYSqr);
            sphericalToCartesionJacobian.m11 = y * z / (rho * rho * sqrtXSqrYSqr);
            sphericalToCartesionJacobian.m20 = -y / (x * x + y * y);
            sphericalToCartesionJacobian.m21 = x / (x * x + y * y);
            sphericalToCartesionJacobian.m12 = -sqrtXSqrYSqr / (rho * rho);
            sphericalToCartesionJacobian.m22 = 0;
            sphericalToCartesionJacobian.m33 = 1;

            return sphericalToCartesionJacobian;
        }

        void FixedUpdate()
        {
            if (!Double.IsInfinity(state.FixedDeltaTimeWorld)) {
                radius = radius - ((float)state.FixedDeltaTimeWorld * SRelativityUtil.c * 2.0f / radius);
            }

            if (radius < radiusCutoff)
            {
                radius = 0;
            }

            eventHorizon.localScale = new Vector3(radius, radius, radius);
        }
    }
}
