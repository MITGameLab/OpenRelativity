using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 52)]
    public struct ShaderParams
    {
        //[FieldOffset(0)]
        public Matrix4x4 ltwMatrix; //local to world matrix of transform
        //[FieldOffset(16)]
        public Matrix4x4 wtlMatrix; //world to local matrix of transform
        //[FieldOffset(32)]
        public Vector3 piw; //velocity of object in world
        //[FieldOffset(35)]
        public Vector3 viw; //velocity of object in world
        //[FieldOffset(38)]
        public Vector3 aviw; //angular velocity of object
        //[FieldOffset(41)]
        public Vector3 vpc; //velocity of player
        //[FieldOffset(44)]
        public Vector3 playerOffset; //player position in world
        //[FieldOffset(47)]
        //public float gtt; //metric tensor 00 component
        //[FieldOffset(48)]
        public float speed; //speed of player;
        //[FieldOffset(49)]
        public float spdOfLight; //current speed of light
        //[FieldOffset(50)]
        public float wrldTime; //current time in world
        //[FieldOffset(51)]
        public float strtTime; //starting time in world
    }
}
