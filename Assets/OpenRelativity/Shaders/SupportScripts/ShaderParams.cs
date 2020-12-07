using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 484)]
    public struct ShaderParams
    {
        //[FieldOffset(0)]
        public Matrix4x4 ltwMatrix; //local to world matrix of transform
        //[FieldOffset(16)]
        public Matrix4x4 wtlMatrix; //world to local matrix of transform
        //[FieldOffset(32)]
        public Matrix4x4 vpcLorentzMatrix;
        //[FieldOffset(48)]
        public Matrix4x4 viwLorentzMatrix;
        //[FieldOffset(64)]
        public Matrix4x4 invVpcLorentzMatrix;
        //[FieldOffset(80)]
        public Matrix4x4 invViwLorentzMatrix;
        //[FieldOffset(96)]
        public Vector4 viw; //velocity of object in world
        //[FieldOffset(100)]
        public Vector4 vpc; //velocity of player
        //[FieldOffset(104)]
        public Vector4 playerOffset; //player position in world
        //[FieldOffset(108)]
        public Vector4 pap;
        //[FieldOffset(112)]
        public Vector4 avp;
        //[FieldOffset(116)]
        public Vector4 aiw;
        //[FieldOffset(120)]
        public float spdOfLight; //current speed of light
        //[FieldOffset(121)]
    }
}
