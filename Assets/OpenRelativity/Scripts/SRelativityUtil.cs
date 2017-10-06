using System;
using System.Collections;
using System.Collections.Generic;
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


        //This method converts the position of an object in the world to its position after the shader is applied.
        public static Vector3 WorldToOptical(this Vector3 realPos, Vector3 velocity)
        {
            return WorldToOptical(realPos, velocity, Vector3.zero, Vector3.zero);
        }

        public static Vector3 WorldToOptical(this Vector3 realPos, Vector3 velocity, Vector3 origin)
        {
            return WorldToOptical(realPos, velocity, origin, Vector3.zero);
        }

        private const float divByZeroCutoff = 1e-8f;

        public static Vector3 WorldToOptical(this Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            float spdOfLight = SRelativityUtil.c;

            Vector3 vpc = -playerVel / spdOfLight;// srCamera.PlayerVelocityVector;
            float playerVelMag = playerVel.magnitude;
            float speed = Mathf.Sqrt(Vector3.Dot(vpc, vpc)); // (float)srCamera.playerVelocity;
            Vector3 pos = piw - origin;
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
            Vector3 riw = pos; //Position that will be used in the output

            //Transform fails and is unecessary if relative speed is zero:
            if (speedr > divByZeroCutoff)
            {
                //Here begins a rotation-free modification of the original OpenRelativity shader:

                float c = -riw.sqrMagnitude; //first get position squared (position doted with position)

                float b = (2 * Vector3.Dot(riw, velocity)); //next get position doted with velocity, should be only in the Z direction

                float d = (spdOfLight * spdOfLight) - velocity.sqrMagnitude;

                float tisw = (-b - (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2 * d);

                //get the new position offset, based on the new time we just found
                //Should only be in the Z direction

                riw = riw + (tisw * velocity);

                //Apply Lorentz transform
                // float newz =(riw.z + state.PlayerVelocity * tisw) / state.SqrtOneMinusVSquaredCWDividedByCSquared;
                //I had to break it up into steps, unity was getting order of operations wrong.	
                float newz = (((float)speed * spdOfLight) * tisw);

                if (speed > divByZeroCutoff)
                {
                    Vector3 vpcUnit = -playerVel / playerVelMag;
                    newz = (Vector3.Dot(riw, vpcUnit) + newz) / Mathf.Sqrt(1 - (speed * speed));
                    riw = riw + (newz - Vector3.Dot(riw, vpcUnit)) * vpcUnit;
                }
            }

            riw += origin;

            return riw;
        }

        const int defaultOpticalToWorldMaxIterations = 5;
        const float defaultOpticalToWorldSqrErrorTolerance = 0.0001f;

        public static Vector3 OpticalToWorld(this Vector3 realPos, Vector3 velocity)
        {
            return realPos.OpticalToWorld(velocity, Vector3.zero, Vector3.zero);
        }

        public static Vector3 OpticalToWorld(this Vector3 realPos, Vector3 velocity, Vector3 origin)
        {
            return realPos.OpticalToWorld(velocity, origin, Vector3.zero);
        }

        public static Vector3 OpticalToWorld(this Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            float spdOfLight = SRelativityUtil.c;

            Vector3 vpc = -playerVel / spdOfLight;// srCamera.PlayerVelocityVector;
            float playerVelMag = playerVel.magnitude;
            float speed = Mathf.Sqrt(Vector3.Dot(vpc, vpc)); // (float)srCamera.playerVelocity;
            Vector3 pos = piw - origin;
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
            Vector3 riw = pos; //Position that will be used in the output

            //Transform fails and is unecessary if relative speed is zero:
            if (speedr > divByZeroCutoff)
            {
                float newz;
                if (speed > divByZeroCutoff)
                {
                    Vector3 vpcUnit = -playerVel / playerVelMag;
                    newz = Vector3.Dot(riw, vpcUnit) * Mathf.Sqrt(1 - (speed * speed));
                    riw = riw + (newz - Vector3.Dot(riw, vpcUnit)) * vpcUnit;
                }

                float c = -riw.sqrMagnitude;

                float b = 2.0f * Vector3.Dot(velocity, riw);

                float d = spdOfLight * spdOfLight - velocity.sqrMagnitude;

                float tisw = (-b - (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2 * d);

                newz = playerVelMag * tisw;

                if (speed > divByZeroCutoff)
                {
                    Vector3 vpcUnit = -playerVel / playerVelMag;
                    riw = riw - newz * vpcUnit;
                }

                riw = riw - (tisw * velocity);
            }

            riw += origin;

            return riw;

        }

        public static Vector3 OpticalToWorldHighPrecision(this Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            Vector3 startPoint = piw;
            Vector3 est = piw.OpticalToWorld(velocity, origin, playerVel);
            Vector3 newEst;
            Vector3 offset = piw - est.WorldToOptical(velocity, origin, playerVel);
            float sqrError = (piw - est.WorldToOptical(velocity, origin, playerVel)).sqrMagnitude;
            float oldSqrError = sqrError + 1.0f;
            float iterations = 1;
            while ( (iterations < defaultOpticalToWorldMaxIterations)
                && (sqrError > defaultOpticalToWorldSqrErrorTolerance)
                && (sqrError < oldSqrError) )
            {
                iterations++;
                startPoint += offset / 2.0f;
                newEst = startPoint.OpticalToWorld(velocity, origin, playerVel);
                offset = startPoint - newEst.WorldToOptical(velocity, origin, playerVel);
                oldSqrError = sqrError;
                sqrError = (piw - newEst.WorldToOptical(velocity, origin, playerVel)).sqrMagnitude;
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
    }
}