using System;
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
            float speed = Mathf.Sqrt(Vector3.Dot(vpc, vpc)); // (float)srCamera.playerVelocity;
            Vector3 pos = piw - origin;
            Vector3 viw = velocity / spdOfLight;

            float vuDot = Vector3.Dot(vpc, viw); //Get player velocity dotted with velocity of the object.
            Vector3 uparra;
            //IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speed > divByZeroCutoff)
            {
                uparra = (vuDot / (speed * speed)) * vpc; //Get the parallel component of the object's velocity
            }
            //If our speed is nearly zero, it could lead to infinities. We insert the limit point.
            else
            {
                speed = 0;
                uparra = viw;
            }
            //Get the perpendicular component of our velocity, just by subtraction
            Vector3 uperp = viw - uparra;
            //relative velocity calculation
            Vector3 vr = -(vpc - uparra - (Mathf.Sqrt(1 - speed * speed)) * uperp) / (1 + vuDot);
            float speedr = vr.magnitude;

            //riw = location in world, for reference
            Vector3 riw = pos; //Position that will be used in the output

            if (speedr != 0) // If speed is zero, rotation fails
            {
                Quaternion rotFromVPCtoZ = Quaternion.identity;
                if (speed != 0)
                {
                    //We're getting the angle between our z direction of movement and the world's Z axis
                    Vector3 direction = vpc.normalized;
                    // If the velocity is almost entirely in the z direction already, this is unnecessary and will fail.
                    float a = (-direction.z / speed);
                    if (Mathf.Abs(a) > divByZeroCutoff && Mathf.Abs(a) > 1.0f / divByZeroCutoff)
                    {
                        a = -Mathf.Acos(-a);
                        //This evaluates to false for infinite, NaN, and uselessly large and small angles:
                        if (Mathf.Abs(a) > divByZeroCutoff && Mathf.Abs(a) > 1.0f / divByZeroCutoff)
                        {
                            //we're getting the angle between our z direction of movement and the world's Z axis
                            rotFromVPCtoZ = Quaternion.FromToRotation(direction, new Vector3(0.0f, 0.0f, 1.0f));

                            //We're rotating player velocity here, making it seem like the player's movement is all in the Z direction
                            //This is because all of our equations are based off of movement in one direction.

                            //And we rotate our point that much to make it as if our magnitude of velocity is in the Z direction
                            riw = rotFromVPCtoZ * riw;
                        }
                    }
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

            riw += origin;

            return riw;
        }

        const int defaultOpticalToWorldMaxIterations = 5;
        const float defaultOpticalToWorldSqrErrorTolerance = 0.0001f;

        public static Vector3 OpticalToWorldSearch(this Vector3 oPos, Vector3 velocity, Vector3 origin, Vector3 playerVel, Vector3 initPIWestimate, float maxIterations = defaultOpticalToWorldMaxIterations, float sqrErrorTolerance = defaultOpticalToWorldSqrErrorTolerance)
        {
            //The exact inverse of WorldToOptical is either really complicated or transcendental (i.e., can't be solved for).
            // However, we can approximate the inverse numerically.
            // Turns out from trial and error that a good initial estimate for a round trip to and from optical position on a velocity change is:
            // position.WorldToOptical(oldViw, state.PlayerTransform.position).WorldToOptical(-newViw, state.PlayerTransform.position)
            // (That is, this neglects player velocity, using only the object's velocity and player's position. Notice the minus sign on newViw.)
            // You can use this to seed the search.

            //High level, more expensive implementation:
            //***************************************************************************************************************************//

            //Vector3 estimate = initPIWestimate;
            //Vector3 newEst = estimate;
            //Vector3 newOPos = estimate.WorldToOptical(velocity, origin, playerVel);
            //float newSqrError = (oPos - newOPos).sqrMagnitude;
            //float sqrError;
            //int iterationCount = 0;
            //do
            //{
            //    estimate = newEst;
            //    sqrError = newSqrError;
            //    newEst = estimate + (oPos - newOPos);
            //    newOPos = newEst.WorldToOptical(velocity, origin, playerVel);
            //    newSqrError = (oPos - newOPos).sqrMagnitude;
            //    iterationCount++;
            //} while (newSqrError < sqrError && newSqrError > sqrErrorTolerance && iterationCount < defaultOpticalToWorldMaxIterations);

            //if (newSqrError < sqrError)
            //{
            //    estimate = newEst;
            //}

            //return estimate;

            //***************************************************************************************************************************//

            //Low level, less expensive implementation:

            Vector3 estimate = initPIWestimate;
            Vector3 newEst = estimate;
            Vector3 newOPos = estimate.WorldToOptical(velocity, origin, playerVel);

            float spdOfLight = SRelativityUtil.c;

            Vector3 vpc = -playerVel / spdOfLight;// srCamera.PlayerVelocityVector;
            float speed = playerVel.magnitude / spdOfLight; // (float)srCamera.playerVelocity;
            Vector3 viw = velocity / spdOfLight;

            float vuDot = Vector3.Dot(vpc, viw); //Get player velocity dotted with velocity of the object.
            Vector3 uparra;
            //IF our speed is zero, this parallel velocity component will be NaN, so we have a check here just to be safe
            if (speed > divByZeroCutoff)
            {
                uparra = (vuDot / (speed * speed)) * vpc; //Get the parallel component of the object's velocity
            }
            //If our speed is nearly zero, set it could lead to infinities, so treat is as exactly zero, and set parallel velocity to zero
            else
            {
                speed = 0;
                uparra = viw;
            }
            //Get the perpendicular component of our velocity, just by subtraction
            Vector3 uperp = viw - uparra;
            //relative velocity calculation
            Vector3 vr = -(vpc - uparra - (Mathf.Sqrt(1 - speed * speed)) * uperp) / (1 + vuDot);
            float speedr = vr.magnitude;

            Quaternion rotFromVPCtoZ = Quaternion.identity;
            if (speedr == 0) // If the relative speed is zero, the optical position is equal to the world position.
            {
                estimate = oPos;
            }
            else
            {
                if (speed != 0)
                {
                    //We're getting the angle between our z direction of movement and the world's Z axis
                    Vector3 direction = vpc.normalized;
                    // If the velocity is almost entirely in the z direction already, this is unnecessary and will fail.
                    float a = (-direction.z / speed);
                    if (Mathf.Abs(a) > divByZeroCutoff && Mathf.Abs(a) > 1.0f / divByZeroCutoff)
                    {
                        a = -Mathf.Acos(-a);
                        //This evaluates to false for infinite, NaN, and uselessly large and small angles:
                        if (Mathf.Abs(a) > divByZeroCutoff && Mathf.Abs(a) > 1.0f / divByZeroCutoff)
                        {
                            rotFromVPCtoZ = Quaternion.FromToRotation(direction, new Vector3(0.0f, 0.0f, 1.0f));
                        }
                        //We're rotating player velocity here, making it seem like the player's movement is all in the Z direction
                        //This is because all of our equations are based off of movement in one direction.
                    }
                }

                Vector3 riw; //Position that will be used in the output

                float newSqrError = (oPos - newOPos).sqrMagnitude;
                float sqrError;
                int iterationCount = 0;
                float c, b, d, tisw, newz;
                Vector3 rotateViw;
                do
                {
                    estimate = newEst;
                    sqrError = newSqrError;
                    newEst = estimate + (oPos - newOPos);

                    riw = rotFromVPCtoZ * (newEst - origin);

                    rotateViw = rotFromVPCtoZ * viw * spdOfLight;

                    c = -(riw.sqrMagnitude); //first get position squared (position dotted with position)

                    b = -(2.0f * Vector3.Dot(riw, rotateViw)); //next get position dotted with velocity, should be only in the Z direction

                    d = (spdOfLight * spdOfLight) - rotateViw.sqrMagnitude;

                    tisw = (-b - (Mathf.Sqrt((b * b) - 4.0f * d * c))) / (2.0f * d);

                    //get the new position offset, based on the new time we just found
                    //Should only be in the Z direction

                    riw += tisw * rotateViw;

                    //Apply Lorentz transform
                    //I had to break it up into steps, unity was getting order of operations wrong.	
                    newz = (((float)speed * spdOfLight) * tisw);

                    newz = riw.z + newz;
                    newz /= Mathf.Sqrt(1 - (speed * speed));
                    riw.z = newz;

                    riw = Quaternion.Inverse(rotFromVPCtoZ) * riw;

                    riw += origin;

                    newOPos = riw;
                    newSqrError = (oPos - newOPos).sqrMagnitude;
                    iterationCount++;
                } while (newSqrError < sqrError && newSqrError > sqrErrorTolerance && iterationCount < defaultOpticalToWorldMaxIterations);

                if (newSqrError < sqrError)
                {
                    estimate = newEst;
                }
            }

            return estimate;
        }

        public static float Gamma(this Vector3 velocity)
        {
            return 1.0f / Mathf.Sqrt(1.0f - velocity.sqrMagnitude / SRelativityUtil.cSqrd);
        }

        public static float InverseGamma(this Vector3 velocity)
        {
            return 1.0f / Mathf.Sqrt(1.0f + velocity.sqrMagnitude / SRelativityUtil.cSqrd);
        }

        public static Vector3 RapidityToVelocity(this Vector3 rapidity)
        {
            float mag = rapidity.magnitude;
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
    }
}