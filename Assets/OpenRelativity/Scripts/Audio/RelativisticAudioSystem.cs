using UnityEngine;
using OpenRelativity;

namespace OpenRelativity.Audio
{
    public class RelativisticAudioSystem : RelativisticBehavior
    {
        // We want a "System" (in Entity-Component-Systems) to be unique.
        // Ensure a singleton:

        private static RelativisticAudioSystem _instance;

        public static RelativisticAudioSystem Instance { get { return _instance; } }

        public static RelativisticAudioListener PlayerAudioListener;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                _instance = this;

                PlayerAudioListener = Camera.main.GetComponent<RelativisticAudioListener>();
            }
        }

        // We treat the speed of sound as a "rapidity of sound" instead, in a relativistic context.
        // The underlying medium of sound wave propagation has an intrinsically preferred frame of inertial rest,
        // (at least locally, in a medium of real particles).
        //
        // The "rapidity," the integral of proper acceleration, is free to be arbitrarily high, up to an infinite limit
        // corresponding with the speed of light. If sender and receiver rapidities relative to the underlying wave medium
        // rest frame are low compared to the arbitrarily high rapidity of sound, then it might sound like something close
        // normal human experience of non-relativistic sound, except delayed by the relativistic transformation from
        // "rapidity" to "velocity" in transit away from source. However, it seems obvious that material physics of a
        // chemical medium like Earth's atmosphere would not stay close to what they are under the alteration of
        // fundamental constants such as the speed of light, like we do in this physics module.
        //
        // "Rapidity of sound" might be a reasonable model for a "hyper-fluid" wave medium under intensely relativistic conditions.
        // Fundamentally, a chemical medium as we know it will not approach these conditions under intense alteration of the
        // speed of light and other constants.

        // Static system physical constants:
        public float RapidityOfSound = 343.0f;
        public Vector3 WorldSoundMediumRapidity = Vector3.zero;

        public void WorldSoundDopplerShift(RelativisticAudioSource source)
        {
            Vector3 unitDisplacementSR = PlayerAudioListener.piw - source.piw;
            unitDisplacementSR.Normalize();
            if (unitDisplacementSR == Vector3.zero)
            {
                unitDisplacementSR = Vector3.up;
            }

            Vector3 sourceRapidity = source.viw;
            sourceRapidity = sourceRapidity * sourceRapidity.InverseGamma(source.metric);

            Vector3 receiverRapidity = PlayerAudioListener.viw;
            receiverRapidity = receiverRapidity * receiverRapidity.InverseGamma();

            float frequencyFactor = (RapidityOfSound + Vector3.Dot(sourceRapidity - WorldSoundMediumRapidity, unitDisplacementSR))
                / (RapidityOfSound + Vector3.Dot(receiverRapidity - WorldSoundMediumRapidity, unitDisplacementSR));

            source.ShiftPitches(frequencyFactor);
        }

        public float WorldSoundVelocityDelay(RelativisticAudioSource source)
        {
            Vector3 displacement = PlayerAudioListener.piw - source.piw;
            float distance = displacement.magnitude;

            if (distance == 0)
            {
                return 0;
            }

            Vector3 dispUnit = displacement / distance;

            Matrix4x4 m = source.metric;
            float gamma = source.viw.Gamma(m);

            return distance / (gamma * Vector3.Project(source.viw, dispUnit)).RapidityToVelocity(m).AddVelocity(source.soundVelocity).magnitude;
        }
    }
}