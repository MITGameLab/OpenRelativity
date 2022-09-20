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

            double x = 2 * ((1.0 / 2) - probX);
            double y = 2 * ((1.0 / 2) - probY);
            double z = 2 * ((1.0 / 2) - probZ);

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
        public static extern int GetError(ulong simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_count_type")]
        public static extern ulong InitType(ulong numQubits, bool decomposeMulti, bool decompose, bool stabilizer, bool bdt, bool pager, bool fusion, bool hybrid, bool opencl, bool hostPointer);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_count")]
        public static extern ulong Init(ulong numQubits, bool hostPointer);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_clone")]
        public static extern ulong Clone(ulong simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "destroy")]
        public static extern void Destroy(ulong simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "allocateQubit")]
        public static extern void AllocQubit(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "release")]
        public static extern void Release(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "X")]
        public static extern void X(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Y")]
        public static extern void Y(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Z")]
        public static extern void Z(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "H")]
        public static extern void H(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "S")]
        public static extern void S(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "T")]
        public static extern void T(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AdjS")]
        public static extern void AdjS(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AdjT")]
        public static extern void AdjT(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "U")]
        public static extern void U(ulong simId, ulong qubitId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Mtrx")]
        public static extern void Mtrx(ulong simId, double[] m, ulong q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "R")]
        public static extern void R(ulong simId, ulong basis, double phi, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCX")]
        public static extern void MCX(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCY")]
        public static extern void MCY(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCZ")]
        public static extern void MCZ(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCH")]
        public static extern void MCH(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCS")]
        public static extern void MCS(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCT")]
        public static extern void MCT(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCAdjS")]
        public static extern void MCAdjS(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCAdjT")]
        public static extern void MCAdjT(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCU")]
        public static extern void MCU(ulong simId, ulong controlLen, ulong[] controls, ulong targetId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCMtrx")]
        public static extern void MCMtrx(ulong simId, ulong controlLen, ulong[] controls, double[] m, ulong q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACX")]
        public static extern void MACX(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACY")]
        public static extern void MACY(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACZ")]
        public static extern void MACZ(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACH")]
        public static extern void MACH(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACS")]
        public static extern void MACS(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACT")]
        public static extern void MACT(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACAdjS")]
        public static extern void MACAdjS(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACAdjT")]
        public static extern void MACAdjT(ulong simId, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACU")]
        public static extern void MACU(ulong simId, ulong controlLen, ulong[] controls, ulong targetId, double theta, double phi, double lambda);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MACMtrx")]
        public static extern void MACMtrx(ulong simId, ulong controlLen, ulong[] controls, double[] m, ulong q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MX")]
        public static extern void MX(ulong simId, ulong targetLen, ulong[] targets);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MY")]
        public static extern void MY(ulong simId, ulong targetLen, ulong[] targets);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MZ")]
        public static extern void MZ(ulong simId, ulong targetLen, ulong[] targets);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MCR")]
        public static extern void MCR(ulong simId, ulong basis, double phi, ulong controlLen, ulong[] controls, ulong targetId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SWAP")]
        public static extern void SWAP(ulong simId, ulong target1, ulong target2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ISWAP")]
        public static extern void ISWAP(ulong simId, ulong target1, ulong target2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AdjISWAP")]
        public static extern void AdjISWAP(ulong simId, ulong target1, ulong target2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FSim")]
        public static extern void FSim(ulong simId, double theta, double phi, ulong target1, ulong target2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CSWAP")]
        public static extern void CSWAP(ulong simId, ulong n, ulong[] c, ulong target1, ulong target2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CSWAP")]
        public static extern void ACSWAP(ulong simId, ulong n, ulong[] c, ulong target1, ulong target2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "M")]
        public static extern ulong M(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MAll")]
        public static extern ulong MAll(ulong simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Measure")]
        public static extern ulong Measure(ulong simId, ulong numQubits, ulong[] bases, ulong[] qubits);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "MeasureShots")]
        public static extern ulong MeasureShots(ulong simId, ulong numQubits, ulong[] qubits, ulong shots, ulong[] measureResults);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AND")]
        public static extern void AND(ulong simId, ulong qi1, ulong qi2, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "OR")]
        public static extern void OR(ulong simId, ulong qi1, ulong qi2, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "XOR")]
        public static extern void XOR(ulong simId, ulong qi1, ulong qi2, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NAND")]
        public static extern void NAND(ulong simId, ulong qi1, ulong qi2, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NOR")]
        public static extern void NOR(ulong simId, ulong qi1, ulong qi2, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "XNOR")]
        public static extern void XNOR(ulong simId, ulong qi1, ulong qi2, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLAND")]
        public static extern void CLAND(ulong simId, bool ci, ulong qi, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLOR")]
        public static extern void CLOR(ulong simId, bool ci, ulong qi, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLXOR")]
        public static extern void CLXOR(ulong simId, bool ci, ulong qi, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLNAND")]
        public static extern void CLNAND(ulong simId, bool ci, ulong qi, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLNOR")]
        public static extern void CLNOR(ulong simId, bool ci, ulong qi, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CLXNOR")]
        public static extern void CLXNOR(ulong simId, bool ci, ulong qi, ulong qo);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prob")]
        public static extern double Prob(ulong simId, ulong qubitId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PermutationExpectation")]
        public static extern double PermutationExpectation(ulong simId, ulong n, ulong[] c);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ResetAll")]
        public static extern void ResetAll(ulong simId);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TrySeparate1Qb")]
        public static extern bool TrySeparate1Qb(ulong simId, ulong q);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TrySeparate2Qb")]
        public static extern bool TrySeparate2Qb(ulong simId, ulong q1, ulong q2);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TrySeparateTol")]
        public static extern bool TrySeparateTol(ulong simId, ulong n, ulong[] q, double error_tol);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetReactiveSeparate")]
        public static extern void SetReactiveSeparate(ulong simId, bool irs);

        [DllImport(QRACKSIM_DLL_NAME, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, EntryPoint = "TimeEvolve")]
        public static extern void TimeEvolve(ulong simId, double t, ulong n, TimeEvolveOpHeader[] teos, ulong mn, double[] mtrx);

        private List<ulong> SimulatorIds = new List<ulong>();

        private void OnDestroy()
        {
            for (int i = 0; i < SimulatorIds.Count; ++i)
            {
                Destroy(SimulatorIds[i]);
            }
        }

        public ulong AllocateSimulator(ulong numQubits)
        {
            // ulong simId = Init(numQubits, false);
            ulong simId = InitType(numQubits, false, false, false, false, false, false, false, false, false);
            SimulatorIds.Add(simId);
            return simId;
        }

        public ulong CloneSimulator(ulong simId)
        {
            ulong nSimId = Clone(simId);
            SimulatorIds.Add(nSimId);
            return nSimId;
        }

        public void DeallocateSimulator(ulong simId)
        {
            if (SimulatorIds.Contains(simId))
            {
                Destroy(simId);
                SimulatorIds.Remove(simId);
            }
        }

        public static void AllocateQubit(ulong simId, ulong qubitId)
        {
            AllocQubit(simId, qubitId);
        }

        public static void ReleaseQubit(ulong simId, ulong qubitId)
        {
            Release(simId, qubitId);
        }

        public static void Rand(ulong simId, ulong target)
        {
            U(simId, target, UnityEngine.Random.Range(0, 4 * Mathf.PI), UnityEngine.Random.Range(0, 4 * Mathf.PI), UnityEngine.Random.Range(0, 4 * Mathf.PI));
        }

        public static void Exp(ulong simId, ulong target, double phi)
        {
            R(simId, 0, phi, target);
        }

        public static void RX(ulong simId, ulong target, double phi)
        {
            R(simId, 1, phi, target);
        }

        public static void RY(ulong simId, ulong target, double phi)
        {
            R(simId, 3, phi, target);
        }

        public static void RZ(ulong simId, ulong target, double phi)
        {
            R(simId, 2, phi, target);
        }

        public static void MCExp(ulong simId, ulong controlLen, ulong[] controls, ulong target, double phi)
        {
            MCR(simId, 0, phi, controlLen, controls, target);
        }

        public static void MCRX(ulong simId, ulong controlLen, ulong[] controls, ulong target, double phi)
        {
            MCR(simId, 1, phi, controlLen, controls, target);
        }

        public static void MCRY(ulong simId, ulong controlLen, ulong[] controls, ulong target, double phi)
        {
            MCR(simId, 3, phi, controlLen, controls, target);
        }

        public static void MCRZ(ulong simId, ulong controlLen, ulong[] controls, ulong target, double phi)
        {
            MCR(simId, 2, phi, controlLen, controls, target);
        }

        public static bool TrySeparate(ulong simId, ulong q)
        {
            return TrySeparate1Qb(simId, q);
        }

        public static bool TrySeparate(ulong simId, ulong q1, ulong q2)
        {
            return TrySeparate2Qb(simId, q1, q2);
        }

        public static bool TrySeparate(ulong simId, ulong n, ulong[] q, double error_tol)
        {
            return TrySeparateTol(simId, n, q, error_tol);
        }

        public static ulong Measure(ulong simId, ulong[] bases, ulong[] qubits)
        {
            return Measure(simId, (ulong)bases.Length, bases, qubits);
        }

        public static void MeasureShots(ulong simId, ulong[] qubits, ulong[] measureResults)
        {
            MeasureShots(simId, (ulong)qubits.Length, qubits, (ulong)measureResults.Length, measureResults);
        }

        public static BlochSphereCoordinates Prob3Axis(ulong simId, ulong target)
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
