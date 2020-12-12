using System.Collections.Generic;
using UnityEngine;
using OpenRelativity.Objects;

namespace OpenRelativity.Audio
{
    public class RelativisticAudioSource : RelativisticBehavior
    {
        public Transform AudioSourceTransform;
        public AudioSource[] audioSources;
        public float[] pitches;

        public RelativisticObject relativisticObject { get; protected set; }

        protected RelativisticAudioSystem audioSystem;

        protected class RelativisticAudioSourcePlayTimeHistoryPoint
        {
            public Vector4 sourceWorldSpaceTimePos { get; set; }
            public int audioSourceIndex { get; set; }

            public RelativisticAudioSourcePlayTimeHistoryPoint(Vector4 sourceWorldSTPos, int audioSource)
            {
                sourceWorldSpaceTimePos = sourceWorldSTPos;
                audioSourceIndex = audioSource;
            }
        }

        protected List<RelativisticAudioSourcePlayTimeHistoryPoint> playTimeHistory;

        protected class RelativisticAudioSourceViwHistoryPoint
        {
            public Vector3 viw { get; set; }

            public float tihw { get; set; }

            public RelativisticAudioSourceViwHistoryPoint(Vector3 v, float t)
            {
                viw = v;
                tihw = t;
            }
        }

        protected List<RelativisticAudioSourceViwHistoryPoint> viwHistory;

        public Vector3 piw
        {
            get
            {
                return relativisticObject.piw;
            }
        }

        public Vector3 viw
        {
            get
            {
                return relativisticObject.viw;
            }
        }

        // Rindler metric responds immediately to player local acceleration,
        // but it would be better 
        public Matrix4x4 metric { get; protected set; }

        protected float tisw
        {
            get
            {
                return relativisticObject.GetTisw();
            }
        }

        protected Vector3 listenerPiw
        {
            get
            {
                return RelativisticAudioSystem.PlayerAudioListener.piw;
            }
        }

        protected Vector3 listenerViw
        {
            get
            {
                return RelativisticAudioSystem.PlayerAudioListener.viw;
            }
        }

        public Vector3 soundVelocity
        {
            get
            {
                Vector3 dispUnit = (listenerPiw - piw).normalized;

                return (audioSystem.RapidityOfSound * dispUnit + audioSystem.WorldSoundMediumRapidity)
                    .RapidityToVelocity(metric);
            }
        }
        protected Vector3 soundPosition
        {
            get
            {
                // opticalPiw is theoretically invariant under velocity changes
                // and therefore need not be cached
                return relativisticObject.opticalPiw + soundLightDelayTime * viw;
            }
        }

        protected float soundLightDelayTime
        {
            get
            {
                Vector3 dispUnit = (listenerPiw - piw).normalized;

                return tisw * (1 - Vector3.Project(soundVelocity, dispUnit).magnitude / state.SpeedOfLight);
            }
        }

        private bool firstFrame;

        protected float lastSoundWorldTime = float.NegativeInfinity;
        protected Vector3 lastViw;

        // Start is called before the first frame update
        void Start()
        {
            audioSystem = RelativisticAudioSystem.Instance;
            relativisticObject = GetComponent<RelativisticObject>();
            playTimeHistory = new List<RelativisticAudioSourcePlayTimeHistoryPoint>();
            viwHistory = new List<RelativisticAudioSourceViwHistoryPoint>();

            if (audioSources == null || audioSources.Length == 0)
            {
                audioSources = AudioSourceTransform.GetComponents<AudioSource>();
            }

            if (pitches == null || pitches.Length == 0)
            {
                pitches = new float[audioSources.Length];
                for (int i = 0; i < audioSources.Length; i++)
                {
                    pitches[i] = audioSources[i].pitch;

                    // Turn off built-in Doppler
                    audioSources[i].dopplerLevel = 0;
                }
            }

            firstFrame = true;
            lastViw = viw;
            viwHistory.Add(new RelativisticAudioSourceViwHistoryPoint(viw, float.NegativeInfinity));
        }

        private void Update()
        {
            if (state.isMovementFrozen)
            {
                for (int i = 0; i < audioSources.Length; i++)
                {
                    audioSources[i].pitch = 0;
                }

                return;
            }

            metric = SRelativityUtil.GetRindlerMetric(piw);

            float soundWorldTime = state.TotalTimeWorld - soundLightDelayTime;

            if (lastSoundWorldTime <= soundWorldTime)
            {
                AudioSourceTransform.position = soundPosition;

                if (!firstFrame)
                {
                    lastSoundWorldTime = soundWorldTime;
                    lastViw = viw;
                }
            }
            else
            {
                // If velocity changes, this helps smooth out a collision that puts the new sound time behind the old sound time.
                AudioSourceTransform.position = Vector3.Lerp(AudioSourceTransform.position, soundPosition, Mathf.Min(1.0f, Time.deltaTime / (lastSoundWorldTime - soundWorldTime)));
                // If the sound time suddenly increases, we don't have a compressed interval to Lerp(), so just move immediately up to date.
                // TODO: (There are literally smoother ways we could handle this.)
            }

            if (lastViw != viw)
            {
                lastViw = viw;
                viwHistory.Add(new RelativisticAudioSourceViwHistoryPoint(viw, soundWorldTime));
            }

            while (viwHistory.Count > 1)
            {
                if (viwHistory[1].tihw <= state.TotalTimeWorld)
                {
                    viwHistory.RemoveAt(0);
                } else
                {
                    break;
                }
            }

            WorldSoundDopplerShift();

            firstFrame = false;

            if (playTimeHistory.Count == 0)
            {
                return;
            }

            while (playTimeHistory[0].sourceWorldSpaceTimePos.w >= soundWorldTime)
            {
                audioSources[playTimeHistory[0].audioSourceIndex].Play();
                playTimeHistory.RemoveAt(0);
            }
        }

        public void WorldSoundDopplerShift()
        {
            Vector3 unitDisplacementSR = listenerPiw - soundPosition;
            unitDisplacementSR.Normalize();
            if (unitDisplacementSR == Vector3.zero)
            {
                unitDisplacementSR = Vector3.up;
            }

            Vector3 sourceRapidity = viwHistory[0].viw;
            sourceRapidity = sourceRapidity * sourceRapidity.InverseGamma(metric);

            Vector3 receiverRapidity = listenerViw;
            receiverRapidity = receiverRapidity * receiverRapidity.InverseGamma();

            float frequencyFactor = (audioSystem.RapidityOfSound + Vector3.Dot(sourceRapidity - audioSystem.WorldSoundMediumRapidity, unitDisplacementSR))
                / (audioSystem.RapidityOfSound + Vector3.Dot(receiverRapidity - audioSystem.WorldSoundMediumRapidity, -unitDisplacementSR));

            ShiftPitches(frequencyFactor);
        }

        public void ShiftPitches(float frequencyFactor)
        {
            for (int i = 0; i < pitches.Length; i++)
            {
                audioSources[i].pitch = frequencyFactor * pitches[i];
            }
        }

        public void PlayOnWorldClock(int audioSourceIndex = 0)
        {
            playTimeHistory.Add(new RelativisticAudioSourcePlayTimeHistoryPoint(relativisticObject.piw, audioSourceIndex));
        }
    }
}