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
        }

        public void R(Pauli basis, double phi, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            QuantumManager.R(SystemId, (uint)basis, phi, targetId);
        }

        protected void SingleBitRotation(uint targetId, double phi, Action<uint, uint, double> func)
        {
            targetId = GetSystemIndex(targetId);
            CheckAlloc(new List<uint>() { targetId });
            func(SystemId, GetSystemIndex(targetId), phi);
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

        public void MCADJS(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCADJS);
        }

        public void MCADJT(uint[] controls, uint targetId)
        {
            MCSingleBitGate(controls, targetId, QuantumManager.MCADJT);
        }

        public void MCU(uint[] controls, uint targetId, double theta, double phi, double lambda)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCU(SystemId, (uint)mappedControls.Length, mappedControls, targetId, theta, phi, lambda);
        }

        public void MCR(Pauli basis, double phi, uint[] controls, uint targetId)
        {
            targetId = GetSystemIndex(targetId);
            uint[] mappedControls = MapQubits(controls);
            List<uint> bits = new List<uint> { targetId };
            bits.AddRange(mappedControls);
            CheckAlloc(bits);

            QuantumManager.MCR(SystemId, (uint)basis, phi, (uint)mappedControls.Length, mappedControls, targetId);
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
            if (targetId >= QubitCount)
            {
                return false;
            }

            return QuantumManager.M(SystemId, GetSystemIndex(targetId)) > 0;
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
            if (targetId >= QubitCount)
            {
                return 0;
            }

            return (float)QuantumManager.Prob(SystemId, GetSystemIndex(targetId));
        }

        public float PermutationExpectation(uint[] bits)
        {
            uint[] mappedBits = MapQubits(bits);
            List<uint> mappedList = new List<uint>(mappedBits);
            CheckAlloc(mappedList);

            return (float)QuantumManager.PermutationExpectation(SystemId, (uint)mappedBits.Length, mappedBits);
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
            return QuantumManager.TrySeparate(SystemId, GetSystemIndex(q));
        }

        public bool TrySeparate(uint q1, uint q2)
        {
            return QuantumManager.TrySeparate(SystemId, GetSystemIndex(q1), GetSystemIndex(q2));
        }

        public bool TrySeparate(uint[] q, double error_tol)
        {
            uint[] mappedQ = MapQubits(q);
            return QuantumManager.TrySeparate(SystemId, (uint)mappedQ.Length, mappedQ, error_tol);
        }

        public void SetReactiveSeparate(bool irs)
        {
            QuantumManager.SetReactiveSeparate(SystemId, irs);
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
        }
    }
}