// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public abstract class AudioStreamInputBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Required descendant's implementation
        protected abstract void RecordingStarted();
        protected abstract void RecordingUpdate();
        protected abstract void RecordingStopped();
        #endregion

        // ========================================================================================================================================
        #region Editor
        // "No. of audio channels provided by selected recording device.
        [HideInInspector]
        public int recChannels = 0;
        [HideInInspector]
        protected int recRate = 0;

        [Header("[Source]")]
        [Tooltip("Audio input driver ID")]
        public int recordDeviceId = 0;

        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("When checked the recording will start automatically on Start with parameters set in Inspector. Otherwise StartCoroutine(Record()) of this component.")]
        public bool recordOnStart = true;

		[Tooltip("Input gain. Default 1")]
		[Range(0f, 1.2f)]
		public float gain = 1f;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnRecordingStarted;
        public EventWithStringBoolParameter OnRecordingPaused;
        public EventWithStringParameter OnRecordingStopped;
        public EventWithStringStringParameter OnError;
        #endregion
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region Init && FMOD structures
        /// <summary>
        /// Component startup sync
        /// Also in case of recording FMOD needs some time to enumerate all present recording devices - we need to wait for it. Check this flag when using from scripting.
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;

        protected FMOD.System system;
        protected FMOD.Sound sound;
        protected FMOD.RESULT result;
        protected FMOD.RESULT lastError = FMOD.RESULT.OK;
        protected FMOD.CREATESOUNDEXINFO exinfo;
        protected uint version;
        protected uint datalength = 0;
        protected uint soundlength;
        protected uint lastrecordpos = 0;

        protected virtual IEnumerator Start()
        {
            // Reference Microphone class on Android in order for Unity to include necessary manifest permission automatically
#if UNITY_ANDROID
            for (var i = 0; i < Microphone.devices.Length; ++i)
                print(string.Format("Enumerating input devices on Android - {0}: {1}", i, Microphone.devices[i]));
#endif
            this.gameObjectName = this.gameObject.name;

            // setup the AudioSource if it's being used
            var audiosrc = this.GetComponent<AudioSource>();
            if (audiosrc)
            {
                audiosrc.playOnAwake = false;
                audiosrc.Stop();
                audiosrc.clip = null;
            }

            // create (temporary) fmod system just for enumerating available input drivers
            // will be recreated once actual Record is called ( in order to start the recording with clean buffers - it does not help completely all the time, however )

            /*
            Create a System object and initialize.
            */
            result = FMOD.Factory.System_Create(out system);
            ERRCHECK(result, "Factory.System_Create");

            result = system.getVersion(out version);
            ERRCHECK(result, "system.getVersion");

            /*
                FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
            */
            var versionString = System.Convert.ToString(version, 16).PadLeft(8, '0');
            this.fmodVersion = string.Format("{0}.{1}.{2}", System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

            if (version < FMOD.VERSION.number)
            {
                var msg = string.Format("FMOD lib version {0} doesn't match header version {1}", version, FMOD.VERSION.number);

                LOG(LogLevel.ERROR, msg);

                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);

                yield break;
            }

            /*
             * Adjust DSP buffer for recording
             */ 
            uint bufferLength;
            int numBuffers;
            result = system.getDSPBufferSize(out bufferLength, out numBuffers);
            ERRCHECK(result, "system.getDSPBufferSize");

            result = system.setDSPBufferSize(64, numBuffers);
            ERRCHECK(result, "system.setDSPBufferSize");

            result = system.getDSPBufferSize(out bufferLength, out numBuffers);
            ERRCHECK(result, "system.getDSPBufferSize");

            LOG(LogLevel.INFO, "FMOD DSP buffer: {0} length, {1} buffers", bufferLength, numBuffers);

#if UNITY_ANDROID && !UNITY_EDITOR
            // For recording to work on Android OpenSL support is needed:
            // https://www.fmod.org/questions/question/is-input-recording-supported-on-android/

            result = system.setOutput(FMOD.OUTPUTTYPE.OPENSL);
            ERRCHECK(result, "system.setOutput", false);

            if ( result != FMOD.RESULT.OK )
            {
                LOG(LogLevel.ERROR, "OpenSL support needed for recording not available.");
                yield break;
            }
#endif
            /*
            System initialization
            */
            result = system.init(10, FMOD.INITFLAGS.NORMAL, System.IntPtr.Zero);
            ERRCHECK(result, "system.init");

            // wait for FMDO to catch up - recordDrivers are not populated if called immediately [e.g. from Start]

            int numAllDrivers = 0;
            int numConnectedDrivers = 0;
            int retries = 0;

            while (numConnectedDrivers < 1)
            {
                result = system.getRecordNumDrivers(out numAllDrivers, out numConnectedDrivers);
                ERRCHECK(result, "system.getRecordNumDrivers");

                LOG(LogLevel.INFO, "{0} {1}", numAllDrivers, numConnectedDrivers);

                if (++retries > 60)
                {
                    var msg = string.Format("There seems to be no audio input device connected");

                    LOG(LogLevel.ERROR, msg);

                    if (this.OnError != null)
                        this.OnError.Invoke(this.gameObjectName, msg);

                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            if (this.recordOnStart)
                StartCoroutine(this.Record());

            this.ready = true;
        }

        #endregion

        // ========================================================================================================================================
        #region Recording
        [Header("[Runtime]")]
        [Tooltip("Set during recording.")]
        public bool isRecording = false;
        [Tooltip("Set during recording.")]
        public bool isPaused = false;
        /// <summary>
        /// FMOD recording buffers and their lengths
        /// </summary>
        protected System.IntPtr ptr1, ptr2;
        protected uint len1, len2;

        public IEnumerator Record()
        {
            if (this.isRecording)
            {
                LOG(LogLevel.WARNING, "Already recording.");
                yield break;
            }

            if (!this.isActiveAndEnabled)
            {
                LOG(LogLevel.WARNING, "Will not start on disabled GameObject.");
                yield break;
            }

            this.isRecording = false;
            this.isPaused = false;

            this.Stop_Internal(); // try to clean partially started recording / Start initialized system

            /*
            Create a System object and initialize.
            */
            result = FMOD.Factory.System_Create(out system);
            ERRCHECK(result, "Factory.System_Create");

            result = system.getVersion(out version);
            ERRCHECK(result, "system.getVersion");

            if (version < FMOD.VERSION.number)
            {
                var msg = string.Format("FMOD lib version {0} doesn't match header version {1}", version, FMOD.VERSION.number);

                LOG(LogLevel.ERROR, msg);

                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);

                yield break;
            }

            /*
             * Adjust DSP buffer for recording
             */
            uint bufferLength;
            int numBuffers;
            result = system.getDSPBufferSize(out bufferLength, out numBuffers);
            ERRCHECK(result, "system.getDSPBufferSize");

            result = system.setDSPBufferSize(64, numBuffers);
            ERRCHECK(result, "system.setDSPBufferSize");

            result = system.getDSPBufferSize(out bufferLength, out numBuffers);
            ERRCHECK(result, "system.getDSPBufferSize");

            LOG(LogLevel.INFO, "FMOD DSP buffer: {0} length, {1} buffers", bufferLength, numBuffers);

#if UNITY_ANDROID && !UNITY_EDITOR
            // For recording to work on Android OpenSL support is needed:
            // https://www.fmod.org/questions/question/is-input-recording-supported-on-android/

            result = system.setOutput(FMOD.OUTPUTTYPE.OPENSL);
            ERRCHECK(result, "system.setOutput", false);

            if ( result != FMOD.RESULT.OK )
            {
                LOG(LogLevel.ERROR, "OpenSL support needed for recording not available.");
                yield break;
            }
#endif
            /*
            System initialization
            */
            result = system.init(10, FMOD.INITFLAGS.NORMAL, System.IntPtr.Zero);
            ERRCHECK(result, "system.init");

            // wait for FMDO to catch up - recordDrivers are not populated if called immediately [e.g. from Start]

            int numAllDrivers = 0;
            int numConnectedDrivers = 0;
            int retries = 0;

            while (numConnectedDrivers < 1)
            {
                result = system.getRecordNumDrivers(out numAllDrivers, out numConnectedDrivers);
                ERRCHECK(result, "system.getRecordNumDrivers");

                if (++retries > 60)
                {
                    var msg = string.Format("There seems to be no audio input device connected");

                    LOG(LogLevel.ERROR, msg);

                    if (this.OnError != null)
                        this.OnError.Invoke(this.gameObjectName, msg);

                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                LOG(LogLevel.INFO, "Setting audio output to default/earspeaker ...");
                iOSSpeaker.RouteForRecording();
            }

            /*
             * create FMOD sound
             */
            int namelen = 255;
            string name;
            System.Guid guid;
            FMOD.SPEAKERMODE speakermode;
            FMOD.DRIVER_STATE driverstate;
            result = system.getRecordDriverInfo(this.recordDeviceId, out name, namelen, out guid, out recRate, out speakermode, out recChannels, out driverstate);
            ERRCHECK(result, "system.getRecordDriverInfo");

            exinfo = new FMOD.CREATESOUNDEXINFO();
            exinfo.numchannels = recChannels;
            exinfo.format = FMOD.SOUND_FORMAT.PCM16;
            exinfo.defaultfrequency = recRate;
            exinfo.length = (uint)(recRate * sizeof(short) * recChannels);    /* 1 second buffer, size here doesn't change latency */
            exinfo.cbsize = Marshal.SizeOf(exinfo);

            result = system.createSound(string.Empty, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref exinfo, out sound);
            ERRCHECK(result, "system.createSound");

            result = system.recordStart(this.recordDeviceId, sound, true);
            ERRCHECK(result, "system.recordStart");

            result = sound.getLength(out soundlength, FMOD.TIMEUNIT.PCM);
            ERRCHECK(result, "sound.getLength");

            datalength = 0;

            this.RecordingStarted();

            this.isRecording = true;

            if (this.OnRecordingStarted != null)
                this.OnRecordingStarted.Invoke(this.gameObjectName);

            yield return StartCoroutine(this.RecordCR());
        }

        IEnumerator RecordCR()
        {
            while (this.isRecording)
            {
                this.RecordingUpdate();
                yield return null;
            }
        }

        public void Pause(bool pause)
        {
            if (!this.isRecording)
            {
                LOG(LogLevel.WARNING, "Not recording..");
                return;
            }

            this.isPaused = pause;

            LOG(LogLevel.INFO, "{0}", this.isPaused ? "paused." : "resumed.");

            if (this.OnRecordingPaused != null)
                this.OnRecordingPaused.Invoke(this.gameObjectName, this.isPaused);
        }
        #endregion

        // ========================================================================================================================================
        #region Shutdown
        public void Stop()
        {
            LOG(LogLevel.INFO, "Stopping..");

            this.StopAllCoroutines();

            /*
             * clear FMOD buffer/s - they like to be reused, and reset rec position -
             */
            this.lastrecordpos = 0;

            if (ptr1.ToInt64() != 0 && ptr1 != System.IntPtr.Zero && len1 > 0)
            {
                byte[] barr = new byte[len1];
                for (int i = 0; i < barr.Length; ++i) barr[i] = 0;
                Marshal.Copy(barr, 0, ptr1, (int)len1);
            }

            if (ptr2.ToInt64() != 0 && ptr2 != System.IntPtr.Zero && len2 > 0)
            {
                byte[] barr = new byte[len2];
                for (int i = 0; i < barr.Length; ++i) barr[i] = 0;
                Marshal.Copy(barr, 0, ptr2, (int)len2);
            }

            this.Stop_Internal();

            this.RecordingStopped();

            if (this.OnRecordingStopped != null)
                this.OnRecordingStopped.Invoke(this.gameObjectName);
        }

        /// <summary>
        /// Stop and try to release FMOD sound resources
        /// </summary>
        void Stop_Internal()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
            {
                asource.Stop();
                Destroy(asource.clip);
                asource.clip = null;
            }

            this.isRecording = false;
            this.isPaused = false;

            /*
                Shut down sound
            */
            if (sound.hasHandle())
            {
                result = sound.release();
                ERRCHECK(result, "sound.release", false);

                sound.clearHandle();
            }

            /*
                Shut down
            */
            if (system.hasHandle())
            {
                result = system.close();
                ERRCHECK(result, "system.close", false);

                result = system.release();
                ERRCHECK(result, "system.release", false);

                system.clearHandle();
            }
        }

        void OnDisable()
        {
            this.Stop();
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        protected void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
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
        /// Enumerates available audio inputs in the system and returns their names.
        /// </summary>
        /// <returns></returns>
        public List<string> AvailableInputs()
        {
            List<string> availableDriversNames = new List<string>();

            /*
            Enumerate record devices
            */
            int numAllDrivers = 0;
            int numConnectedDrivers = 0;
            result = system.getRecordNumDrivers(out numAllDrivers, out numConnectedDrivers);
            ERRCHECK(result, "system.getRecordNumDrivers");

            for (int i = 0; i < numConnectedDrivers; ++i)
            {
                int recChannels = 0;
                int recRate = 0;
                int namelen = 255;
                string name;
                System.Guid guid;
                FMOD.SPEAKERMODE speakermode;
                FMOD.DRIVER_STATE driverstate;
                result = system.getRecordDriverInfo(i, out name, namelen, out guid, out recRate, out speakermode, out recChannels, out driverstate);
                ERRCHECK(result, "system.getRecordDriverInfo");

                var description = string.Format("{0} rate: {1} speaker mode: {2} channels: {3}", name, recRate, speakermode, recChannels);

                availableDriversNames.Add(description);

                LOG(LogLevel.DEBUG, "{0}{1}guid: {2}{3}systemrate: {4}{5}speaker mode: {6}{7}channels: {8}{9}state: {10}"
                    , name
                    , System.Environment.NewLine
                    , guid
                    , System.Environment.NewLine
                    , recRate
                    , System.Environment.NewLine
                    , speakermode
                    , System.Environment.NewLine
                    , recChannels
                    , System.Environment.NewLine
                    , driverstate
                    );
            }

            return availableDriversNames;
        }
        #endregion
    }
}