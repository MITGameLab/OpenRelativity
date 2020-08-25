using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Qrack
{
    public class QuantumManager : MonoBehaviour
    {
        public const string QRACKSIM_DLL_NAME = @"qrack_pinvoke";

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_count")]
        public static extern uint Init(uint numQubits);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "destroy")]
        public static extern void Destroy(uint simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "allocateQubit")]
        public static extern void AllocQubit(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "release")]
        public static extern void Release(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "X")]
        public static extern void X(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Y")]
        public static extern void Y(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Z")]
        public static extern void Z(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "H")]
        public static extern void H(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "S")]
        public static extern void S(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "T")]
        public static extern void T(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AdjS")]
        public static extern void AdjS(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AdjT")]
        public static extern void AdjT(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "U")]
        public static extern void U(uint simId, uint qubitId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "R")]
        public static extern void R(uint simId, uint basis, double phi, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCX")]
        public static extern void MCX(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCY")]
        public static extern void MCY(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCZ")]
        public static extern void MCZ(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCH")]
        public static extern void MCH(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCS")]
        public static extern void MCS(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCT")]
        public static extern void MCT(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCADJS")]
        public static extern void MCADJS(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCADJT")]
        public static extern void MCADJT(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCU")]
        public static extern void MCU(uint simId, uint controlLen, uint[] controls, uint targetId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCR")]
        public static extern void MCR(uint simId, uint basis, double phi, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "M")]
        public static extern uint M(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AND")]
        public static extern uint AND(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "OR")]
        public static extern uint OR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "XOR")]
        public static extern uint XOR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NAND")]
        public static extern uint NAND(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NOR")]
        public static extern uint NOR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "XNOR")]
        public static extern uint XNOR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLAND")]
        public static extern uint CLAND(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLOR")]
        public static extern uint CLOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLXOR")]
        public static extern uint CLXOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLNAND")]
        public static extern uint CLNAND(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLNOR")]
        public static extern uint CLNOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLXNOR")]
        public static extern uint CLXNOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prob")]
        public static extern double Prob(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TimeEvolve")]
        public static extern void TimeEvolve(uint simId, double t, uint n, TimeEvolveOpHeader[] teos, uint mn, double[] mtrx);

        private List<uint> SimulatorIds;

        private void Awake()
        {
            SimulatorIds = new List<uint>();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < SimulatorIds.Count; i++)
            {
                Destroy(SimulatorIds[i]);
            }
        }

        public uint AllocateSimulator(uint numQubits)
        {
            uint simId = Init(numQubits);
            SimulatorIds.Add(simId);
            return simId;
        }

        public void DeallocateSimulator(uint simId)
        {
            if (SimulatorIds.Contains(simId))
            {
                Destroy(simId);
                SimulatorIds.Remove(simId);
            }
        }

        public static void AllocateQubit(uint simId, uint qubitId)
        {
            AllocQubit(simId, qubitId);
        }

        public static void ReleaseQubit(uint simId, uint qubitId)
        {
            Release(simId, qubitId);
        }

        public static void Rand(uint simId, uint target)
        {
            U(simId, target, Random.Range(0, 2 * Mathf.PI), Random.Range(0, 2 * Mathf.PI), Random.Range(0, 2 * Mathf.PI));
        }

        public static void Exp(uint simId, uint target, double phi)
        {
            R(simId, 0, phi, target);
        }

        public static void RX(uint simId, uint target, double phi)
        {
            R(simId, 1, phi, target);
        }

        public static void RY(uint simId, uint target, double phi)
        {
            R(simId, 3, phi, target);
        }

        public static void RZ(uint simId, uint target, double phi)
        {
            R(simId, 2, phi, target);
        }

        public static void MCExp(uint simId, uint controlLen, uint[] controls, uint target, double phi)
        {
            MCR(simId, 0, phi, controlLen, controls, target);
        }

        public static void MCRX(uint simId, uint controlLen, uint[] controls, uint target, double phi)
        {
            MCR(simId, 1, phi, controlLen, controls, target);
        }

        public static void MCRY(uint simId, uint controlLen, uint[] controls, uint target, double phi)
        {
            MCR(simId, 3, phi, controlLen, controls, target);
        }

        public static void MCRZ(uint simId, uint controlLen, uint[] controls, uint target, double phi)
        {
            MCR(simId, 2, phi, controlLen, controls, target);
        }

    }
}
