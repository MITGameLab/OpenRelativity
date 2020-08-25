using System.Runtime.InteropServices;

namespace Qrack
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeEvolveOpHeader
    {
        public uint target;
        public uint controlLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public uint[] controls;

        public TimeEvolveOpHeader(uint t, uint[] c)
        {
            controls = new uint[32];

            target = t;

            if (c == null)
            {
                controlLen = 0;
                return;
            }

            controlLen = (uint)c.Length;
            
            for (int i = 0; i < c.Length; i++)
            {
                controls[i] = c[i];
            }
        }
    }
}