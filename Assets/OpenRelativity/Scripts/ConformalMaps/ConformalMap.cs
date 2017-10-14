using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenRelativity.ConformalMaps
{
    public abstract class ConformalMap : MonoBehaviour
    {
        public const int defaultOpticalToWorldMaxIterations = 5;
        public const float defaultOpticalToWorldSqrErrorTolerance = 0.0001f;
        public const float divByZeroCutoff = 1e-8f;

        abstract public Matrix4x4 GetConformalFactor(Vector4 piw);

        //This method converts the position of an object in the world to its position after the shader is applied.
        abstract public Vector3 WorldToOptical(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel);
        public Vector3 WorldToOptical(Vector3 realPos, Vector3 velocity)
        {
            return WorldToOptical(realPos, velocity, Vector3.zero, Vector3.zero);
        }
        public Vector3 WorldToOptical(Vector3 realPos, Vector3 velocity, Vector3 origin)
        {
            return WorldToOptical(realPos, velocity, origin, Vector3.zero);
        }

        //This method converts the position of an object after the shader is applied to its position in world coordinates.
        abstract public Vector3 OpticalToWorld(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel);
        virtual public Vector3 OpticalToWorldHighPrecision(Vector3 piw, Vector3 velocity, Vector3 origin, Vector3 playerVel)
        {
            return OpticalToWorld(piw, velocity, origin, playerVel);
        }
        public Vector3 OpticalToWorld(Vector3 realPos, Vector3 velocity)
        {
            return OpticalToWorld(realPos, velocity, Vector3.zero, Vector3.zero);
        }
        public Vector3 OpticalToWorld(Vector3 realPos, Vector3 velocity, Vector3 origin)
        {
            return OpticalToWorld(realPos, velocity, origin, Vector3.zero);
        }
    }
}
