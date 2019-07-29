using System.Runtime.InteropServices;
using UnityEngine;

namespace OpenRelativity
{
    //Shader properties:
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 352)]
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
        public float speed; //speed of player;
        //[FieldOffset(45)]
        public float spdOfLight; //current speed of light
        //[FieldOffset(46)]
        public Vector4 pap;
        //[FieldOffset(50)]
        public Vector4 avp;
        //[FieldOffset(52)]
        public Vector4 aiw;
        //[FieldOffset(56)]
        public Matrix4x4 vpcLorentzMatrix;
        //[FieldOffset(72)]
        public Matrix4x4 viwLorentzMatrix;
        //[FieldOffset(88)]
    }
}
