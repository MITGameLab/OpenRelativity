#if OPEN_RELATIVITY_INCLUDED
using OpenRelativity;
using OpenRelativity.Objects;
using System;
#endif

using System.Collections.Generic;
using UnityEngine;

namespace Qrack
{
#if OPEN_RELATIVITY_INCLUDED
    public class QuantumSystem : RelativisticBehavior
#else
    public class QuantumSystem : MonoBehaviour
#endif
    {

        public ulong QubitCount = 1;
        public float ClockOffset;

        public ulong SystemId { get; set; }

        protected ulong lastQubitCount;

        private QuantumManager _qMan = null;

        private QuantumManager qMan
        {
            get
            {
                if (_qMan == null)
                {
                    _qMan = FindObjectOfType<QuantumManager>();
                }

                return _qMan;
            }
        }

        virtual protected ulong GetSystemIndex(ulong registerIndex)
        {
            return registerIndex;
        }

#if OPEN_RELATIVITY_INCLUDED
        private RelativisticObject _myRO;

        private RelativisticObject myRO
        {
            get
            {
                return _myRO ? _myRO : _myRO = GetComponent<RelativisticObject>();
            }
        }
#endif

        public float LocalTime
        {
            get
            {
#if OPEN_RELATIVITY_INCLUDED
                return ClockOffset + (myRO ? myRO.GetLocalTime() : state.TotalTimeWorld);
#else
                return ClockOffset + Time.time;
#endif
            }
        }

        public float LocalDeltaTime
        {
            get
            {
#if OPEN_RELATIVITY_INCLUDED
                return (float)(myRO ? myRO.localDeltaTime : state.DeltaTimeWorld);
#else
                return Time.deltaTime;
#endif
            }
        }

        public float LocalFixedDeltaTime
        {
            get
            {
#if OPEN_RELATIVITY_INCLUDED
                return (float)(myRO ? myRO.localFixedDeltaTime : state.FixedDeltaTimeWorld);
#else
                return Time.fixedDeltaTime;
#endif
            }
        }

        public float VisualTime
        {
            get
            {
#if OPEN_RELATIVITY_INCLUDED
                return ClockOffset + (myRO ? myRO.GetVisualTime() : state.TotalTimeWorld);
#else
                return ClockOffset + Time.time;
#endif
            }
        }

        public void AllocateQubit(ulong qid) {
            QuantumManager.AllocateQubit(SystemId, qid);
        }

        public void ReleaseQubit(ulong qid) {
            QuantumManager.ReleaseQubit(SystemId, qid);
        }

        // Awake() is called before Start()
        void Awake()
        {
            SystemId = qMan.AllocateSimulator(QubitCount);
            lastQubitCount = QubitCount;
        }

        void Update()
        {
            if (QubitCount > 64)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning("Tried to realloc more than 64 qubits in system " + SystemId + ", clamped to 64");
                }
                QubitCount = 64;
            }

