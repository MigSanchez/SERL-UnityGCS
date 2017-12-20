// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System.Collections;
using UnityEngine;

namespace AudioStream
{
    public class GVRSource : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        // [Range(0f, 1f)]
        // [Tooltip("Volume for AudioStreamMinimal has to be set independently from Unity audio")]
        // Leave volume at 1 for channel using GVR gain.
        float volume = 1f;

        [Header("[System]")]
        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
        #endregion

        // ========================================================================================================================================
        #region GVR Plugin + GVR parameters
        GVRPlugin gvrPlugin = null;
        
        [Header("[3D Settings]")]
        /// <summary>
        /// 
        /// </summary>
        public Transform listener;

        [Range(-80f, 24f)]
        [Tooltip("Gain")]
        public float gain = 0f;

        [Range(0f, 360f)]
        [Tooltip("Spread")]
        public float spread = 0f;

        [Range(0f, 10000f)]
        [Tooltip("min distance")]
        public float minDistance = 1f;

        [Range(0f, 10000f)]
        [Tooltip("max distance")]
        public float maxDistance = 500f;

        [Tooltip("rolloff")]
        public GVRPlugin.DistanceRolloff distanceRolloff = GVRPlugin.DistanceRolloff.LOGARITHMIC;

        [Range(0f, 10f)]
        [Tooltip("occlusion")]
        public float occlusion = 0f;

        // very narrow forward oriented cone for testing
        // directivity          -   0.8 -   forward cone only
        // directivitySharpness -   10  -   narrow focused cone

        [Range(0f, 1f)]
        [Tooltip("directivity")]
        public float directivity = 0f;

        [Range(1f, 10f)]
        [Tooltip("directivity sharpness")]
        public float directivitySharpness = 1f;

        [Tooltip("Bypass room")]
        public bool bypassRoom = false;

        /// <summary>
        /// previous positions for velocity
        /// </summary>
		Vector3 last_relative_position = Vector3.zero;
		Vector3 last_position = Vector3.zero;

        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            if (this.listener == null)
                this.listener = Camera.main.transform;

            yield return StartCoroutine(base.Start());

            while (!channel.hasHandle())
                yield return null;

            this.last_relative_position = this.transform.position - this.listener.position;
            this.last_position = this.transform.position;
        }

        void Update()
        {
            if (this.gvrPlugin != null)
            {
                // The position of the sound relative to the listeners.
                Vector3 rel_position = this.transform.position - this.listener.position;
                Vector3 rel_velocity = rel_position - this.last_relative_position;
                this.last_relative_position = rel_position;

                // The position of the sound in world coordinates.
                Vector3 abs_velocity = this.transform.position - this.last_position;
                this.last_position = this.transform.position;

                this.gvrPlugin.GVRSource_SetGain(this.gain);
                this.gvrPlugin.GVRSource_SetSpread(this.spread);
                this.gvrPlugin.GVRSource_SetMinDistance(this.minDistance);
                this.gvrPlugin.GVRSource_SetMaxDistance(this.maxDistance);
                this.gvrPlugin.GVRSource_SetDistanceRolloff(this.distanceRolloff);
                this.gvrPlugin.GVRSource_SetOcclusion(this.occlusion);
                this.gvrPlugin.GVRSource_SetDirectivity(this.directivity);
                this.gvrPlugin.GVRSource_SetDirectivitySharpness(this.directivitySharpness);

                this.gvrPlugin.GVRSource_Set3DAttributes(
                    this.listener.InverseTransformDirection(rel_position)
                    , rel_velocity
                    , this.listener.InverseTransformDirection(this.transform.forward)
                    , this.listener.InverseTransformDirection(this.transform.up)
                    , this.transform.position
                    , abs_velocity
                    , this.transform.forward
                    , this.transform.up
                    );

                this.gvrPlugin.GVRSource_SetBypassRoom(this.bypassRoom);
            }
        }

        protected override void OnDisable()
        {
            if (channel.hasHandle())
            {
                result = channel.removeDSP(this.gvrPlugin.GVRListener_DSP);
                ERRCHECK(result, "channel.removeDSP", false);

                result = channel.removeDSP(this.gvrPlugin.GVRSource_DSP);
                ERRCHECK(result, "channel.removeDSP", false);
            }

            if (this.gvrPlugin != null)
            {
                this.gvrPlugin.Release();
                this.gvrPlugin = null;
            }

            base.OnDisable();
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        protected override void StreamStarting(int samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            this.SetOutput(this.outputDriverID);

            FMOD.ChannelGroup master;
            result = system.getMasterChannelGroup(out master);
            ERRCHECK(result, "system.getMasterChannelGroup");

            result = system.playSound(sound, master, false, out channel);
            ERRCHECK(result, "system.playSound");

            result = channel.setVolume(this.volume);
            ERRCHECK(result, "channel.setVolume");

            /*
             *
             */
            this.gvrPlugin = new GVRPlugin(system, this.logLevel);

            /*
             * Add DSPs to the default channel when started.
             */
            channel.addDSP(0, this.gvrPlugin.GVRListener_DSP);
            ERRCHECK(result, "channel.addDSP");

            channel.addDSP(1, this.gvrPlugin.GVRSource_DSP);
            ERRCHECK(result, "channel.addDSP");
        }

        protected override bool StreamStarving()
        {
            if (channel.hasHandle())
            {
                /* Silence the stream until we have sufficient data for smooth playback. */
                result = channel.setMute(starving);
                //ERRCHECK(result, "channel.setMute", false);

                if (!starving)
                {
                    result = channel.setVolume(this.volume);
                    //ERRCHECK(result, "channel.setVolume", false);
                }
            }

            return this.starving || result != FMOD.RESULT.OK;
        }

        protected override void StreamPausing(bool pause)
        {
            if (channel.hasHandle())
            {
                result = this.channel.setPaused(pause);
                ERRCHECK(result, "channel.setPaused");
            }
        }

        protected override void StreamStopping()
        {

        }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            float defFrequency;
            int defPriority;
            result = sound.getDefaults(out defFrequency, out defPriority);
            ERRCHECK(result, "sound.getDefaults");

            LOG(LogLevel.INFO, "Stream samplerate change from {0}, {1}", defFrequency, sound_format);

            result = sound.setDefaults(samplerate, defPriority);
            ERRCHECK(result, "sound.setDefaults");

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}, {1}", samplerate, sound_format);
        }

        public override void SetOutput(int _outputDriverID)
        {
            LOG(LogLevel.INFO, "Setting output to driver {0} ", _outputDriverID);

            result = system.setDriver(_outputDriverID);
            ERRCHECK(result, "system.setDriver");

            /*
             * Log output device info
             */
            int od_namelen = 255;
            string od_name;
            System.Guid od_guid;
            int od_systemrate;
            FMOD.SPEAKERMODE od_speakermode;
            int od_speakermodechannels;

            result = system.getDriverInfo(this.outputDriverID, out od_name, od_namelen, out od_guid, out od_systemrate, out od_speakermode, out od_speakermodechannels);
            ERRCHECK(result, "system.getDriverInfo");

            LOG(LogLevel.INFO, "Device {0} Info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, od_speakermode, od_speakermodechannels);

            if (this.speakerMode != FMOD.SPEAKERMODE.DEFAULT)
                LOG(LogLevel.INFO, "Device {0} User: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, this.speakerMode, this.numOfRawSpeakers);

            this.outputDriverID = _outputDriverID;
        }
        #endregion
    }
}