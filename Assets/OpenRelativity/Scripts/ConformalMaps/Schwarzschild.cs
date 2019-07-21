using System;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public float radius = 1;
        public float radiusCutoff = 1;

        override public Matrix4x4 LocalToWorld(Vector4 stpiw)
        {
            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            Vector4 cartesianPos = stpiw - origin;
            Vector4 sphericalPos = cartesianPos.Cartesian4ToSpherical4();

            //Assume that spherical transform of input are Lemaître coordinates, since they "co-move" with the gravitational pull of the black hole:
            float rho = cartesianPos.magnitude;
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
            Vector4 origin = transform.position;

            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            Vector4 cartesianPos = pstpiw - origin;
            Vector4 sphericalPos = cartesianPos.Cartesian4ToSpherical4();
            //Assume that spherical transform of input are "Unity world coordinates," and we're converting to local (player) coordinates:
            float r = cartesianPos.magnitude;
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
    }
}
