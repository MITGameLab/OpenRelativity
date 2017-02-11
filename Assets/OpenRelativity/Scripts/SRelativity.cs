using System.Collections;
using System.Collections.Generic;
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
            float toAddMag = toAdd.magnitude;
            float origMag = orig.magnitude;
            if (origMag == 0.0f)
            {
                if (toAddMag <= maxVel)
                {
                    return toAdd;
                }
                else
                {
                    return maxVel / toAddMag * toAdd;
                }
            }
            else if (toAddMag == 0.0f)
            {
                if (origMag <= maxVel)
                {
                    return orig;
                }
                else
                {
                    return maxVel / origMag * orig;
                }
            }
            else
            {
                Quaternion rot = Quaternion.FromToRotation(orig.normalized, Vector3.forward);
                Vector3 toAddRot = rot * toAdd;
                float denom = 1.0f / (1.0f + toAddRot.z * origMag / cSqrd);
                float invGammaDenom = denom * Mathf.Sqrt(1.0f - orig.sqrMagnitude / cSqrd);

                Vector3 toRet = Quaternion.Inverse(rot) * new Vector3(
                    toAddRot.x * invGammaDenom,
                    toAddRot.y * invGammaDenom,
                    (toAddRot.z + origMag) * denom
                );
                float toRetMag = toRet.magnitude;
                if (toRetMag <= maxVel)
                {
                    return toRet;
                }
                else
                {
                    return maxVel / toRetMag * toRet;
                }
            }
        }

        public static Vector4 AddVelocity(this Vector4 orig, Vector4 toAdd)
        {
            Vector3 new3Vel = orig.GetSpatial().AddVelocity(toAdd.GetSpatial());
            return new Vector4(new3Vel.x, new3Vel.y, new3Vel.z, c);
        }


        public static Vector4 ContractLengthBy(this Vector4 interval, Vector4 velocity)
        {
            float sqrMag = velocity.sqrMagnitude;
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
            float invGamma = Mathf.Sqrt(1.0f + sqrMag / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(1.0f / sqrMag * velocity, Vector3.right);
            Vector3 rotInt = rot * new Vector3(interval.x, interval.y, interval.z);
            rotInt = new Vector3(rotInt.x / invGamma, rotInt.y, rotInt.z);
            rotInt = Quaternion.Inverse(rot) * rotInt;
            return new Vector4(rotInt.x, rotInt.y, rotInt.z, interval.w * invGamma);
        }

        public static Vector3 ContractLengthBy(this Vector3 interval, Vector3 velocity)
        {
            if (interval == Vector3.zero)
            {
                return Vector3.zero;
            }
            float sqrMag = velocity.sqrMagnitude;
            if (sqrMag == 0.0f)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f - sqrMag / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(velocity.normalized, Vector3.forward);
            Vector3 rotInt = rot * interval;
            rotInt = new Vector3(rotInt.x, rotInt.y, rotInt.z * invGamma);
            return Quaternion.Inverse(rot) * rotInt;
        }

        public static Vector3 InverseContractLengthBy(this Vector3 interval, Vector3 velocity)
        {
            if (interval == Vector3.zero)
            {
                return Vector3.zero;
            }
            float sqrMag = velocity.sqrMagnitude;
            if (sqrMag == 0.0f)
            {
                return interval;
            }
            float invGamma = Mathf.Sqrt(1.0f - sqrMag / cSqrd);
            Quaternion rot = Quaternion.FromToRotation(velocity.normalized, Vector3.forward);
            Vector3 rotInt = rot * interval;
            rotInt = new Vector3(rotInt.x, rotInt.y, rotInt.z / invGamma);
            return Quaternion.Inverse(rot) * rotInt;
        }

        //This method transforms a real-space position to a Minkowski space-time position.
        // (It uses logic grabbed out of the original relativity.shader.)
        // Note that the inverse of pos = real.RealToPosition(vel) is real = pos.RealToPosition(-vel),
        // since a second Lorentz transform by the velocity in the opposite direction returns to an
        // inertial frame with the same initial velocity.
        public static Vector3 RealToMinkowski(this Vector3 realPos, Vector3 velocity)
        {
            float spdOfLight = SRelativityUtil.c;

            Vector3 vpc = Vector3.zero;
            float speed = 0.0f;
            Vector3 pos = realPos;
            Vector3 viw = velocity / spdOfLight;

            float vuDot = Vector3.Dot(vpc, viw); //Get player velocity dotted with velocity of the object
            Vector3 uparra;

            //IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speed != 0)
            {
                uparra = (vuDot / (speed * speed)) * vpc; //Get the parallel component of the object's velocity
            }
            //If our speed is zero, set parallel velocity to zero
            else
            {
                uparra = Vector3.zero;
            }
            Vector3 uperp = viw - uparra;
            //relative velocity calculation
            Vector3 vr = -1 * (vpc - uparra - (Mathf.Sqrt(1 - speed * speed)) * uperp) / (1 + vuDot);
            //float3 vr = (vpc - uparra - (sqrt(1 - speed*speed))*uperp) / (1 + vuDot);
            //set our relative velocity
            //o.vr = vr;
            //vr *= -1;
            float speedr = vr.magnitude;
            float svc = Mathf.Sqrt(1 - speedr * speedr);

            //riw = location in world, for reference
            Vector3 riw = pos; //Position that will be used in the output

            if (speedr != 0) // If speed is zero, rotation fails
            {
                Quaternion rotFromVPCtoZ = Quaternion.identity;
                if (speed != 0)
                {
                    //we're getting the angle between our z direction of movement and the world's Z axis
                    rotFromVPCtoZ = Quaternion.FromToRotation(vpc.normalized, new Vector3(0.0f, 0.0f, 1.0f));

                    //We're rotating player velocity here, making it seem like the player's movement is all in the Z direction
                    //This is because all of our equations are based off of movement in one direction.

                    //And we rotate our point that much to make it as if our magnitude of velocity is in the Z direction
                    riw = rotFromVPCtoZ * riw;
                }

                //Here begins the original code, made by the guys behind the Relativity game

                //Rotate our velocity
                Vector3 rotateViw = rotFromVPCtoZ * viw * spdOfLight;

                float c = -(riw.sqrMagnitude); //first get position squared (position dotted with position)

                float b = -(2.0f * Vector3.Dot(riw, rotateViw)); //next get position dotted with velocity, should be only in the Z direction

                float d = (spdOfLight * spdOfLight) - rotateViw.sqrMagnitude;

                float tisw = (-b - (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2.0f * d);

                //get the new position offset, based on the new time we just found
                //Should only be in the Z direction

                riw += tisw * rotateViw;

                //Apply Lorentz transform
                //I had to break it up into steps, unity was getting order of operations wrong.	
                float newz = (((float)speed * spdOfLight) * tisw);

                newz = riw.z + newz;
                newz /= Mathf.Sqrt(1 - (speed * speed));
                riw.z = newz;

                riw = Quaternion.Inverse(rotFromVPCtoZ) * riw;
            }

            //riw = riw;// + playerOffset;

            return riw;
        }

        public static float GetGamma(this Vector3 velocity)
        {
            return 1.0f / Mathf.Sqrt(1.0f - velocity.sqrMagnitude / SRelativityUtil.cSqrd);
        }

        public static float GetInverseGamma(this Vector3 velocity)
        {
            return 1.0f / Mathf.Sqrt(1.0f + velocity.sqrMagnitude / SRelativityUtil.cSqrd);
        }

        public static float InverseAccelerateTime(Vector3 accel, Vector3 vel, float deltaT)
        {
            float dilatedTime;
            float invGamma = Mathf.Sqrt(1.0f - vel.sqrMagnitude / c);
            float accelMag = accel.magnitude;
            if (accelMag == 0.0f)
            {
                dilatedTime = deltaT * invGamma;
            }
            else
            {
                float properDeltaTime = Time.deltaTime * invGamma;
                dilatedTime = Mathf.Abs((Mathf.Exp(accelMag * properDeltaTime) - 1.0f) / accelMag) * invGamma;
            }

            return dilatedTime;
        }
        public static float AccelerateTime(Vector3 accel, Vector3 vel, float deltaT)
        {
            if (deltaT == 0.0f) return 0.0f;

            float dilatedTime;
            float gamma = 1.0f / Mathf.Sqrt(1.0f - vel.sqrMagnitude / cSqrd);
            float accelMag = accel.magnitude;
            if (accelMag == 0.0f)
            {
                dilatedTime = deltaT * gamma;
            }
            else
            {
                float arg = Mathf.Abs(accelMag * gamma * deltaT);
                dilatedTime = gamma / accelMag * Mathf.Log(1.0f + arg);
                if (deltaT < 0.0f)
                {
                    dilatedTime = -Mathf.Abs(dilatedTime);
                }
            }

            return dilatedTime;
        }

        public static float LightDelayWithGravity(Vector3 location1, Vector3 location2)
        {
            return LightDelayWithGravity(location1, location2, Vector3.zero);
        }

        public static float LightDelayWithGravity(Vector3 location1, Vector3 location2, Vector3 relativeVelocity)
        {
            return AccelerateTime(srCamera.PlayerAccelerationVector, relativeVelocity, (location1 - location2).magnitude / c);
        }

        public static float LightDelayWithGravity(float deltaT, Vector3 relativeVelocity)
        {
            return AccelerateTime(srCamera.PlayerAccelerationVector, relativeVelocity, deltaT);
        }

        public static float LightDelayWithGravity(float deltaT)
        {
            return AccelerateTime(srCamera.PlayerAccelerationVector, Vector3.zero, deltaT);
        }
    }
}