            if (QubitCount < 1)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning("Tried to realloc fewer than 1 qubit in system " + SystemId + ", clamped to 1");
                }
                QubitCount = 1;
            }

            if (lastQubitCount < QubitCount)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log("Automatically allocated qubits in system " + SystemId + ", original: " + lastQubitCount + ", new: " + QubitCount);
                }

                for (ulong i = lastQubitCount; i < QubitCount; ++i)
                {
                    AllocateQubit(i);
                }
            }

            if (lastQubitCount > QubitCount)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log("Automatically deallocated qubits in system " + SystemId + ", original: " + lastQubitCount + ", new: " + QubitCount);
                }

                for (ulong i = (lastQubitCount - 1); i >= QubitCount; i--)
                {
                    ReleaseQubit(i);
                }
            }

            lastQubitCount = QubitCount;
        }

        void OnDestroy()
        {
            if (qMan != null)
            {
                qMan.DeallocateSimulator(SystemId);
            }
        }

        private ulong[] MapQubits(ulong[] controls)
        {
            ulong[] mappedControls = new ulong[controls.Length];
            for (int i = 0; i < controls.Length; ++i)
            {
                mappedControls[i] = GetSystemIndex(controls[i]);
            }

            return mappedControls;
        }

        public virtual void CheckAlloc(List<ulong> bits)
        {
            bits.Sort();

            ulong highBit = bits[bits.Count - 1];

            if (highBit >= QubitCount)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log("Automatically allocated qubits in system " + SystemId + ", original: " + QubitCount + ", new: " + (highBit + 1));
                }

                QubitCount = highBit + 1;

                for (ulong i = lastQubitCount; i < QubitCount; ++i)
                {
                    AllocateQubit(i);
                }

                lastQubitCount = QubitCount;
            }
        }

        protected void SingleBitGate(ulong targetId, Action<ulong, ulong> func)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<ulong>() { targetId });
            func(SystemId, targetId);

            if (GetError() != 0) {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void Rand(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.Rand);
        }

        public void X(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.X);
        }

        public void Y(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.Y);
        }

        public void Z(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.Z);
        }

        public void H(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.H);
        }
        public void S(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.S);
        }

        public void T(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.T);
        }

        public void AdjS(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.AdjS);
        }

        public void AdjT(ulong targetId)
        {
            SingleBitGate(targetId, QuantumManager.AdjT);
        }

        public void U(ulong targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<ulong>() { targetId });
            QuantumManager.U(SystemId, targetId, theta, phi, lambda);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // 2x2 complex number matrix gate, (serialized as 8 doubles, real-imaginary adjacent, then row-major)
        public void Mtrx(double[] m, ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<ulong>() { targetId });
            QuantumManager.Mtrx(SystemId, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Pauli X
        public void PowX(double p, ulong targetId)
        {
            PowMCX(null, p, targetId);
        }

        // Powers (and roots) of CNOT
        public void PowMCX(ulong[] controlIds, double p, ulong targetId)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1 + p));
            double sinPi1P = Math.Sin(Math.PI * (1 + p));

            double[] m = {
                // 0-0
                (1 - cosPi1P) / 2, -sinPi1P / 2,
                // 0-1
                (1 + cosPi1P) / 2, sinPi1P / 2,
                // 1-0
                (1 - cosPiP) / 2, -sinPiP / 2,
                // 1-1
                (1 + cosPiP) / 2, sinPiP / 2
            };

            if ((controlIds == null) || (controlIds.Length == 0)) {
                Mtrx(m, targetId);
            } else {
                MCMtrx(controlIds, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Pauli Y
        public void PowY(double p, ulong targetId)
        {
            PowMCY(null, p, targetId);
        }

        // Powers (and roots) of CY
        public void PowMCY(ulong[] controlIds, double p, ulong targetId)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);

            double[] m = {
                // 0-0
                (1 + cosPiP) / 2, sinPiP / 2,
                // 0-1
                -sinPiP / 2, (-1 + cosPiP) / 2,
                // 1-0
                sinPiP / 2, (1 - cosPiP) / 2,
                // 1-1
                (1 + cosPiP) / 2, sinPiP / 2
            };

            if ((controlIds == null) || (controlIds.Length == 0)) {
                Mtrx(m, targetId);
            } else {
                MCMtrx(controlIds, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Pauli Z
        public void PowZ(double p, ulong targetId)
        {
            PowMCZ(null, p, targetId);
        }

        // Powers (and roots) of CZ
        public void PowMCZ(ulong[] controlIds, double p, ulong targetId)
        {
            double[] m = {
                // 0-0
                1, 0,
                // 0-1
                0, 0,
                // 1-0
                0, 0,
                // 1-1
                Math.Cos(Math.PI * p), Math.Sin(Math.PI * p)
            };

            if ((controlIds == null) || (controlIds.Length == 0)) {
                Mtrx(m, targetId);
            } else {
                MCMtrx(controlIds, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Hadamard gate
        public void PowH(double p, ulong targetId)
        {
            PowMCH(null, p, targetId);
        }

        // Powers (and roots) of CH
        public void PowMCH(ulong[] controlIds, double p, ulong targetId)
        {
            double sqrt2 = Math.Sqrt(2);
            double sqrt2x2 = 2 * sqrt2;
            double sqrt2p1 = 1 + sqrt2;
            double sqrt2m1 = -1 + sqrt2;
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1 + p));
            double sinPi1P = Math.Sin(Math.PI * (1 + p));

            double[] m = {
                // 0-0
                (sqrt2p1 - sqrt2m1 * cosPi1P) / sqrt2x2, -sqrt2m1 * sinPi1P / sqrt2x2,
                // 0-1
                sqrt2m1 * sqrt2p1 * (1 + cosPi1P) / sqrt2x2, sqrt2p1 * sqrt2m1 * sinPi1P / sqrt2x2,
                // 1-0
                (1 - cosPiP) / sqrt2x2, -sinPiP / sqrt2x2,
                // 1-1
                (sqrt2m1 + sqrt2p1 * cosPiP) / sqrt2x2, sqrt2p1 * sinPiP / sqrt2x2
            };

            if ((controlIds == null) || (controlIds.Length == 0)) {
                Mtrx(m, targetId);
            } else {
                MCMtrx(controlIds, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void R(Pauli basis, double phi, ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<ulong>() { targetId });
            QuantumManager.R(SystemId, (ulong)basis, phi, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        protected void SingleBitRotation(ulong targetId, double phi, Action<ulong, ulong, double> func)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<ulong>() { targetId });
            func(SystemId, targetId, phi);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void Exp(ulong targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.Exp);
        }

        public void RX(ulong targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.RX);
        }

        public void RY(ulong targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.RY);
        }

        public void RZ(ulong targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.RZ);
        }

        public void MCSingleBitGate(ulong[] controls, ulong targetId, Action<ulong, ulong, ulong[], ulong> func)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            func(SystemId, (ulong)mappedControls.Length, mappedControls, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MCX(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCX);
        }

        public void MCY(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCY);
        }

        public void MCZ(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCZ);
        }

        public void MCH(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCH);
        }

        public void MCS(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCS);
        }

        public void MCT(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCT);
        }

        public void MCAdjS(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCAdjS);
        }

        public void MCAdjT(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCAdjT);
        }

        public void MCU(ulong[] controls, ulong targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCU(SystemId, (ulong)mappedControls.Length, mappedControls, targetId, theta, phi, lambda);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Multiply-controlled 2x2 complex number matrix gate, (serialized as 8 doubles, real-imaginary adjacent, then row-major)
        public void MCMtrx(ulong[] controls, double[] m, ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCMtrx(SystemId, (ulong)mappedControls.Length, mappedControls, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MACX(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACX);
        }

        public void MACY(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACY);
        }

        public void MACZ(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACZ);
        }

        public void MACH(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACH);
        }

        public void MACS(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACS);
        }

        public void MACT(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACT);
        }

        public void MACAdjS(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACAdjS);
        }

        public void MACAdjT(ulong[] controls, ulong targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACAdjT);
        }

        public void MACU(ulong[] controls, ulong targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MACU(SystemId, (ulong)mappedControls.Length, mappedControls, targetId, theta, phi, lambda);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Multiply-controlled 2x2 complex number matrix gate, (serialized as 8 doubles, real-imaginary adjacent, then row-major)
        public void MACMtrx(ulong[] controls, double[] m, ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MACMtrx(SystemId, (ulong)mappedControls.Length, mappedControls, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of multiply-controlled Pauli X
        protected void PowMCNOTx(double p, ulong[] controls, ulong targetId, bool isAnti)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1 + p));
            double sinPi1P = Math.Sin(Math.PI * (1 + p));

            double[] m = {
                // 0-0
                (1 - cosPi1P) / 2, -sinPi1P / 2,
                // 0-1
                (1 + cosPi1P) / 2, sinPi1P / 2,
                // 1-0
                (1 - cosPiP) / 2, -sinPiP / 2,
                // 1-1
                (1 + cosPiP) / 2, sinPiP / 2
            };

            if (isAnti)
            {
                MACMtrx(controls, m, targetId);
            }
            else
            {
                MCMtrx(controls, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void PowMCNOT(double p, ulong[] controls, ulong targetId)
        {
            PowMCNOTx(p, controls, targetId, false);
        }

        public void PowMACNOT(double p, ulong[] controls, ulong targetId)
        {
            PowMCNOTx(p, controls, targetId, true);
        }

        // Powers (and roots) of multiply-controlled Pauli Y
        public void PowMCYx(double p, ulong[] controls, ulong targetId, bool isAnti)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);

            double[] m = {
                // 0-0
                (1 + cosPiP) / 2, sinPiP / 2,
                // 0-1
                -sinPiP / 2, (-1 + cosPiP) / 2,
                // 1-0
                sinPiP / 2, (1 - cosPiP) / 2,
                // 1-1
                (1 + cosPiP) / 2, sinPiP / 2
            };

            if (isAnti)
            {
                MACMtrx(controls, m, targetId);
            }
            else
            {
                MCMtrx(controls, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void PowMCY(double p, ulong[] controls, ulong targetId)
        {
            PowMCYx(p, controls, targetId, false);
        }

        public void PowMACY(double p, ulong[] controls, ulong targetId)
        {
            PowMCYx(p, controls, targetId, true);
        }

        // Powers (and roots) of multiply-controlled Pauli Z
        public void PowMCZx(double p, ulong[] controls, ulong targetId, bool isAnti)
        {
            double[] m = {
                // 0-0
                1, 0,
                // 0-1
                0, 0,
                // 1-0
                0, 0,
                // 1-1
                Math.Cos(Math.PI * p), Math.Sin(Math.PI * p)
            };

            if (isAnti)
            {
                MACMtrx(controls, m, targetId);
            }
            else
            {
                MCMtrx(controls, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void PowMCZ(double p, ulong[] controls, ulong targetId)
        {
            PowMCZx(p, controls, targetId, false);
        }

        public void PowMACZ(double p, ulong[] controls, ulong targetId)
        {
            PowMCZx(p, controls, targetId, true);
        }

        // Powers (and roots) of multiply-controlled "S" gate
        public void PowMCS(double p, ulong[] controls, ulong targetId)
        {
            PowMCZ(p / 2, controls, targetId);
        }

        public void PowMACS(double p, ulong[] controls, ulong targetId)
        {
            PowMACZ(p / 2, controls, targetId);
        }

        // Powers (and roots) of multiply-controlled "T" gate
        public void PowMCT(double p, ulong[] controls, ulong targetId)
        {
            PowMCZ(p / 4, controls, targetId);
        }

        public void PowMACT(double p, ulong[] controls, ulong targetId)
        {
            PowMACZ(p / 4, controls, targetId);
        }

        // Powers (and roots) of multiply-controlled Hadamard gate
        public void MCPowH(double p, ulong[] controls, ulong targetId)
        {
            MCPowHx(p, controls, targetId, false);
        }

        public void MACPowH(double p, ulong[] controls, ulong targetId)
        {
            MCPowHx(p, controls, targetId, true);
        }

        public void MCPowHx(double p, ulong[] controls, ulong targetId, bool isAnti)
        {
            double sqrt2 = Math.Sqrt(2);
            double sqrt2x2 = 2 * sqrt2;
            double sqrt2p1 = 1 + sqrt2;
            double sqrt2m1 = -1 + sqrt2;
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1 + p));
            double sinPi1P = Math.Sin(Math.PI * (1 + p));

            double[] m = {
                // 0-0
                (sqrt2p1 - sqrt2m1 * cosPi1P) / sqrt2x2, -sqrt2m1 * sinPi1P / sqrt2x2,
                // 0-1
                sqrt2m1 * sqrt2p1 * (1 + cosPi1P) / sqrt2x2, sqrt2p1 * sqrt2m1 * sinPi1P / sqrt2x2,
                // 1-0
                (1 - cosPiP) / sqrt2x2, -sinPiP / sqrt2x2,
                // 1-1
                (sqrt2m1 + sqrt2p1 * cosPiP) / sqrt2x2, sqrt2p1 * sinPiP / sqrt2x2
            };

            if (isAnti)
            {
                MACMtrx(controls, m, targetId);
            }
            else
            {
                MCMtrx(controls, m, targetId);
            }

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MultiBitGate(ulong[] targets, Action<ulong, ulong, ulong[]> func)
        {
            ulong[] mappedTargets = MapQubits(targets);
            List<ulong> bits = new List<ulong>();
            bits.AddRange(mappedTargets);
            CheckAlloc(bits);

            func(SystemId, (ulong)mappedTargets.Length, mappedTargets);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MX(ulong[] targets)
        {
            MultiBitGate(targets, QuantumManager.MX);
        }

        public void MY(ulong[] targets)
        {
            MultiBitGate(targets, QuantumManager.MY);
        }

        public void MZ(ulong[] targets)
        {
            MultiBitGate(targets, QuantumManager.MZ);
        }

        public void MCR(Pauli basis, double phi, ulong[] controls, ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCR(SystemId, (ulong)basis, phi, (ulong)mappedControls.Length, mappedControls, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        protected delegate void MCRot(ulong systemId, ulong controlLen, ulong[] controls, ulong targetId, double phi);

        protected void MCSingleBitRotation(ulong[] controls, ulong targetId, double phi, MCRot func)
        {
            targetId = GetSystemIndex(targetId);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            func(SystemId, (ulong)mappedControls.Length, mappedControls, targetId, phi);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MCExp(ulong[] controls, ulong targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCExp);
        }

        public void MCRX(ulong[] controls, ulong targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCRX);
        }

        public void MCRY(ulong[] controls, ulong targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCRY);
        }

        public void MCRZ(ulong[] controls, ulong targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCRZ);
        }

        protected void DoubleBitGate(ulong target1, ulong target2, Action<ulong, ulong, ulong> func)
        {
            target1 = GetSystemIndex(target1);
            target2 = GetSystemIndex(target2);
            CheckAlloc(new List<ulong>() { target1, target2 });
            func(SystemId, target1, target2);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void SWAP(ulong target1, ulong target2)
        {
            DoubleBitGate(target1, target2, QuantumManager.SWAP);
        }

        public void ISWAP(ulong target1, ulong target2)
        {
            DoubleBitGate(target1, target2, QuantumManager.ISWAP);
        }

        public void AdjISWAP(ulong target1, ulong target2)
        {
            DoubleBitGate(target1, target2, QuantumManager.AdjISWAP);
        }

        public void FSim(double theta, double phi, ulong target1, ulong target2)
        {
            target1 = GetSystemIndex(target1);
            target2 = GetSystemIndex(target2);
            CheckAlloc(new List<ulong>() { target1, target2 });
            QuantumManager.FSim(SystemId, theta, phi, target1, target2);
        }

        public void CSWAP(ulong[] controls, ulong target1, ulong target2)
        {
            target1 = GetSystemIndex(target1);
            target2 = GetSystemIndex(target2);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { target1, target2 };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.CSWAP(SystemId, (ulong)mappedControls.Length, mappedControls, target1, target2);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void ACSWAP(ulong[] controls, ulong target1, ulong target2)
        {
            target1 = GetSystemIndex(target1);
            target2 = GetSystemIndex(target2);
            ulong[] mappedControls = MapQubits(controls);
            List<ulong> bits = new List<ulong> { target1, target2 };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.ACSWAP(SystemId, (ulong)mappedControls.Length, mappedControls, target1, target2);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public bool M(ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            if (targetId >= QubitCount)
            {
                return false;
            }

            bool toRet = QuantumManager.M(SystemId, targetId) > 0;

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public ulong MAll()
        {
            ulong toRet = QuantumManager.MAll(SystemId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void QSET(ulong targetId)
        {
            if (!M(targetId))
            {
                X(targetId);
            }
        }

        public void QRESET(ulong targetId)
        {
            if (M(targetId))
            {
                X(targetId);
            }
        }

        public void BoolGate(ulong qInput1, ulong qInput2, ulong qOutput, Action<ulong, ulong, ulong, ulong> func)
        {
            qInput1 = GetSystemIndex(qInput1);
            qInput2 = GetSystemIndex(qInput2);
            qOutput = GetSystemIndex(qOutput);

            List<ulong> bits = new List<ulong> { qInput1, qInput2, qOutput };
            CheckAlloc(bits);

            func(SystemId, qInput1, qInput2, qOutput);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void QAND(ulong qInput1, ulong qInput2, ulong qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.AND);
        }

        public void QOR(ulong qInput1, ulong qInput2, ulong qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.OR);
        }

        public void QXOR(ulong qInput1, ulong qInput2, ulong qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.XOR);
        }

        public void QNAND(ulong qInput1, ulong qInput2, ulong qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.NAND);
        }

        public void QNOR(ulong qInput1, ulong qInput2, ulong qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.NOR);
        }

        public void QXNOR(ulong qInput1, ulong qInput2, ulong qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.XNOR);
        }

        public void SemiBoolGate(bool cInput, ulong qInput, ulong qOutput, Action<ulong, bool, ulong, ulong> func)
        {
            qInput = GetSystemIndex(qInput);
            qOutput = GetSystemIndex(qOutput);

            List<ulong> bits = new List<ulong> { qInput, qOutput };
            CheckAlloc(bits);

            func(SystemId, cInput, qInput, qOutput);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void CQAND(bool cInput, ulong qInput, ulong qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLAND);
        }

        public void CQOR(bool cInput, ulong qInput, ulong qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLOR);
        }

        public void CQXOR(bool cInput, ulong qInput, ulong qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLXOR);
        }

        public void CQNAND(bool cInput, ulong qInput, ulong qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLNAND);
        }

        public void CQNOR(bool cInput, ulong qInput, ulong qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLNOR);
        }

        public void CQXNOR(bool cInput, ulong qInput, ulong qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLXNOR);
        }

        public float Prob(ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            if (targetId >= QubitCount)
            {
                return 0;
            }

            float toRet = (float)QuantumManager.Prob(SystemId, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void ResetAll()
        {
            QuantumManager.ResetAll(SystemId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public float PermutationExpectation(ulong[] bits)
        {
            ulong[] mappedBits = MapQubits(bits);
            List<ulong> mappedList = new List<ulong>(mappedBits);
            CheckAlloc(mappedList);

            float toRet = (float)QuantumManager.PermutationExpectation(SystemId, (ulong)mappedBits.Length, mappedBits);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void SetBit(ulong targetID, bool tOrF)
        {
            if (tOrF)
            {
                QSET(targetID);
            } else
            {
                QRESET(targetID);
            }
        }

        public bool TrySeparate(ulong q)
        {
            q = GetSystemIndex(q);
            if (q >= QubitCount)
            {
                return true;
            }
            bool toRet = QuantumManager.TrySeparate(SystemId, q);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public bool TrySeparate(ulong q1, ulong q2)
        {
            CheckAlloc(new List<ulong>() { q1, q2 });
            bool toRet = QuantumManager.TrySeparate(SystemId, GetSystemIndex(q1), GetSystemIndex(q2));

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public bool TrySeparate(ulong[] q, double error_tol)
        {
            ulong[] mappedQ = MapQubits(q);
            bool toRet = QuantumManager.TrySeparate(SystemId, (ulong)mappedQ.Length, mappedQ, error_tol);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void SetReactiveSeparate(bool irs)
        {
            QuantumManager.SetReactiveSeparate(SystemId, irs);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public ulong Measure(ulong[] bases, ulong[] qubits)
        {
            ulong[] mappedQ = MapQubits(qubits);
            ulong toRet = QuantumManager.Measure(SystemId, bases, mappedQ);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void MeasureShots(ulong[] qubits, ulong[] measureResults)
        {
            ulong[] mappedQ = MapQubits(qubits);
            QuantumManager.MeasureShots(SystemId, mappedQ, measureResults);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void TimeEvolve(double t, TimeEvolveOpHeader[] teos, double[] mtrx)
        {
            TimeEvolveOpHeader[] mappedTeos = new TimeEvolveOpHeader[teos.Length];
            for (int i = 0; i < teos.Length; ++i)
            {
                mappedTeos[i].target = GetSystemIndex(teos[i].target);
                if (teos[i].controlLen > 0)
                {
                    mappedTeos[i].controls = new ulong[teos[i].controlLen];
                    for (ulong j = 0; j < teos[i].controlLen; ++j)
                    {
                        mappedTeos[i].controls[j] = GetSystemIndex(teos[i].controls[j]);
                    }
                    List<ulong> bits = new List<ulong> { mappedTeos[i].target };
                    bits.AddRange(mappedTeos[i].controls);
                    CheckAlloc(bits);
                }
                else
                {
                    mappedTeos[i].controls = null;
                }
            }

            QuantumManager.TimeEvolve(SystemId, t, (ulong)mappedTeos.Length, mappedTeos, (ulong)mtrx.Length, mtrx);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public int GetError()
        {
            return QuantumManager.GetError(SystemId);
        }

        public BlochSphereCoordinates Prob3Axis(ulong targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<ulong>() { targetId });
            BlochSphereCoordinates toRet = QuantumManager.Prob3Axis(SystemId, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }
    }
}