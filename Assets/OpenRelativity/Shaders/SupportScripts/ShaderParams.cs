using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 360)]
    public struct ShaderParams
    {
        //[FieldOffset(0)]
        public Matrix4x4 ltwMatrix; //local to world matrix of transform
        //[FieldOffset(16)]
        public Matrix4x4 wtlMatrix; //world to local matrix of transform
        //[FieldOffset(32)]
        public Vector4 viw; //velocity of object in world
        //[FieldOffset(36)]
        //[FieldOffset(40)]
        public Vector4 vpc; //velocity of player
        //[FieldOffset(44)]
        public Vector4 playerOffset; //player position in world
        //[FieldOffset(48)]
        public float speed; //speed of player;
        //[FieldOffset(49)]
        public float spdOfLight; //current speed of light
        //[FieldOffset(50)]
        public Vector4 pap;
        //[FieldOffset(66)]
        public Vector4 avp;
        //[FieldOffset(82)]
        public Vector4 aiw;
        //[FieldOffset(98)]
        public Matrix4x4 vpcLorentzMatrix;
        //[FieldOffset(162)]
        public Matrix4x4 viwLorentzMatrix;
    }
}
