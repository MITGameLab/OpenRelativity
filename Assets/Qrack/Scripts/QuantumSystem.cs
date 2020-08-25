#if OPEN_RELATIVITY_INCLUDED
using OpenRelativity.Objects;
#endif

using System.Collections.Generic;
using UnityEngine;

namespace Qrack
{
    public class QuantumSystem : MonoBehaviour
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
                return ClockOffset + myRO.GetLocalTime();
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
                return (float)myRO.localDeltaTime;
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
                return (float)myRO.localFixedDeltaTime;
#else
                return Time.fixedDeltaTime;
#endif
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            SystemId = qMan.AllocateSimulator(QubitCount);
            lastQubitCount = QubitCount;
        }

        void Update()
        {
            if (QubitCount > 64)
            {
                QubitCount = 64;
            }

            if (QubitCount < 1)
            {
                QubitCount = 1;
            }

            if (lastQubitCount < QubitCount)
            {
                for (uint i = lastQubitCount; i < QubitCount; i++)
                {
                    QuantumManager.AllocateQubit(SystemId, i);
                }
            }

            if (lastQubitCount > QubitCount)
            {
                for (uint i = (lastQubitCount - 1); i >= QubitCount; i--)
                {
                    QuantumManager.ReleaseQubit(SystemId, i);
                }
            }

        }

        void OnDestroy()
        {
            if (qMan != null)
            {
                qMan.DeallocateSimulator(SystemId);
            }
        }

        private uint[] MapControls(uint[] controls)
        {
            uint[] mappedControls = new uint[controls.Length];
            for (int i = 0; i < controls.Length; i++)
            {
                mappedControls[i] = GetSystemIndex(controls[i]);
            }

            return mappedControls;
        }

        public void Rand(uint targetId)
        {
            QuantumManager.Rand(SystemId, GetSystemIndex(targetId));
        }

        public void X(uint targetId)
        {
            QuantumManager.X(SystemId, GetSystemIndex(targetId));
        }

        public void Y(uint targetId)
        {
            QuantumManager.Y(SystemId, GetSystemIndex(targetId));
        }

        public void Z(uint targetId)
        {
            QuantumManager.Z(SystemId, GetSystemIndex(targetId));
        }

        public void H(uint targetId)
        {
            QuantumManager.H(SystemId, GetSystemIndex(targetId));
        }
        public void S(uint targetId)
        {
            QuantumManager.S(SystemId, GetSystemIndex(targetId));
        }

        public void T(uint targetId)
        {
            QuantumManager.T(SystemId, GetSystemIndex(targetId));
        }

        public void AdjS(uint targetId)
        {
            QuantumManager.AdjS(SystemId, GetSystemIndex(targetId));
        }

        public void AdjT(uint targetId)
        {
            QuantumManager.AdjT(SystemId, GetSystemIndex(targetId));
        }

        public void U(uint targetId, double theta, double phi, double lambda)
        {
            QuantumManager.U(SystemId, GetSystemIndex(targetId), theta, phi, lambda);
        }

        public void R(Pauli basis, double phi, uint targetId)
        {
            QuantumManager.R(SystemId, (uint)basis, phi, GetSystemIndex(targetId));
        }

        public void Exp(uint targetId, double phi)
        {
            QuantumManager.Exp(SystemId, GetSystemIndex(targetId), phi);
        }

        public void RX(uint targetId, double phi)
        {
            QuantumManager.RX(SystemId, GetSystemIndex(targetId), phi);
        }

        public void RY(uint targetId, double phi)
        {
            QuantumManager.RY(SystemId, GetSystemIndex(targetId), phi);
        }

        public void RZ(uint targetId, double phi)
        {
            QuantumManager.RZ(SystemId, GetSystemIndex(targetId), phi);
        }

        public void MCX(uint[] controls, uint targetId)
        {
            QuantumManager.MCX(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCY(uint[] controls, uint targetId)
        {
            QuantumManager.MCY(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCZ(uint[] controls, uint targetId)
        {
            QuantumManager.MCZ(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCH(uint[] controls, uint targetId)
        {
            QuantumManager.MCH(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCS(uint[] controls, uint targetId)
        {
            QuantumManager.MCS(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCT(uint[] controls, uint targetId)
        {
            QuantumManager.MCT(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCADJS(uint[] controls, uint targetId)
        {
            QuantumManager.MCADJS(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCADJT(uint[] controls, uint targetId)
        {
            QuantumManager.MCADJT(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCU(uint[] controls, uint targetId, double theta, double phi, double lambda)
        {
            QuantumManager.MCU(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId), theta, phi, lambda);
        }

        public void MCR(Pauli basis, double phi, uint[] controls, uint targetId)
        {
            QuantumManager.MCR(SystemId, (uint)basis, phi, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId));
        }

        public void MCExp(uint[] controls, uint targetId, double phi)
        {
            QuantumManager.MCExp(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId), phi);
        }

        public void MCRX(uint[] controls, uint targetId, double phi)
        {
            QuantumManager.MCRX(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId), phi);
        }

        public void MCRY(uint[] controls, uint targetId, double phi)
        {
            QuantumManager.MCRY(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId), phi);
        }

        public void MCRZ(uint[] controls, uint targetId, double phi)
        {
            QuantumManager.MCRZ(SystemId, (uint)controls.Length, MapControls(controls), GetSystemIndex(targetId), phi);
        }

        public bool M(uint targetId)
        {
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

        public void QAND(uint qInput1, uint qInput2, uint qOutput)
        {
            QuantumManager.AND(SystemId, GetSystemIndex(qInput1), GetSystemIndex(qInput2), GetSystemIndex(qOutput));
        }

        public void QOR(uint qInput1, uint qInput2, uint qOutput)
        {
            QuantumManager.OR(SystemId, GetSystemIndex(qInput1), GetSystemIndex(qInput2), GetSystemIndex(qOutput));
        }

        public void QXOR(uint qInput1, uint qInput2, uint qOutput)
        {
            QuantumManager.XOR(SystemId, GetSystemIndex(qInput1), GetSystemIndex(qInput2), GetSystemIndex(qOutput));
        }

        public void QNAND(uint qInput1, uint qInput2, uint qOutput)
        {
            QuantumManager.NAND(SystemId, GetSystemIndex(qInput1), GetSystemIndex(qInput2), GetSystemIndex(qOutput));
        }

        public void QNOR(uint qInput1, uint qInput2, uint qOutput)
        {
            QuantumManager.NOR(SystemId, GetSystemIndex(qInput1), GetSystemIndex(qInput2), GetSystemIndex(qOutput));
        }

        public void QXNOR(uint qInput1, uint qInput2, uint qOutput)
        {
            QuantumManager.XNOR(SystemId, GetSystemIndex(qInput1), GetSystemIndex(qInput2), GetSystemIndex(qOutput));
        }

        public void CQAND(bool cInput, uint qInput, uint cOutput)
        {
            QuantumManager.CLAND(SystemId, cInput, GetSystemIndex(qInput), GetSystemIndex(cOutput));
        }

        public void CQOR(bool cInput, uint qInput, uint cOutput)
        {
            QuantumManager.CLOR(SystemId, cInput, GetSystemIndex(qInput), GetSystemIndex(cOutput));
        }

        public void CQXOR(bool cInput, uint qInput, uint cOutput)
        {
            QuantumManager.CLXOR(SystemId, cInput, GetSystemIndex(qInput), GetSystemIndex(cOutput));
        }

        public void CQNAND(bool cInput, uint qInput, uint cOutput)
        {
            QuantumManager.CLNAND(SystemId, cInput, GetSystemIndex(qInput), GetSystemIndex(cOutput));
        }

        public void CQNOR(bool cInput, uint qInput, uint cOutput)
        {
            QuantumManager.CLNOR(SystemId, cInput, GetSystemIndex(qInput), GetSystemIndex(cOutput));
        }

        public void CQXNOR(bool cInput, uint qInput, uint cOutput)
        {
            QuantumManager.CLXNOR(SystemId, cInput, GetSystemIndex(qInput), GetSystemIndex(cOutput));
        }

        public float Prob(uint targetId)
        {
            return (float)QuantumManager.Prob(SystemId, GetSystemIndex(targetId));
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

        public void TimeEvolve(double t, TimeEvolveOpHeader[] teos, double[] mtrx)
        {
            QuantumManager.TimeEvolve(SystemId, t, (uint)teos.Length, teos, (uint)mtrx.Length, mtrx);
        }
    }
}