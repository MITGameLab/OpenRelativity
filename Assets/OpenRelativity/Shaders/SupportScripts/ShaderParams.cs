using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 612)]
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
        public Matrix4x4 intrinsicMetric;
        //[FieldOffset(80)]
        public Matrix4x4 invVpcLorentzMatrix;
        //[FieldOffset(96)]
        public Matrix4x4 invViwLorentzMatrix;
        //[FieldOffset(112)]
        public Matrix4x4 invIntrinsicMetric;
        //[FieldOffset(128)]
        public Vector4 viw; //velocity of object in world
        //[FieldOffset(132)]
        public Vector4 vpc; //velocity of player
        //[FieldOffset(136)]
        public Vector4 playerOffset; //player position in world
        //[FieldOffset(140)]
        public Vector4 pap;
        //[FieldOffset(144)]
        public Vector4 avp;
        //[FieldOffset(148)]
        public Vector4 pao;
        //[FieldOffset(152)]
        public float spdOfLight; //current speed of light
        //[FieldOffset(153)]
    }
}
