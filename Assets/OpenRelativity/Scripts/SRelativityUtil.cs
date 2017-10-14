using System;
using System.Reflection;
using UnityEngine;

namespace OpenRelativity
{
    public static class SRelativityUtil
    {
        public static float c { get { return (float)srCamera.SpeedOfLight; } }
        public static float cSqrd { get { return (float)srCamera.SpeedOfLightSqrd; } }
        public static float maxVel { get { return (float)srCamera.MaxSpeed; } }

        private static GameState _srCamera;
        private static GameState srCamera
        {
            get
            {
                if (_srCamera == null)
                {
                    GameObject cameraGO = GameObject.FindGameObjectWithTag(Tags.player);
                    _srCamera = cameraGO.GetComponent<GameState>();
                }

                return _srCamera;
            }
        }

        private static Vector3 GetSpatial(this Vector4 st)
        {
            return new Vector3(st.x, st.y, st.z);
        }

        private static Vector4 MakeVel(this Vector3 spatial)
        {
            return new Vector4(spatial.x, spatial.y, spatial.z, c);

        }

        public static Vector3 AddVelocity(this Vector3 orig, Vector3 toAdd)
        {
            Vector3 parra = Vector3.Project(toAdd, orig);
            Vector3 perp = toAdd - parra;
            perp = orig.InverseGamma() * perp / (1.0f + Vector3.Dot(orig, parra) / cSqrd);
            parra = (parra + orig) / (1.0f + Vector3.Dot(orig, parra) / cSqrd);
            return parra + perp;
        }

        public static Vector4 AddVelocity(this Vector4 orig, Vector4 toAdd)
        {
            Vector3 new3Vel = orig.GetSpatial().AddVelocity(toAdd.GetSpatial());
            return new Vector4(new3Vel.x, new3Vel.y, new3Vel.z, c);
        }

