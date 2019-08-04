using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 436)]
    public struct ShaderParams
    {
        //[FieldOffset(0)]
        public Matrix4x4 ltwMatrix; //local to world matrix of transform
        //[FieldOffset(16)]
        public Matrix4x4 wtlMatrix; //world to local matrix of transform
        //[FieldOffset(32)]
        public Vector4 viw; //velocity of object in world
        //[FieldOffset(36)]
        public Vector4 vpc; //velocity of player
        //[FieldOffset(40)]
        public Vector4 playerOffset; //player position in world
        //[FieldOffset(44)]
        public float spdOfLight; //current speed of light
        //[FieldOffset(45)]
        public Vector4 pap;
        //[FieldOffset(49)]
        public Vector4 avp;
        //[FieldOffset(51)]
        public Vector4 aiw;
        //[FieldOffset(55)]
        public Matrix4x4 vpcLorentzMatrix;
        //[FieldOffset(71)]
        public Matrix4x4 viwLorentzMatrix;
        //[FieldOffset(87)]
        public Matrix4x4 invVpcLorentzMatrix;
        //[FieldOffset(93)]
        public Matrix4x4 invViwLorentzMatrix;
        //[FieldOffset(109)]
    }
}
