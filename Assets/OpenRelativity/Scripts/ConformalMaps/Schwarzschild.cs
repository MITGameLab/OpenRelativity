using System;
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
            Vector4 cartesianPos = stpiw - origin;
            Vector4 sphericalPos = cartesianPos.CartesianToSpherical();
            float dist = cartesianPos.magnitude;
            //At the center of the coordinate system is a singularity, at the Schwarzschild radius is an event horizon,
            // so we need to cut-off the interior metric at some point, for numerical sanity:

            if (dist <= radiusCutoff)
            {
                return Matrix4x4.identity;
            }
            else
            {
                float schwarzFac = (1 - radius / dist);

                //Here's the value of the conformal factor at this distance in spherical coordinates with the orig at zero:
                Matrix4x4 sphericalConformalFactor = Matrix4x4.zero;
                //(For the metric, rather than the conformal factor, the time coordinate would have its sign flipped relative to the spatial components,
                // either positive space and negative time, or negative time and positive space.)
                sphericalConformalFactor[3, 3] = 1 /schwarzFac;
                sphericalConformalFactor[0, 0] = schwarzFac;
                sphericalConformalFactor[1, 1] = dist * dist;
                sphericalConformalFactor[2, 2] = dist * dist * Mathf.Pow(Mathf.Sin(sphericalPos.y), 2);

                //A particular useful "tensor" (which we can think of loosely here as "just a matrix") called the "Jacobian"
                // lets us convert the "metric tensor" (and other tensors) between coordinate systems, like from spherical back to Cartesian:
                Matrix4x4 jacobian = Matrix4x4.identity;
                double x = cartesianPos.x;
                double y = cartesianPos.y;
                double z = cartesianPos.z;
                double rho = Math.Sqrt(x * x + y * y + z * z);
                double sqrtXSqrYSqr = Math.Sqrt(x * x + y * y);
                // This is the Jacobian from spherical to Cartesian coordinates:
                jacobian.m00 = (float)(x / rho);
                jacobian.m01 = (float)(y / rho);
                jacobian.m02 = (float)(z / rho);
                jacobian.m10 = (float)(x * z / (rho * rho * sqrtXSqrYSqr));
                jacobian.m11 = (float)(y * z / (rho * rho * sqrtXSqrYSqr));
                jacobian.m12 = (float)(-sqrtXSqrYSqr / (rho * rho));
                jacobian.m20 = (float)(-y / (x * x + y * y));
                jacobian.m21 = (float)(x / (x * x + y * y));
                jacobian.m22 = 0;
                jacobian.m33 = 1;

                //To convert the coordinate system of the metric (or the "conformal factor," in this case,) we multiply this way by the Jacobian and its transpose.
                //(*IMPORTANT NOTE: I'm assuming this "conformal factor" transforms like a true tensor, which not all matrices are. I need to do more research to confirm that
                // it transforms the same way as the metric, but given that the conformal factor maps from Minkowski to another metric, I think this is a safe bet.)
                return jacobian.transpose * sphericalConformalFactor * jacobian;
            }
        }

        public override Vector3 OpticalToWorld(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            throw new NotImplementedException();
        }

        public override Vector3 WorldToOptical(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            throw new NotImplementedException();
        }
    }
}
