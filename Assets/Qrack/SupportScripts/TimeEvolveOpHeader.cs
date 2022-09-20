using System.Runtime.InteropServices;

namespace Qrack
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeEvolveOpHeader
    {
        public ulong target;
        public ulong controlLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public ulong[] controls;

        public TimeEvolveOpHeader(ulong t, ulong[] c)
        {
            controls = new ulong[32];

            target = t;

            if (c == null)
            {
                controlLen = 0;
                return;
            }

            controlLen = (ulong)c.Length;
            
            for (int i = 0; i < c.Length; ++i)
            {
                controls[i] = c[i];
            }
        }
    }
}