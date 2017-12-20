// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using UnityEngine;

namespace AudioStream
{
    public class AudioStreamMinimal : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        [Header("")]
        [Range(0f, 1f)]
        [Tooltip("Volume for AudioStreamMinimal has to be set independently from Unity audio")]
        public float volume = 1f;

        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
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

        protected override void StreamStopping() { }

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
