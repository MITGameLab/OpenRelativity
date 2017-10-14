using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 240)]
    public struct ShaderParams
    {
        //[FieldOffset(0)]
        public Matrix4x4 ltwMatrix; //local to world matrix of transform
        //[FieldOffset(16)]
        public Matrix4x4 wtlMatrix; //world to local matrix of transform
        //[FieldOffset(32)]
        //public Vector3 piw; //velocity of object in world
        //[FieldOffset(35)]
        public Vector4 viw; //velocity of object in world
        //[FieldOffset(38)]
        //public Vector3 aviw; //angular velocity of object
        //[FieldOffset(41)]
        public Vector3 vpc; //velocity of player
        //[FieldOffset(44)]
        public Vector3 playerOffset; //player position in world
        //[FieldOffset(47)]
        public System.Single speed; //speed of player;
        //[FieldOffset(48)]
        public System.Single spdOfLight; //current speed of light
        //[FieldOffset(49)]
        //public System.Single wrldTime; //current time in world
        //[FieldOffset(50)]
        //public System.Single strtTime; //starting time in world
        public Matrix4x4 metric;
    }
}
