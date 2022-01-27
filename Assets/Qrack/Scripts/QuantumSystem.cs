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

        public uint QubitCount = 1;
        public float ClockOffset;

        public uint SystemId { get; set; }

        protected uint lastQubitCount;

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

        virtual protected uint GetSystemIndex(uint registerIndex)
        {
            return registerIndex;
        }

#if OPEN_RELATIVITY_INCLUDED
        private RelativisticObject _myRO;

        private RelativisticObject myRO
        {
            get
            {
                return _myRO != null ? _myRO : _myRO = GetComponent<RelativisticObject>();
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

                for (uint i = lastQubitCount; i < QubitCount; i++)
                {
                    QuantumManager.AllocateQubit(SystemId, i);
                }
            }

            if (lastQubitCount > QubitCount)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log("Automatically deallocated qubits in system " + SystemId + ", original: " + lastQubitCount + ", new: " + QubitCount);
                }

                for (uint i = (lastQubitCount - 1); i >= QubitCount; i--)
                {
                    QuantumManager.ReleaseQubit(SystemId, i);
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

        private uint[] MapQubits(uint[] controls, int controlLen = -1)
        {
            if (controls == null)
            {
                return null;
            }

            if (controlLen < 0) {
                controlLen = controls.Length;
            }

            if (controlLen == 0)
            {
                return controls;
            }

            uint[] mappedControls = new uint[controlLen];
            for (int i = 0; i < controlLen; i++)
            {
                mappedControls[i] = GetSystemIndex(controls[i]);
            }

            return mappedControls;
        }

        public virtual void CheckAlloc(List<uint> bits)
        {
            bits.Sort();

            uint highBit = bits[bits.Count - 1];

            if (highBit >= QubitCount)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log("Automatically allocated qubits in system " + SystemId + ", original: " + QubitCount + ", new: " + (highBit + 1));
                }

                QubitCount = highBit + 1;

                for (uint i = lastQubitCount; i < QubitCount; i++)
                {
                    QuantumManager.AllocateQubit(SystemId, i);
                }

                lastQubitCount = QubitCount;
            }
        }

        protected void SingleBitGate(uint targetId, Action<uint, uint> func)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            func(SystemId, targetId);

            if (GetError() != 0) {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void Rand(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.Rand);
        }

        public void X(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.X);
        }

        public void Y(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.Y);
        }

        public void Z(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.Z);
        }

        public void H(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.H);
        }
        public void S(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.S);
        }

        public void T(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.T);
        }

        public void AdjS(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.AdjS);
        }

        public void AdjT(uint targetId)
        {
            SingleBitGate(targetId, QuantumManager.AdjT);
        }

        public void U(uint targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            QuantumManager.U(SystemId, targetId, theta, phi, lambda);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // 2x2 complex number matrix gate, (serialized as 8 doubles, real-imaginary adjacent, then row-major)
        public void Mtrx(double[] m, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            QuantumManager.Mtrx(SystemId, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Pauli X
        public void PowX(double p, uint targetId)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1.0 + p));
            double sinPi1P = Math.Sin(Math.PI * (1.0 + p));

            double[] m = {
                // 0-0
                0.5 * (1.0 - cosPi1P), -0.5 * sinPi1P,
                // 0-1
                0.5 * (1.0 + cosPi1P), 0.5 * sinPi1P,
                // 1-0
                0.5 * (1.0 - cosPiP), -0.5 * sinPiP,
                // 1-1
                0.5 * (1.0 + cosPiP), 0.5 * sinPiP
            };

            Mtrx(m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Pauli Y
        public void PowY(double p, uint targetId)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);

            double[] m = {
                // 0-0
                0.5 * (1.0 + cosPiP), 0.5 * sinPiP,
                // 0-1
                -0.5 * sinPiP, 0.5 * (-1.0 + cosPiP),
                // 1-0
                0.5 * sinPiP, 0.5 * (1.0 - cosPiP),
                // 1-1
                0.5 * (1.0 + cosPiP), 0.5 * sinPiP
            };

            Mtrx(m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Pauli Z
        public void PowZ(double p, uint targetId)
        {
            double[] m = {
                // 0-0
                1.0, 0.0,
                // 0-1
                0.0, 0.0,
                // 1-0
                0.0, 0.0,
                // 1-1
                Math.Cos(Math.PI * p), Math.Sin(Math.PI * p)
            };

            Mtrx(m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of "S" gate
        public void PowS(double p, uint targetId)
        {
            PowZ(p / 2.0, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of "T" gate
        public void PowT(double p, uint targetId)
        {
            PowZ(p / 4.0, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of Hadamard gate
        public void PowH(double p, uint targetId)
        {
            double sqrt2 = Math.Sqrt(2.0);
            double sqrt2x2 = 2.0 * sqrt2;
            double sqrt2p1 = 1.0 + sqrt2;
            double sqrt2m1 = -1.0 + sqrt2;
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1.0 + p));
            double sinPi1P = Math.Sin(Math.PI * (1.0 + p));

            double[] m = {
                // 0-0
                (sqrt2p1 - sqrt2m1 * cosPi1P) / sqrt2x2, -sqrt2m1 * sinPi1P / sqrt2x2,
                // 0-1
                sqrt2m1 * sqrt2p1 * (1.0 + cosPi1P) / sqrt2x2, sqrt2p1 * sqrt2m1 * sinPi1P / sqrt2x2,
                // 1-0
                (1.0 - cosPiP) / sqrt2x2, -sinPiP / sqrt2x2,
                // 1-1
                (sqrt2m1 + sqrt2p1 * cosPiP) / sqrt2x2, sqrt2p1 * sinPiP / sqrt2x2
            };

            Mtrx(m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void R(Pauli basis, double phi, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            QuantumManager.R(SystemId, (uint)basis, phi, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        protected void SingleBitRotation(uint targetId, double phi, Action<uint, uint, double> func)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            func(SystemId, targetId, phi);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void Exp(uint targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.Exp);
        }

        public void RX(uint targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.RX);
        }

        public void RY(uint targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.RY);
        }

        public void RZ(uint targetId, double phi)
        {
            SingleBitRotation(targetId, phi, QuantumManager.RZ);
        }

        public void MCSingleBitGate(uint[] controls, uint targetId, Action<uint, uint, uint[], uint> func)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            func(SystemId, (uint)mappedControls.Length, mappedControls, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MCX(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCX);
        }

        public void MCY(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCY);
        }

        public void MCZ(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCZ);
        }

        public void MCH(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCH);
        }

        public void MCS(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCS);
        }

        public void MCT(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCT);
        }

        public void MCAdjS(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCAdjS);
        }

        public void MCAdjT(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCAdjT);
        }

        public void MCU(uint[] controls, uint targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCU(SystemId, (uint)mappedControls.Length, mappedControls, targetId, theta, phi, lambda);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Multiply-controlled 2x2 complex number matrix gate, (serialized as 8 doubles, real-imaginary adjacent, then row-major)
        public void MCMtrx(uint[] controls, double[] m, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCMtrx(SystemId, (uint)mappedControls.Length, mappedControls, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MACX(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACX);
        }

        public void MACY(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACY);
        }

        public void MACZ(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACZ);
        }

        public void MACH(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACH);
        }

        public void MACS(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACS);
        }

        public void MACT(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACT);
        }

        public void MACAdjS(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACAdjS);
        }

        public void MACAdjT(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MACAdjT);
        }

        public void MACU(uint[] controls, uint targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MACU(SystemId, (uint)mappedControls.Length, mappedControls, targetId, theta, phi, lambda);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Multiply-controlled 2x2 complex number matrix gate, (serialized as 8 doubles, real-imaginary adjacent, then row-major)
        public void MACMtrx(uint[] controls, double[] m, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MACMtrx(SystemId, (uint)mappedControls.Length, mappedControls, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        // Powers (and roots) of multiply-controlled Pauli X
        protected void PowMCNOTx(double p, uint[] controls, uint targetId, bool isAnti)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1.0 + p));
            double sinPi1P = Math.Sin(Math.PI * (1.0 + p));

            double[] m = {
                // 0-0
                0.5 * (1.0 - cosPi1P), -0.5 * sinPi1P,
                // 0-1
                0.5 * (1.0 + cosPi1P), 0.5 * sinPi1P,
                // 1-0
                0.5 * (1.0 - cosPiP), -0.5 * sinPiP,
                // 1-1
                0.5 * (1.0 + cosPiP), 0.5 * sinPiP
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

        public void PowMCNOT(double p, uint[] controls, uint targetId)
        {
            PowMCNOTx(p, controls, targetId, false);
        }

        public void PowMACNOT(double p, uint[] controls, uint targetId)
        {
            PowMCNOTx(p, controls, targetId, true);
        }

        // Powers (and roots) of multiply-controlled Pauli Y
        public void PowMCYx(double p, uint[] controls, uint targetId, bool isAnti)
        {
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);

            double[] m = {
                // 0-0
                0.5 * (1.0 + cosPiP), 0.5 * sinPiP,
                // 0-1
                -0.5 * sinPiP, 0.5 * (-1.0 + cosPiP),
                // 1-0
                0.5 * sinPiP, 0.5 * (1.0 - cosPiP),
                // 1-1
                0.5 * (1.0 + cosPiP), 0.5 * sinPiP
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

        public void PowMCY(double p, uint[] controls, uint targetId)
        {
            PowMCYx(p, controls, targetId, false);
        }

        public void PowMACY(double p, uint[] controls, uint targetId)
        {
            PowMCYx(p, controls, targetId, true);
        }

        // Powers (and roots) of multiply-controlled Pauli Z
        public void PowMCZx(double p, uint[] controls, uint targetId, bool isAnti)
        {
            double[] m = {
                // 0-0
                1.0, 0.0,
                // 0-1
                0.0, 0.0,
                // 1-0
                0.0, 0.0,
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

        public void PowMCZ(double p, uint[] controls, uint targetId)
        {
            PowMCZx(p, controls, targetId, false);
        }

        public void PowMACZ(double p, uint[] controls, uint targetId)
        {
            PowMCZx(p, controls, targetId, true);
        }

        // Powers (and roots) of multiply-controlled "S" gate
        public void PowMCS(double p, uint[] controls, uint targetId)
        {
            PowMCZ(p / 2.0, controls, targetId);
        }

        public void PowMACS(double p, uint[] controls, uint targetId)
        {
            PowMACZ(p / 2.0, controls, targetId);
        }

        // Powers (and roots) of multiply-controlled "T" gate
        public void PowMCT(double p, uint[] controls, uint targetId)
        {
            PowMCZ(p / 4.0, controls, targetId);
        }

        public void PowMACT(double p, uint[] controls, uint targetId)
        {
            PowMACZ(p / 4.0, controls, targetId);
        }

        // Powers (and roots) of multiply-controlled Hadamard gate
        public void MCPowH(double p, uint[] controls, uint targetId)
        {
            double sqrt2 = Math.Sqrt(2.0);
            double sqrt2x2 = 2.0 * sqrt2;
            double sqrt2p1 = 1.0 + sqrt2;
            double sqrt2m1 = -1.0 + sqrt2;
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1.0 + p));
            double sinPi1P = Math.Sin(Math.PI * (1.0 + p));

            double[] m = {
                // 0-0
                (sqrt2p1 - sqrt2m1 * cosPi1P) / sqrt2x2, -sqrt2m1 * sinPi1P / sqrt2x2,
                // 0-1
                sqrt2m1 * sqrt2p1 * (1.0 + cosPi1P) / sqrt2x2, sqrt2p1 * sqrt2m1 * sinPi1P / sqrt2x2,
                // 1-0
                (1.0 - cosPiP) / sqrt2x2, -sinPiP / sqrt2x2,
                // 1-1
                (sqrt2m1 + sqrt2p1 * cosPiP) / sqrt2x2, sqrt2p1 * sinPiP / sqrt2x2
            };

            MCMtrx(controls, m, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MCPowHx(double p, uint[] controls, uint targetId, bool isAnti)
        {
            double sqrt2 = Math.Sqrt(2.0);
            double sqrt2x2 = 2.0 * sqrt2;
            double sqrt2p1 = 1.0 + sqrt2;
            double sqrt2m1 = -1.0 + sqrt2;
            double cosPiP = Math.Cos(Math.PI * p);
            double sinPiP = Math.Sin(Math.PI * p);
            double cosPi1P = Math.Cos(Math.PI * (1.0 + p));
            double sinPi1P = Math.Sin(Math.PI * (1.0 + p));

            double[] m = {
                // 0-0
                (sqrt2p1 - sqrt2m1 * cosPi1P) / sqrt2x2, -sqrt2m1 * sinPi1P / sqrt2x2,
                // 0-1
                sqrt2m1 * sqrt2p1 * (1.0 + cosPi1P) / sqrt2x2, sqrt2p1 * sqrt2m1 * sinPi1P / sqrt2x2,
                // 1-0
                (1.0 - cosPiP) / sqrt2x2, -sinPiP / sqrt2x2,
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

        public void MCR(Pauli basis, double phi, uint[] controls, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCR(SystemId, (uint)basis, phi, (uint)mappedControls.Length, mappedControls, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        protected delegate void MCRot(uint systemId, uint controlLen, uint[] controls, uint targetId, double phi);

        protected void MCSingleBitRotation(uint[] controls, uint targetId, double phi, MCRot func)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            func(SystemId, (uint)mappedControls.Length, mappedControls, targetId, phi);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void MCExp(uint[] controls, uint targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCExp);
        }

        public void MCRX(uint[] controls, uint targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCRX);
        }

        public void MCRY(uint[] controls, uint targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCRY);
        }

        public void MCRZ(uint[] controls, uint targetId, double phi)
        {
            MCSingleBitRotation(controls, targetId, phi, QuantumManager.MCRZ);
        }

        public bool M(uint targetId)
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

        public uint MAll()
        {
            uint toRet = QuantumManager.MAll(SystemId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void QSET(uint targetId)
        {
            if (!M(targetId))
            {
                X(targetId);
            }
        }

        public void QRESET(uint targetId)
        {
            if (M(targetId))
            {
                X(targetId);
            }
        }

        public void BoolGate(uint qInput1, uint qInput2, uint qOutput, Action<uint, uint, uint, uint> func)
        {
            qInput1 = GetSystemIndex(qInput1);
            qInput2 = GetSystemIndex(qInput2);
            qOutput = GetSystemIndex(qOutput);

            List<uint> bits = new List<uint> { qInput1, qInput2, qOutput };
            CheckAlloc(bits);

            func(SystemId, qInput1, qInput2, qOutput);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void QAND(uint qInput1, uint qInput2, uint qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.AND);
        }

        public void QOR(uint qInput1, uint qInput2, uint qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.OR);
        }

        public void QXOR(uint qInput1, uint qInput2, uint qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.XOR);
        }

        public void QNAND(uint qInput1, uint qInput2, uint qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.NAND);
        }

        public void QNOR(uint qInput1, uint qInput2, uint qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.NOR);
        }

        public void QXNOR(uint qInput1, uint qInput2, uint qOutput)
        {
            BoolGate(qInput1, qInput2, qOutput, QuantumManager.XNOR);
        }

        public void SemiBoolGate(bool cInput, uint qInput, uint qOutput, Action<uint, bool, uint, uint> func)
        {
            qInput = GetSystemIndex(qInput);
            qOutput = GetSystemIndex(qOutput);

            List<uint> bits = new List<uint> { qInput, qOutput };
            CheckAlloc(bits);

            func(SystemId, cInput, qInput, qOutput);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void CQAND(bool cInput, uint qInput, uint qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLAND);
        }

        public void CQOR(bool cInput, uint qInput, uint qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLOR);
        }

        public void CQXOR(bool cInput, uint qInput, uint qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLXOR);
        }

        public void CQNAND(bool cInput, uint qInput, uint qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLNAND);
        }

        public void CQNOR(bool cInput, uint qInput, uint qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLNOR);
        }

        public void CQXNOR(bool cInput, uint qInput, uint qOutput)
        {
            SemiBoolGate(cInput, qInput, qOutput, QuantumManager.CLXNOR);
        }

        public float Prob(uint targetId)
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

        public float PermutationExpectation(uint[] bits)
        {
            uint[] mappedBits = MapQubits(bits);
            List<uint> mappedList = new List<uint>(mappedBits);
            CheckAlloc(mappedList);

            float toRet = (float)QuantumManager.PermutationExpectation(SystemId, (uint)mappedBits.Length, mappedBits);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void SetBit(uint targetID, bool tOrF)
        {
            if (tOrF)
            {
                QSET(targetID);
            } else
            {
                QRESET(targetID);
            }
        }

        public bool TrySeparate(uint q)
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

        public bool TrySeparate(uint q1, uint q2)
        {
            CheckAlloc(new List<uint>() { q1, q2 });
            bool toRet = QuantumManager.TrySeparate(SystemId, GetSystemIndex(q1), GetSystemIndex(q2));

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public bool TrySeparate(uint[] q, double error_tol)
        {
            uint[] mappedQ = MapQubits(q);
            bool toRet = QuantumManager.TrySeparate(SystemId, (uint)mappedQ.Length, mappedQ, error_tol);

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

        public uint Measure(uint[] bases, uint[] qubits)
        {
            uint[] mappedQ = MapQubits(qubits);
            uint toRet = QuantumManager.Measure(SystemId, bases, mappedQ);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }

        public void MeasureShots(uint[] qubits, uint[] measureResults)
        {
            uint[] mappedQ = MapQubits(qubits);
            QuantumManager.MeasureShots(SystemId, mappedQ, measureResults);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public void TimeEvolve(double t, TimeEvolveOpHeader[] teos, double[] mtrx)
        {
            TimeEvolveOpHeader[] mappedTeos = new TimeEvolveOpHeader[teos.Length];
            for (int i = 0; i < teos.Length; i++)
            {
                mappedTeos[i].target = GetSystemIndex(teos[i].target);
                mappedTeos[i].controls = MapQubits(teos[i].controls, (int)teos[i].controlLen);
                List<uint> bits = new List<uint> { mappedTeos[i].target };
                bits.AddRange(mappedTeos[i].controls);
                CheckAlloc(bits);
            }

            QuantumManager.TimeEvolve(SystemId, t, (uint)mappedTeos.Length, mappedTeos, (uint)mtrx.Length, mtrx);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }
        }

        public uint GetError()
        {
            return QuantumManager.GetError(SystemId);
        }

        public BlochSphereCoordinates Prob3Axis(uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            BlochSphereCoordinates toRet = QuantumManager.Prob3Axis(SystemId, targetId);

            if (GetError() != 0)
            {
                throw new InvalidOperationException("QrackSimulator C++ library raised exception.");
            }

            return toRet;
        }
    }
}