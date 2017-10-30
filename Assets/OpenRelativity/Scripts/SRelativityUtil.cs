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
        public static Matrix4x4 GetMetric(Vector4 stpiw, Vector4 pstpiw)
        {
            return srCamera.conformalMap.GetMetric(stpiw, pstpiw);
        }
        public static Matrix4x4 GetConformalFactor(Vector4 stpiw, Vector4 pstpiw)
        {
            return srCamera.conformalMap.GetConformalFactor(stpiw, pstpiw);
        }
        public static Vector4 GetWorldAcceleration(Vector3 piw, Vector3 playerPos)
        {
            return srCamera.conformalMap.GetWorldAcceleration(piw, playerPos);
        }

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


        //This method converts the position of an object in the world to its position after the shader is applied.
        //public static Vector3 WorldToOptical(this Vector3 piw, Vector3 velocity, Matrix4x4? metric = null)
        //{
        //    return ((Vector4)piw).WorldToOptical(velocity, Vector3.zero, Vector3.zero, metric);
        //}

        //This method converts the position of an object in the world to its position after the shader is applied.
        public static Vector3 WorldToOptical(this Vector4 stpiw, Vector3 velocity, Matrix4x4? metric = null, Vector4? accel = null)
        {
            return stpiw.WorldToOptical(velocity, Vector3.zero, Vector3.zero, metric, accel);
        }

        //public static Vector3 WorldToOptical(this Vector3 piw, Vector3 velocity, Vector3 origin, Matrix4x4? metric = null)
        //{
        //    return ((Vector4)piw).WorldToOptical(velocity, origin, Vector3.zero, metric);
        //}

        public static Vector3 WorldToOptical(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Matrix4x4? metric = null, Vector4? accel = null)
        {
            return stpiw.WorldToOptical(velocity, origin, Vector3.zero, metric, accel);
        }

        //public static Vector3 WorldToOptical(this Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Matrix4x4? metric = null)
        //{
        //    return ((Vector4)piw).WorldToOptical(velocity, origin, playerVel, metric);
        //}

        public const float divByZeroCutoff = 1e-8f;

        public static Vector3 WorldToOptical(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Matrix4x4? metric = null, Vector4? accel = null)
        {
            float spdOfLight = SRelativityUtil.c;

            Vector3 vpc = -playerVel / spdOfLight;// srCamera.PlayerVelocityVector;
            float playerVelMag = playerVel.magnitude;
            float speed = Mathf.Sqrt(Vector3.Dot(vpc, vpc)); // (float)srCamera.playerVelocity;
            Vector4 pos = stpiw - (Vector4)origin;
            Vector3 viw = velocity / spdOfLight;

            float vuDot = Vector3.Dot(vpc, viw); //Get player velocity dotted with velocity of the object.
            Vector3 vr;
            //IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speed > divByZeroCutoff)
            {
                Vector3 uparra = (vuDot / (speed * speed)) * vpc; //Get the parallel component of the object's velocity
                                                                 //Get the perpendicular component of our velocity, just by subtraction
                Vector3 uperp = viw - uparra;
                //relative velocity calculation
                vr = (vpc - uparra - (Mathf.Sqrt(1 - speed * speed)) * uperp) / (1 + vuDot);
            }
            //If our speed is nearly zero, it could lead to infinities.
            else
            {
                //relative velocity calculation
                vr = -viw;
            }

            //relative speed
            float speedr = vr.magnitude;

            //riw = location in world, for reference
            Vector4 riw = pos;//Position that will be used in the output
            if (metric == null)
            {
                metric = GetMetric(stpiw, origin);
            }

            Vector4 velocity4 = velocity.To4Viw();

            //Here begins a rotation-free modification of the original OpenRelativity shader:

            float c = Vector4.Dot(riw, metric.Value * riw); //first get position squared (position dotted with position)

            float b = -(2 * Vector4.Dot(riw, metric.Value * velocity4)); //next get position dotted with velocity, should be only in the Z direction

            float d = cSqrd;

            float tisw = 0;
            if ((b * b) >= 4.0 * d * c)
            {
                tisw = (-b + (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2 * d);
            }

            //get the new position offset, based on the new time we just found

            if (accel == null)
            {
                accel = GetWorldAcceleration(stpiw, origin);
            }
            Vector3 apparentAccel = -accel.Value;

            riw = (Vector3)riw + (tisw * velocity) + (apparentAccel * Mathf.Abs(tisw) * tisw / 2.0f);

            //Apply Lorentz transform
            //I had to break it up into steps, unity was getting order of operations wrong.	
            float newz = (((float)speed * spdOfLight) * tisw);

            if (speed > divByZeroCutoff)
            {
                Vector4 vpcUnit = vpc / speed;
                newz = (Vector4.Dot(riw, vpcUnit) + newz) * Mathf.Sqrt(1 - (speed * speed));
                riw = riw + (newz - Vector4.Dot(riw, vpcUnit)) * vpcUnit;
            }

            riw = (Vector3)riw + origin;

            return riw;
        }

        const int defaultOpticalToWorldMaxIterations = 5;
        const float defaultOpticalToWorldSqrErrorTolerance = 0.0001f;

        //public static Vector3 OpticalToWorld(this Vector3 piw, Vector3 velocity, Matrix4x4? metric = null)
        //{
        //    return ((Vector4)piw).OpticalToWorld(velocity, Vector3.zero, Vector3.zero, metric);
        //}

        public static Vector3 OpticalToWorld(this Vector4 stpiw, Vector3 velocity, Matrix4x4? metric = null, Vector4? accel = null)
        {
            return stpiw.OpticalToWorld(velocity, Vector3.zero, Vector3.zero, metric, accel);
        }

        //public static Vector3 OpticalToWorld(this Vector3 piw, Vector3 velocity, Vector3 origin, Matrix4x4? metric = null)
        //{
        //    return ((Vector4)piw).OpticalToWorld(velocity, origin, Vector3.zero, metric);
        //}

        public static Vector3 OpticalToWorld(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Matrix4x4? metric = null, Vector4? accel = null)
        {
            return stpiw.OpticalToWorld(velocity, origin, Vector3.zero, metric, accel);
        }

        //public static Vector3 OpticalToWorld(this Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Matrix4x4? metric = null)
        //{
        //    return ((Vector4)piw).OpticalToWorld(velocity, origin, playerVel, metric);
        //}

        public static Vector3 OpticalToWorld(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Vector3 playerVel, Matrix4x4? metric = null, Vector4? accel = null)
        {
            float spdOfLight = SRelativityUtil.c;

            Vector3 vpc = -playerVel / spdOfLight;// srCamera.PlayerVelocityVector;
            float playerVelMag = playerVel.magnitude;
            float speed = Mathf.Sqrt(Vector3.Dot(vpc, vpc)); // (float)srCamera.playerVelocity;
            Vector4 pos = stpiw - (Vector4)origin;
            Vector3 viw = velocity / spdOfLight;

            float vuDot = Vector3.Dot(vpc, viw); //Get player velocity dotted with velocity of the object.
            Vector3 vr;
            //IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speed > divByZeroCutoff)
            {
                Vector3 uparra = (vuDot / (speed * speed)) * vpc; //Get the parallel component of the object's velocity
                                                                  //Get the perpendicular component of our velocity, just by subtraction
                Vector3 uperp = viw - uparra;
                //relative velocity calculation
                vr = (vpc - uparra - (Mathf.Sqrt(1 - speed * speed)) * uperp) / (1 + vuDot);
            }
            //If our speed is nearly zero, it could lead to infinities.
            else
            {
                //relative velocity calculation
                vr = -viw;
            }
            float speedr = vr.magnitude;

            //riw = location in world, for reference
            Vector4 riw = pos; //Position that will be used in the output
            //Transform fails and is unecessary if relative speed is zero:
            float newz;
            if (speed > divByZeroCutoff)
            {
                Vector4 vpcUnit = -(Vector4)playerVel / playerVelMag;
                newz = Vector4.Dot((Vector3)riw, vpcUnit) / Mathf.Sqrt(1 - (speed * speed));
                riw = riw + (newz - Vector4.Dot((Vector3)riw, vpcUnit)) * vpcUnit;
            }

            if (metric == null)
            {
                metric = GetMetric(stpiw, origin);
            }

            Vector4 pVel4 = (-playerVel).To4Viw();

            float c = Vector4.Dot(riw, metric.Value * riw); //first get position squared (position dotted with position)

            float b = -(2 * Vector4.Dot(riw, metric.Value * pVel4)); //next get position dotted with velocity, should be only in the Z direction

            float d = cSqrd;

            float tisw = 0;
            if ((b * b) >= 4.0 * d * c)
            {
                tisw = (-b + (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2 * d);
            }

            newz = playerVelMag * tisw;

            if (speed > divByZeroCutoff)
            {
                Vector4 vpcUnit = -playerVel / playerVelMag;
                riw = riw - newz * vpcUnit;
            }

            if (accel == null)
            {
                accel = GetWorldAcceleration(stpiw, origin);
            }
            Vector3 apparentAccel = -accel.Value;

            riw = (Vector3)riw - (tisw * velocity) - (apparentAccel * Mathf.Abs(tisw) * tisw / 2.0f);

            riw = (Vector3)riw + origin;

            return riw;

        }

        //public static Vector3 OpticalToWorldHighPrecision(this Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        //{
        //    return ((Vector4)piw).OpticalToWorldHighPrecision(velocity, origin, playerVel);
        //}

        public static Vector3 OpticalToWorldHighPrecision(this Vector4 stpiw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            Vector4 startPoint = stpiw;
            Vector3 est = stpiw.OpticalToWorld(velocity, origin, playerVel);
            Vector3 newEst;
            Vector3 offset = (Vector3)stpiw - ((Vector4)est).WorldToOptical(velocity, origin, playerVel);
            float sqrError = offset.sqrMagnitude;
            float oldSqrError = sqrError + 1.0f;
            float iterations = 1;
            while ( (iterations < defaultOpticalToWorldMaxIterations)
                && (sqrError > defaultOpticalToWorldSqrErrorTolerance)
                && (sqrError < oldSqrError) )
            {
                iterations++;
                startPoint += (Vector4)offset / 2.0f;
                newEst = startPoint.OpticalToWorld(velocity, origin, playerVel);
                offset = (Vector3)startPoint - ((Vector4)newEst).WorldToOptical(velocity, origin, playerVel);
                oldSqrError = sqrError;
                sqrError = ((Vector3)stpiw - ((Vector4)newEst).WorldToOptical(velocity, origin, playerVel)).sqrMagnitude;
                if (sqrError < oldSqrError)
                {
                    est = newEst;
                }
            }

            return est;
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

        public static Vector3 SphericalToCartesian(this Vector3 spherical)
        {
            float radius = spherical.x;
            float elevation = spherical.y;
            float polar = spherical.z;
            Vector3 outCart;

            float a = radius * Mathf.Cos(elevation);
            outCart.x = a * Mathf.Cos(polar);
            outCart.y = radius * Mathf.Sin(elevation);
            outCart.z = a * Mathf.Sin(polar);

            return outCart;
        }

        public static Vector4 Spherical4ToCartesian4(this Vector4 spherical)
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

        public static Vector3 CartesianToSpherical(this Vector3 cartesian)
        {
            float radius, polar, elevation;
            radius = cartesian.magnitude;
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

            return new Vector3(radius, elevation, polar);
        }

        public static Vector4 Cartesian4ToSpherical4(this Vector4 cartesian)
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

        public static Vector4 To4Viw(this Vector3 viw)
        {

            return new Vector4(viw.x, viw.y, viw.z, (float)Math.Sqrt(1.0 - viw.sqrMagnitude / cSqrd));
        }
    }
}