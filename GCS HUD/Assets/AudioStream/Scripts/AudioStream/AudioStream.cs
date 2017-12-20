// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioStream : AudioStreamBase
    {
        /// <summary>
        /// autoresolved reference for automatic playback redirection
        /// </summary>
        AudioSourceOutputDevice audioSourceOutputDevice = null;

        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            // setup the AudioSource
            var audiosrc = this.GetComponent<AudioSource>();
            audiosrc.playOnAwake = false;
            audiosrc.Stop();
            audiosrc.clip = null;

            // and check if AudioSourceOutputDevice is present
            this.audioSourceOutputDevice = this.GetComponent<AudioSourceOutputDevice>();

            yield return StartCoroutine(base.Start());
        }


        byte[] streamDataBytes = null;
        GCHandle streamDataBytesPinned;
        System.IntPtr streamDataBytesPtr = System.IntPtr.Zero;
        float[] oafrDataArr = null; // instance buffer
        FMOD.SOUND_FORMAT stream_sound_format;
        byte bytes_per_sample;
        /// <summary>
        /// Starving flag for Sound::readData is separate from base class
        /// </summary>
        bool _networkStarving = false;
        /// <summary>
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any processing here, just return them
        /// </summary>
        /// <param name="data"></param>
        void PCMReaderCallback(float[] data)
        {
            if (result == FMOD.RESULT.OK && openstate == FMOD.OPENSTATE.READY && this.isPlaying && !this.isPaused)
            {
				var blength = data.Length * this.bytes_per_sample;

                if (this.streamDataBytes == null || this.streamDataBytes.Length != blength)
                {
					LOG(LogLevel.DEBUG, "Allocating new stream buffer of size {0} ({1}b per sample)", blength, this.bytes_per_sample);

                    this.streamDataBytes = new byte[blength];

                    this.streamDataBytesPinned.Free();
                    this.streamDataBytesPinned = GCHandle.Alloc(this.streamDataBytes, GCHandleType.Pinned);

                    this.streamDataBytesPtr = this.streamDataBytesPinned.AddrOfPinnedObject();
                }

                uint read = 2;
                result = this.sound.readData(this.streamDataBytesPtr, (uint)this.streamDataBytes.Length, out read);

                // ERRCHECK(result, "OAFR sound.readData", false);

                if (result == FMOD.RESULT.OK)
                {
                    if (read > 0)
                    {
                        int length = AudioStreamSupport.ByteArrayToFloatArray(this.streamDataBytes, read, this.bytes_per_sample, this.stream_sound_format, ref this.oafrDataArr);
                        Array.Copy(this.oafrDataArr, data, length);

                        this._networkStarving = false;
                    }
                    else
                    {
                        /*
                         * do not attempt to read from empty buffer again
                         */
						// direct err call due to onError not on main thread
						AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "[PCMReaderCallback] !(read > 0)", false);
                        this._networkStarving = true;
                    }
                }
                else
                {
                    /*
                     * do not attempt to read from buffer with error again
                     */
					// direct err call due to onError not on main thread
					AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "[PCMReaderCallback]", false);
                    this._networkStarving = true;
                }
            }
            else
            {
                // starving is checked only once the straming has started, so setting this here should be ok
                this._networkStarving = true;
            }

            if (this._networkStarving)
                // try to mute the channel - should help with repeating the buffer content if there's a netowrk problem
                System.Array.Clear(data, 0, data.Length);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            this.streamDataBytesPinned.Free();
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        const int loopingBufferSamplesCount = 128;
        protected override void StreamStarting(int samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            this._networkStarving = false;

            this.stream_sound_format = sound_format;
            this.bytes_per_sample = AudioStreamSupport.BytesPerSample(this.stream_sound_format);

            var asource = this.GetComponent<AudioSource>();
            asource.clip = AudioClip.Create(this.url, loopingBufferSamplesCount, channels, samplerate, true, this.PCMReaderCallback);
            asource.loop = true;

            asource.Play();

            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.StartRedirect();
        }

        // we are not playing the channel and retrieving decoded frames manually via readData, starving check is handled by readData
        protected override bool StreamStarving() { return this._networkStarving; }

        protected override void StreamPausing(bool pause) { }

        protected override void StreamStopping()
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.StopRedirect();

            var asource = this.GetComponent<AudioSource>();

            asource.Stop();

            Destroy(asource.clip);

            asource.clip = null;
        }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
        {
            LOG(LogLevel.INFO, "Stream samplerate change from {0}", this.GetComponent<AudioSource>().clip.frequency);

            this.StreamStopping();

            this.StreamStarting((int)samplerate, channels, sound_format);

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}", samplerate);
        }

        public override void SetOutput(int outputDriverId)
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.SetOutput(outputDriverId);
        }
        #endregion
    }
}