        public static Vector3 RelativeVelocityTo(this Vector3 myWorldVel, Vector3 otherWorldVel)
        {
            float speedSqr = myWorldVel.sqrMagnitude / cSqrd;
            //Get player velocity dotted with velocity of the object.
            float vuDot = Vector3.Dot(myWorldVel, otherWorldVel) / cSqrd;
            Vector3 vr;
            //If our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speedSqr != 0)
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

        public static Vector4 RelativeVelocityTo(this Vector4 myWorldVel, Vector4 otherWorldVel)
        {
            Vector3 new3Vel = myWorldVel.GetSpatial().RelativeVelocityTo(otherWorldVel.GetSpatial());
            return new Vector4(new3Vel.x, new3Vel.y, new3Vel.z, c);
        }


        public static Vector4 ContractLengthBy(this Vector4 interval, Vector4 velocity)
        {
            float sqrMag = velocity.sqrMagnitude;
            if (sqrMag == 0.0f)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f + sqrMag / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(1.0f / sqrMag * velocity, Vector3.right);
            Vector3 rotInt = rot * new Vector3(interval.x, interval.y, interval.z);
            rotInt = new Vector3(rotInt.x * invGamma, rotInt.y, rotInt.z);
            rotInt = Quaternion.Inverse(rot) * rotInt;
            return new Vector4(rotInt.x, rotInt.y, rotInt.z, interval.w / invGamma);
        }
        public static Vector4 InverseContractLengthBy(this Vector4 interval, Vector4 velocity)
        {
            float sqrMag = velocity.sqrMagnitude;
            if (sqrMag == 0.0f)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f + sqrMag / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(1.0f / sqrMag * velocity, Vector3.right);
            Vector3 rotInt = rot * new Vector3(interval.x, interval.y, interval.z);
            rotInt = new Vector3(rotInt.x / invGamma, rotInt.y, rotInt.z);
            rotInt = Quaternion.Inverse(rot) * rotInt;
            return new Vector4(rotInt.x, rotInt.y, rotInt.z, interval.w * invGamma);
        }

        public static Vector3 ContractLengthBy(this Vector3 interval, Vector3 velocity)
        {
            float speedSqr = velocity.sqrMagnitude;
            if (speedSqr == 0.0)
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
            if (speedSqr == 0.0)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f - speedSqr / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(velocity / Mathf.Sqrt(speedSqr), Vector3.forward);
            Vector3 rotInt = rot * interval;
            rotInt = new Vector3(rotInt.x, rotInt.y, rotInt.z / invGamma);
            return Quaternion.Inverse(rot) * rotInt;
        }

        public static float Gamma(this Vector3 velocity)
        {
            return 1.0f / Mathf.Sqrt(1.0f - velocity.sqrMagnitude / SRelativityUtil.cSqrd);
        }

        public static float InverseGamma(this Vector3 velocity)
        {
            return 1.0f / Mathf.Sqrt(1.0f + velocity.sqrMagnitude / SRelativityUtil.cSqrd);
        }

        public static Vector3 RapidityToVelocity(this Vector3 rapidity, float? altMag = null)
        {
            float mag = altMag ?? rapidity.magnitude;
            if (mag == 0.0f)
            {
                return Vector3.zero;
            }
            return (float)(SRelativityUtil.c * Math.Tanh(mag / SRelativityUtil.c) / mag) * rapidity;
        }

        public static Vector3 GetGravity(Vector3 origin)
        {
            return Physics.gravity;
        }

        public static Vector4 LorentzTransform(this Vector4 pos4, Vector3 vel3)
        {
            float gamma = vel3.Gamma();
            Vector3 pos3 = pos4;
            Vector3 parra = Vector3.Project(pos3, vel3.normalized);
            float tnew = gamma * (pos4.w - Vector3.Dot(parra, vel3) / SRelativityUtil.cSqrd);
            pos3 = gamma * (parra - vel3 * pos4.w) + (pos3 - parra);
            return new Vector4(pos3.x, pos3.y, pos3.z, tnew);
        }

        public static Vector4 InverseLorentzTransform(this Vector4 pos4, Vector3 vel3)
        {
            float gamma = vel3.Gamma();
            Vector3 pos3 = pos4;
            Vector3 parra = Vector3.Project(pos3, vel3.normalized);
            float tnew = gamma * (pos4.w + Vector3.Dot(parra, vel3) / SRelativityUtil.cSqrd);
            pos3 = gamma * (parra + vel3 * pos4.w) + (pos3 - parra);
            return new Vector4(pos3.x, pos3.y, pos3.z, tnew);
        }

        //http://answers.unity3d.com/questions/530178/how-to-get-a-component-from-an-object-and-add-it-t.html
        public static T GetCopyOf<T>(this Component comp, T other) where T : Component
        {
            Type type = comp.GetType();
            if (type != other.GetType()) return null; // type mis-match
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {
                if (pinfo.CanWrite)
                {
                    try
                    {
                        pinfo.SetValue(comp, pinfo.GetValue(other, null), null);
                    }
                    catch { } // In case of NotImplementedException being thrown. For some reason specifying that exception didn't seem to catch it, so I didn't catch anything specific.
                }
            }
            FieldInfo[] finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                finfo.SetValue(comp, finfo.GetValue(other));
            }
            return comp as T;
        }

        public static T AddComponent<T>(this GameObject go, T toAdd) where T : Component
        {
            return go.AddComponent<T>().GetCopyOf(toAdd) as T;
        }

        public static Vector4 SphericalToCartesian(this Vector4 spherical)
        {
            float radius = spherical.x;
            float elevation = spherical.y;
            float polar = spherical.z;
            float time = spherical.w;
            Vector4 outCart;

            float a = radius * Mathf.Cos(elevation);
            outCart.x = a * Mathf.Cos(polar);
            outCart.y = radius * Mathf.Sin(elevation);
            outCart.z = a * Mathf.Sin(polar);
            outCart.w = time;

            return outCart;
        }

        public static Vector4 CartesianToSpherical(this Vector4 cartesian)
        {
            float outTime = cartesian.w;
            float radius, polar, elevation;
            Vector3 spatial = new Vector3(cartesian.x, cartesian.y, cartesian.z);
            radius = spatial.magnitude;
            if (radius == 0)
            {
                polar = 0;
                elevation = 0;
            }
            else {
                float sqrtXSqrYSqr = Mathf.Sqrt(cartesian.x * cartesian.x + cartesian.y * cartesian.y);
                if ((cartesian.z == 0) && (sqrtXSqrYSqr == 0))
                {
                    elevation = 0;
                }
                else
                {
                    elevation = Mathf.Atan2(sqrtXSqrYSqr, cartesian.z);
                }

                if ((cartesian.y == 0) && (cartesian.x == 0))
                {
                    polar = 0;
                }
                else
                {
                    polar = Mathf.Atan2(cartesian.y, cartesian.x);
                    
                }
            }

            return new Vector4(radius, elevation, polar, outTime);
        }
    }
}