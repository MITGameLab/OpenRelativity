using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace Qrack
{
    public class BlochSphereCoordinates
    {
        public double probX { get; private set; }
        public double probY { get; private set; }
        public double probZ { get; private set; }

        public double r { get; private set; }
        public double azimuth { get; private set; }
        public double inclination { get; private set; }

        public BlochSphereCoordinates(double px, double py, double pz)
        {
            probX = px;
            probY = py;
            probZ = pz;

            double x = 2 * ((1.0 / 2.0) - probX);
            double y = 2 * ((1.0 / 2.0) - probY);
            double z = 2 * ((1.0 / 2.0) - probZ);

            r = Math.Sqrt(x * x + y * y + z * z);
            inclination = Math.Atan2(Math.Sqrt(x * x + y * y), z);
            azimuth = Math.Atan2(y, x);
        }
    }

    public class QuantumManager : MonoBehaviour
    {
#if USE_SYSTEM_QRACK_INSTALL
#if _LINUX
        public const string QRACKSIM_DLL_NAME = @"/usr/local/lib/libqrack_pinvoke.so";
#elif _DARWIN
        public const string QRACKSIM_DLL_NAME = @"/usr/local/lib/libqrack_pinvoke.dylib";
#else
        public const string QRACKSIM_DLL_NAME = @"C:\\Program Files\\Qrack\\bin\\qrack_pinvoke.dll";
#endif
#else
        public const string QRACKSIM_DLL_NAME = @"qrack_pinvoke";
#endif

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_error")]
        public static extern uint GetError(uint simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_count")]
        public static extern uint Init(uint numQubits, bool hostPointer);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_count_type")]
        public static extern uint InitType(uint numQubits, bool decomposeMulti, bool decompose, bool stabilizer, bool bdt, bool pager, bool fusion, bool hybrid, bool opencl, bool hostPointer);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_clone")]
        public static extern uint Clone(uint simId);

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

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mtrx")]
        public static extern void Mtrx(uint simId, double[] m, uint q);

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

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCAdjS")]
        public static extern void MCAdjS(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCAdjT")]
        public static extern void MCAdjT(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCU")]
        public static extern void MCU(uint simId, uint controlLen, uint[] controls, uint targetId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCMtrx")]
        public static extern void MCMtrx(uint simId, uint controlLen, uint[] controls, double[] m, uint q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACX")]
        public static extern void MACX(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACY")]
        public static extern void MACY(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACZ")]
        public static extern void MACZ(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACH")]
        public static extern void MACH(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACS")]
        public static extern void MACS(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACT")]
        public static extern void MACT(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACAdjS")]
        public static extern void MACAdjS(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACAdjT")]
        public static extern void MACAdjT(uint simId, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACU")]
        public static extern void MACU(uint simId, uint controlLen, uint[] controls, uint targetId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACMtrx")]
        public static extern void MACMtrx(uint simId, uint controlLen, uint[] controls, double[] m, uint q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCR")]
        public static extern void MCR(uint simId, uint basis, double phi, uint controlLen, uint[] controls, uint targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "M")]
        public static extern uint M(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MAll")]
        public static extern uint MAll(uint simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Measure")]
        public static extern uint Measure(uint simId, uint numQubits, uint[] bases, uint[] qubits);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MeasureShots")]
        public static extern uint MeasureShots(uint simId, uint numQubits, uint[] qubits, uint shots, uint[] measureResults);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AND")]
        public static extern void AND(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "OR")]
        public static extern void OR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "XOR")]
        public static extern void XOR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NAND")]
        public static extern void NAND(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NOR")]
        public static extern void NOR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "XNOR")]
        public static extern void XNOR(uint simId, uint qi1, uint qi2, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLAND")]
        public static extern void CLAND(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLOR")]
        public static extern void CLOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLXOR")]
        public static extern void CLXOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLNAND")]
        public static extern void CLNAND(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLNOR")]
        public static extern void CLNOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLXNOR")]
        public static extern void CLXNOR(uint simId, bool ci, uint qi, uint qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prob")]
        public static extern double Prob(uint simId, uint qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PermutationExpectation")]
        public static extern double PermutationExpectation(uint simId, uint n, uint[] c);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ResetAll")]
        public static extern void ResetAll(uint simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TrySeparate1Qb")]
        public static extern bool TrySeparate1Qb(uint simId, uint q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TrySeparate2Qb")]
        public static extern bool TrySeparate2Qb(uint simId, uint q1, uint q2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TrySeparateTol")]
        public static extern bool TrySeparateTol(uint simId, uint n, uint[] q, double error_tol);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetReactiveSeparate")]
        public static extern void SetReactiveSeparate(uint simId, bool irs);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TimeEvolve")]
        public static extern void TimeEvolve(uint simId, double t, uint n, TimeEvolveOpHeader[] teos, uint mn, double[] mtrx);

        private List<uint> SimulatorIds = new List<uint>();

        private void OnDestroy()
        {
            for (int i = 0; i < SimulatorIds.Count; i++)
            {
                Destroy(SimulatorIds[i]);
            }
        }

        public uint AllocateSimulator(uint numQubits)
        {
            uint simId = Init(numQubits, false);
            SimulatorIds.Add(simId);
            return simId;
        }

        public uint CloneSimulator(uint simId)
        {
            uint nSimId = Clone(simId);
            SimulatorIds.Add(nSimId);
            return nSimId;
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
            U(simId, target, UnityEngine.Random.Range(0, 4 * Mathf.PI), UnityEngine.Random.Range(0, 4 * Mathf.PI), UnityEngine.Random.Range(0, 4 * Mathf.PI));
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

        public static bool TrySeparate(uint simId, uint q)
        {
            return TrySeparate1Qb(simId, q);
        }

        public static bool TrySeparate(uint simId, uint q1, uint q2)
        {
            return TrySeparate2Qb(simId, q1, q2);
        }

        public static bool TrySeparate(uint simId, uint n, uint[] q, double error_tol)
        {
            return TrySeparateTol(simId, n, q, error_tol);
        }

        public static uint Measure(uint simId, uint[] bases, uint[] qubits)
        {
            return Measure(simId, (uint)bases.Length, bases, qubits);
        }

        public static void MeasureShots(uint simId, uint[] qubits, uint[] measureResults)
        {
            MeasureShots(simId, (uint)qubits.Length, qubits, (uint)measureResults.Length, measureResults);
        }

        public static BlochSphereCoordinates Prob3Axis(uint simId, uint target)
        {
            double probZ = Prob(simId, target);
            H(simId, target);
            double probX = Prob(simId, target);
            S(simId, target);
            H(simId, target);
            double probY = Prob(simId, target);
            H(simId, target);
            AdjS(simId, target);
            H(simId, target);

            return new BlochSphereCoordinates(probX, probY, probZ);
        }
    }
}
