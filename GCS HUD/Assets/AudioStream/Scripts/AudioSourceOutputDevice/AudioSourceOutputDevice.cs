// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    public class AudioSourceOutputDevice : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;
        [Tooltip("When used with streaming we have to wait until after clip is created and playing once stream is setup. AudioStream calls this' StartRedirect() automatically - set this to false when used in conjuction with AudioStream component.\r\nWhen checked it also tries to start - Play() - attached AudioSource.\r\n\r\nSee also OutputDeviceDemo.cs for how to make calls at runtime when not starting automatically.")]
        public bool autoStart = false;
        [Tooltip("Speaker mode for redirected signal")]
        public FMOD.SPEAKERMODE speakerMode = FMOD.SPEAKERMODE.DEFAULT;
        [Tooltip("No. of speakers for RAW speaker mode. You must also provide mix matrix for custom setups,\r\nsee remarks at http://www.fmod.org/documentation/#content/generated/FMOD_SPEAKERMODE.html, \r\nand http://www.fmod.org/documentation/#content/generated/FMOD_Channel_SetMixMatrix.html about how to setup the matrix.")]
        // Specify 0 to ignore; when raw speaker mode is selected that defaults to 2 speakers ( stereo ), unless set by user.
        public int numOfRawSpeakers = 0;
        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and 'getNumDrivers' - see demo scene's Start() and AvailableOutputs()")]
        public int outputDriverID = 0;
        [Tooltip("Mute the signal after being routed.\r\nUseful when having more than one AudioSourceOutputDevice on one AudioSource/Listener for multiple devices at the same time.\r\n- only the last one in chain should be muted in that case.")]
        public bool muteAfterRouting = true;


        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnRedirectStarted;
        public EventWithStringParameter OnRedirectStopped;
        public EventWithStringStringParameter OnError;
        #endregion
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region FMOD && Unity audio callback
        /// <summary>
        /// Component startup sync
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;

        protected FMOD.System system;
        protected FMOD.Sound sound;
        protected FMOD.Channel channel;
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;

        FMOD.SOUND_PCMREADCALLBACK pcmreadcallback;
        FMOD.SOUND_PCMSETPOSCALLBACK pcmsetposcallback;

        /// <summary>
        /// the size of a single sample ( i.e. per channel ) is the size of Int16 a.k.a. signed short 
        /// specified by FMOD.SOUND_FORMAT.PCM16 format while creating sound info for createSound
        /// </summary>
        int elementSize;
        /// <summary>
        /// true if redirect is running
        /// </summary>
        bool isRedirecting = false;
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        IEnumerator Start()
        {
            this.gameObjectName = this.gameObject.name;


            this.elementSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(byte)) * 2;

            // Explicitly create the delegate object and assign it to a member so it doesn't get freed
            // by the garbage collected while it's being used
            this.pcmreadcallback = new FMOD.SOUND_PCMREADCALLBACK(PCMReadCallback);
            this.pcmsetposcallback = new FMOD.SOUND_PCMSETPOSCALLBACK(PCMSetPosCallback);

            // decodebuffersize samples worth of bytes will be called in read callback
            // createSound calls back, too
            this.pcmReadCallbackBuffer = new List<List<byte>>();
            this.pcmReadCallbackBuffer.Add(new List<byte>());
            this.pcmReadCallbackBuffer.Add(new List<byte>());

            /*
             * temporary system init to allow reporting available output devices.
             */
            this.CreateAndInitSystem(false);

            // wait for FMDO to catch up to be safe if requested to play immediately [i.e. autoStart]
            int numDrivers = 0;
            int retries = 0;

            do
            {
                result = system.getNumDrivers(out numDrivers);
                ERRCHECK(result, "system.getNumDrivers");

                LOG(LogLevel.INFO, "Got {0} driver/s available", numDrivers);

                if (++retries > 500)
                {
                    var msg = string.Format("There seems to be no audio output device connected");

                    LOG(LogLevel.ERROR, msg);

                    if (this.OnError != null)
                        this.OnError.Invoke(this.gameObjectName, msg);

                    yield break;
                }

                yield return null;

            } while (numDrivers < 1);


            if (this.autoStart)
            {
                var _as = this.GetComponent<AudioSource>();
                if (_as != null)
                    _as.Play();

                this.StartFMODSound();
            }

            this.ready = true;
        }

        byte[] bArr = null;
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channel.hasHandle())
            {
                AudioStreamSupport.FloatArrayToByteArray(data, (uint)data.Length, ref this.bArr);
                // lock for PCMReadCallback
                lock (this.pcmReadCallbackBufferLock)
                {
                    this.pcmReadCallbackBuffer[this.pcmReadCallback_ActiveBuffer].AddRange(bArr);
                }
            }

            if (this.muteAfterRouting)
                // clear the output buffer if this component is the last one in audio chain, i.e. at the bottom in the inspector
                Array.Clear(data, 0, data.Length);
        }

        void OnDisable()
        {
            this.StopFMODSound();

            if (this.pcmReadCallbackBuffer != null)
            {
                this.pcmReadCallbackBuffer[0].Clear();
                this.pcmReadCallbackBuffer[1].Clear();

                this.pcmReadCallbackBuffer[0] = null;
                this.pcmReadCallbackBuffer[1] = null;

                this.pcmReadCallbackBuffer.Clear();
                this.pcmReadCallbackBuffer = null;
            }

            this.pcmreadcallback = null;
            this.pcmsetposcallback = null;
        }
        #endregion

        // ========================================================================================================================================
        #region Start / Stop

        void StartFMODSound()
        {
            this.StopFMODSound();

            this.CreateAndInitSystem(true);

            LOG(LogLevel.INFO, "Setting output to driver {0} ", this.outputDriverID);

            result = system.setDriver(this.outputDriverID);
            ERRCHECK(result, "system.setDriver");

            // try to infer signal properties in order not to wait for OAFR to start, which is desirable under some circumstances such as AutoStart when the clip is still empty.
            // take signal parameters from AudioSettings. - hopefully it is enough, i.e. will always correspond to what is passed into OAFR..
            // change of these should not happen during runtime
            var config = AudioSettings.GetConfiguration();
            var channels = AudioStreamSupport.ChannelsFromAudioSpeakerMode(config.speakerMode);
            var OAFRbufferSize = config.dspBufferSize * channels;

            // Work around fmod and Unity buffer sizes
            // - FMOD does not even bother to call the pcmcallbacks if the buffer is too small ( e.g. Unity's with best latency audio setting it is 512 bytes (256 samples, 2 ch), so force the reported incoming buffer size to be at least some minimum.
            // - we need to drain OAFR buffer sufficiently enough to prevent under- and overruns ( crackling, resp. out of mem exceptions over long periods of time )
            // , so we request chunks large enough to cover the OAFR buffer size under all circumstances which might arise from resampling due to output device having different sample rate, and having FMOD dynamically resize its requests depending on the pcm buffer state
            // note: this directly affect latency ( which is noticeable but due to the above inevitable )
            uint decodebuffersize = (uint)Mathf.Max(OAFRbufferSize * 4, 1024);

            FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
            // exinfo.cbsize = sizeof(FMOD.CREATESOUNDEXINFO);
            exinfo.numchannels = channels;                                                              /* Number of channels in the sound. */
            exinfo.defaultfrequency = AudioSettings.outputSampleRate;                                   /* Default playback rate of sound. */
            exinfo.decodebuffersize = decodebuffersize;                                                 /* Chunk size of stream update in samples. This will be the amount of data passed to the user callback. */
            exinfo.length = (uint)(exinfo.defaultfrequency * exinfo.numchannels * this.elementSize);    /* Length of PCM data in bytes of whole song (for Sound::getLength) */
            exinfo.format = FMOD.SOUND_FORMAT.PCM16;                                                    /* Data format of sound. */
            exinfo.pcmreadcallback = this.pcmreadcallback;                                              /* User callback for reading. */
            exinfo.pcmsetposcallback = this.pcmsetposcallback;                                          /* User callback for seeking. */
            exinfo.cbsize = System.Runtime.InteropServices.Marshal.SizeOf(exinfo);

            result = system.createSound(""
                , FMOD.MODE.OPENUSER
                | FMOD.MODE.CREATESTREAM
                | FMOD.MODE.LOOP_NORMAL
                , ref exinfo
                , out sound);
            ERRCHECK(result, "system.createSound");

            LOG(LogLevel.INFO, "About to play...");

            FMOD.ChannelGroup master;
            result = system.getMasterChannelGroup(out master);
            ERRCHECK(result, "system.getMasterChannelGroup");

            result = system.playSound(sound, master, false, out channel);
            ERRCHECK(result, "system.playSound");

            LOG(LogLevel.INFO, string.Format("Starting redirect to device {0}", this.outputDriverID));

            this.isRedirecting = true;

            if (this.OnRedirectStarted != null)
                this.OnRedirectStarted.Invoke(this.gameObjectName);
        }

        void StopFMODSound()
        {
            if (channel.hasHandle())
            {
                result = channel.setVolume(0f);
                result = channel.stop();
                ERRCHECK(result, "channel.stop", false);

                channel.clearHandle();
            }

            // pause was in original sample; however, it causes noticeable pop on default device when stopping the sound
            // removing it does not _seem_ to affect anything, but even then - occasional sound on default device is not completely eliminated :/

            // System.Threading.Thread.Sleep(50);

            if (sound.hasHandle())
            {
                result = sound.release();
                ERRCHECK(result, "sound.release", false);

                sound.clearHandle();
            }

            if (system.hasHandle())
            {
                result = system.close();
                ERRCHECK(result, "system.close", false);

                result = system.release();
                ERRCHECK(result, "system.release", false);

                system.clearHandle();
            }

            if (this.pcmReadCallbackBuffer != null)
            {
                this.pcmReadCallbackBuffer[0].Clear();
                this.pcmReadCallbackBuffer[1].Clear();
            }

            this.isRedirecting = false;

            LOG(LogLevel.INFO, string.Format("Stopped redirect to device {0}", this.outputDriverID));

            if (this.OnRedirectStopped != null)
                this.OnRedirectStopped.Invoke(this.gameObjectName);
        }

        #endregion

        // ========================================================================================================================================
        #region fmod buffer callbacks
        List<List<byte>> pcmReadCallbackBuffer = null;
        object pcmReadCallbackBufferLock = new object();
        int pcmReadCallback_ActiveBuffer = 0;

        [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMREADCALLBACK))]
        FMOD.RESULT PCMReadCallback(System.IntPtr soundraw, System.IntPtr data, uint datalen)
        {
            // lock on pcmReadCallbackBuffer - can be changed ( added to ) in OAFR thread leading here to collision
            lock (this.pcmReadCallbackBufferLock)
            {
                var count = this.pcmReadCallbackBuffer[this.pcmReadCallback_ActiveBuffer].Count;

                // LOG(LogLevel.DEBUG, "PCMReadCallback requested {0} while having {1}, time: {2}", datalen, count, AudioSettings.dspTime);

                if (count > 0 && count >= datalen && datalen > 0)
                {
                    var bArr = this.pcmReadCallbackBuffer[this.pcmReadCallback_ActiveBuffer].ToArray();

                    System.Runtime.InteropServices.Marshal.Copy(bArr, 0, data, (int)datalen);

                    this.pcmReadCallbackBuffer[1 - this.pcmReadCallback_ActiveBuffer].AddRange(this.pcmReadCallbackBuffer[this.pcmReadCallback_ActiveBuffer].GetRange((int)datalen, count - (int)datalen));

                    this.pcmReadCallbackBuffer[this.pcmReadCallback_ActiveBuffer].Clear();

                    this.pcmReadCallback_ActiveBuffer = 1 - this.pcmReadCallback_ActiveBuffer;
                }
                else
                {
                    var bArr = new byte[datalen];
                    for (int i = 0; i < datalen; ++i)
                        bArr[i] = 0;

                    System.Runtime.InteropServices.Marshal.Copy(bArr, 0, data, (int)datalen);
                }
            }

            return FMOD.RESULT.OK;
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMSETPOSCALLBACK))]
        FMOD.RESULT PCMSetPosCallback(System.IntPtr soundraw, int subsound, uint position, FMOD.TIMEUNIT postype)
        {
            LOG(LogLevel.DEBUG, "PCMSetPosCallback requesting position {0}. Having {1}, time: {2}"
                , position
                , this.pcmReadCallbackBuffer[this.pcmReadCallback_ActiveBuffer].Count
                , AudioSettings.dspTime
                );
            return FMOD.RESULT.OK;
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        /// <summary>
        /// 
        /// </summary>
        /// <param name="syncOutput"></param>
        void CreateAndInitSystem(bool syncOutput)
        {
            /*
             * create component sound system (tm)
             */
            uint version = 0;

            result = FMOD.Factory.System_Create(out system);
            ERRCHECK(result, "FMOD.Factory.System_Create");

            result = system.getVersion(out version);
            ERRCHECK(result, "system.getVersion");

            if (version < FMOD.VERSION.number)
            {
                var msg = string.Format("FMOD lib version {0} doesn't match header version {1}", version, FMOD.VERSION.number);
                LOG(LogLevel.ERROR, msg);

                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);

                return;
            }

            /*
                FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
            */
            var versionString = System.Convert.ToString(version, 16).PadLeft(8, '0');
            this.fmodVersion = string.Format("{0}.{1}.{2}", System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

            if (syncOutput)
            {
                /*
                 * sync FMOD samplerate and channels with output device.
                 * - must be set before init.
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
                {
                    system.setSoftwareFormat(od_systemrate, this.speakerMode, this.numOfRawSpeakers);
                    ERRCHECK(result, "system.setSoftwareFormat");

                    LOG(LogLevel.INFO, "Device {0} User: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", this.outputDriverID, od_systemrate, this.speakerMode, this.numOfRawSpeakers);
                }
            }

            int fmod_rate;
            FMOD.SPEAKERMODE fmod_sm;
            int fmod_sc;

            result = system.getSoftwareFormat(out fmod_rate, out fmod_sm, out fmod_sc);
            ERRCHECK(result, "system.getSoftwareFormat");

            LOG(LogLevel.INFO, "FMOD samplerate: {0}, speaker mode: {1}, num. of raw speakers: {2}", fmod_rate, fmod_sm, fmod_sc);

            /*
             * init the system
             */
            System.IntPtr extradriverdata = System.IntPtr.Zero;
            result = system.init(10, FMOD.INITFLAGS.NORMAL, extradriverdata);
            ERRCHECK(result, "system.init");
        }

        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, this.OnError, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }

        #endregion

        // ========================================================================================================================================
        #region User support
        /// <summary>
        /// change of output device means we have to set sound format, which is allowed only before system init -> we have to restart
        /// </summary>
        /// <param name="_outputDriverID"></param>
        public void SetOutput(int _outputDriverID)
        {
            if (_outputDriverID == this.outputDriverID)
                return;

            this.outputDriverID = _outputDriverID;

            if (this.isRedirecting)
                this.StartFMODSound();
        }

        public void StartRedirect()
        {
            this.StartFMODSound();
        }

        public void StopRedirect()
        {
            StopAllCoroutines();

            this.StopFMODSound();
        }

        /// <summary>
        /// Enumerates available audio outputs in the system and returns their names.
        /// </summary>
        /// <returns></returns>
        public List<string> AvailableOutputs()
        {
            if (system.hasHandle())
            {
                LOG(LogLevel.ERROR, "AudioSourceOutputDevice not yet initialized before usage.");
                return null;
            }

            List<string> availableDriversNames = new List<string>();

            int numDrivers;
            result = system.getNumDrivers(out numDrivers);
            ERRCHECK(result, "system.getNumDrivers");

            for (int i = 0; i < numDrivers; ++i)
            {
                int namelen = 255;
                string name;
                System.Guid guid;
                int systemrate;
                FMOD.SPEAKERMODE speakermode;
                int speakermodechannels;

                result = system.getDriverInfo(i, out name, namelen, out guid, out systemrate, out speakermode, out speakermodechannels);
                ERRCHECK(result, "system.getDriverInfo");

                availableDriversNames.Add(name.ToString());

                LOG(LogLevel.DEBUG, "{0}{1}guid: {2}{3}systemrate: {4}{5}speaker mode: {6}{7}channels: {8}"
                    , name
                    , System.Environment.NewLine
                    , guid
                    , System.Environment.NewLine
                    , systemrate
                    , System.Environment.NewLine
                    , speakermode
                    , System.Environment.NewLine
                    , speakermodechannels
                    );
            }

            return availableDriversNames;
        }
        #endregion
    }
}
