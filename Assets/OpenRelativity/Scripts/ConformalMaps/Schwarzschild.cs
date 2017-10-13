using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Schwarzschild : ConformalMap
    {
        public float radius = 1;
        public float radiusCutoff = 1;

        private Vector4 origin;

        public void Awake()
        {
            Vector3 pos = transform.position;
            origin = new Vector4(pos.x, pos.y, pos.z, 0);
        }

        override public Matrix4x4 GetConformalFactor(Vector4 stpiw)
        {
            //We assume all input space-time-position-in-world vectors are Cartesian.
            //The Schwarzschild metric is most naturally expressed in spherical coordinates.
            //So, let's just convert to spherical to get the conformal factor:
            Vector4 sphericalPos = (stpiw - origin).SphericalToCartesian();
            float dist = (new Vector3(sphericalPos.x, sphericalPos.y, sphericalPos.z)).magnitude;
            //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
            // so we need to cut-off the interior metric at some point, for numerical sanity:

            if (dist <= radiusCutoff)
            {
                return Matrix4x4.identity;
            }
            else
            {

                float cSqr = SRelativityUtil.cSqrd;
                float schwarzFac = (1 - radius / dist);

                //Here's the value of the conformal factor at this distance in spherical coordinates with the orig at zero:
                Matrix4x4 sphericalConformalFactor = Matrix4x4.zero;
                //(For the metric, rather than the conformal factor, the time coordinate would have its sign flipped relative to the spatial components,
                // either positive space and negative time, or negative time and positive space.)
                sphericalConformalFactor[3, 3] = schwarzFac;
                sphericalConformalFactor[0, 0] = 1 / schwarzFac;
                sphericalConformalFactor[1, 1] = dist * dist;
                sphericalConformalFactor[2, 2] = dist * dist * Mathf.Pow(Mathf.Sin(sphericalPos.y), 2);

                //A particular useful "tensor" (which we can think of loosely here as "just a matrix") called the "Jacobian"
                // lets us convert the "metric tensor" (and other tensors) between coordinate systems, like from spherical back to Cartesian:
                Matrix4x4 jacobian = Matrix4x4.identity;
                float sinTheta = Mathf.Sin(sphericalPos.y);
                float cosTheta = Mathf.Cos(sphericalPos.y);
                float sinPhi = Mathf.Sin(sphericalPos.z);
                float cosPhi = Mathf.Cos(sphericalPos.z);
                float rho = sphericalPos.x;
                // This is the Jacobian from spherical to Cartesian coordinates:
                jacobian.m00 = sinTheta * cosPhi;
                jacobian.m01 = rho * cosTheta * cosPhi;
                jacobian.m02 = -rho * sinTheta * sinPhi;
                jacobian.m10 = sinTheta * sinPhi;
                jacobian.m11 = rho * cosTheta * sinPhi;
                jacobian.m12 = rho * sinTheta * cosPhi;
                jacobian.m20 = cosPhi;
                jacobian.m21 = -rho * sinTheta;
                jacobian.m22 = 0;

                //To convert the coordinate system of the metric (or the "conformal factor," in this case,) we multiply this way by the Jacobian and its transpose.
                //(*IMPORTANT NOTE: I'm assuming this "conformal factor" transforms like a true tensor, which not all matrices are. I need to do more research to confirm that
                // it transforms the same way as the metric, but given that the conformal factor maps from Minkowski to another metric, I think this is a safe bet.)
                return jacobian.transpose * sphericalConformalFactor * jacobian;
            }
        }
    }
}
