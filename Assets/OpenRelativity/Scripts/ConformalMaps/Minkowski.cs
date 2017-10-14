using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public class Minkowski : ConformalMap
    {
        private Matrix4x4 metric;

        public void Awake() {
            metric = Matrix4x4.identity;
            metric[0, 0] = -1;
            metric[1, 1] = -1;
            metric[2, 2] = -1;
            metric[3, 3] = 1;
        }

        override public Matrix4x4 GetConformalFactor(Vector4 stpiw)
        {
            return Matrix4x4.identity;
        }

        override public Vector3 WorldToOptical(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
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

        override public Vector3 OpticalToWorld(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
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

        override public Vector3 OpticalToWorldHighPrecision(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            Vector3 startPoint = piw;
            Vector3 est = OpticalToWorld(piw, velocity, origin, playerVel);
            Vector3 newEst;
            Vector3 offset = piw - WorldToOptical(est, velocity, origin, playerVel);
            float sqrError = (piw - WorldToOptical(est, velocity, origin, playerVel)).sqrMagnitude;
            float oldSqrError = sqrError + 1.0f;
            float iterations = 1;
            while ((iterations < defaultOpticalToWorldMaxIterations)
                && (sqrError > defaultOpticalToWorldSqrErrorTolerance)
                && (sqrError < oldSqrError))
            {
                iterations++;
                startPoint += offset / 2.0f;
                newEst = OpticalToWorld(startPoint, velocity, origin, playerVel);
                offset = startPoint - WorldToOptical(newEst, velocity, origin, playerVel);
                oldSqrError = sqrError;
                sqrError = (piw - WorldToOptical(newEst, velocity, origin, playerVel)).sqrMagnitude;
                if (sqrError < oldSqrError)
                {
                    est = newEst;
                }
            }

            return est;
        }
    }
}
