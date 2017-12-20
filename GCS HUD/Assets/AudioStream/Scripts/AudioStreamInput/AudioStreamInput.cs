// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioStreamInput : AudioStreamInputBase
    {
        // ========================================================================================================================================
        #region Init && FMOD structures
        protected override IEnumerator Start()
        {
            yield return StartCoroutine(base.Start());
        }
        #endregion

        // ========================================================================================================================================
        #region Recording
        /// <summary>
        /// Create new audio clip with FMOD callback for spatialization. Latency will suffer.
        /// </summary>
        protected override void RecordingStarted()
        {
            var asource = this.GetComponent<AudioSource>();
            const int loopingBufferSamplesCount = 65;       // go for lowest possible reasonable ( i.e. working ) value
            asource.clip = AudioClip.Create(this.AvailableInputs()[this.recordDeviceId], loopingBufferSamplesCount, recChannels, recRate, true, this.PCMReaderCallback);
            asource.loop = true;

            asource.Play();
        }
        /// <summary>
        /// Provide recording data for FMOD callback
        /// </summary>
        protected override void RecordingUpdate()
        {
            result = system.update();
            ERRCHECK(result, "system.update", false);

            uint recordpos = 0;

            system.getRecordPosition(this.recordDeviceId, out recordpos);
            ERRCHECK(result, "system.getRecordPosition");

            if (recordpos != lastrecordpos)
            {
                int blocklength;

                blocklength = (int)recordpos - (int)lastrecordpos;
                if (blocklength < 0)
                {
                    blocklength += (int)soundlength;
                }

                /*
                Lock the sound to get access to the raw data.
                */
                result = sound.@lock((uint)(lastrecordpos * exinfo.numchannels * 2), (uint)(blocklength * exinfo.numchannels * 2), out ptr1, out ptr2, out len1, out len2);   /* * exinfo.numchannels * 2 = stereo 16bit.  1 sample = 4 bytes. */

                /*
                Write it to output.
                */
                if (ptr1.ToInt64() != 0 && len1 > 0)
                {
                    datalength += len1;
                    byte[] barr = new byte[len1];
                    Marshal.Copy(ptr1, barr, 0, (int)len1);

                    this.AddBytesToOutputBuffer(barr);
                }
                if (ptr2.ToInt64() != 0 && len2 > 0)
                {
                    datalength += len2;
                    byte[] barr = new byte[len2];
                    Marshal.Copy(ptr2, barr, 0, (int)len2);
                    this.AddBytesToOutputBuffer(barr);
                }

                /*
                Unlock the sound to allow FMOD to use it again.
                */
                result = sound.unlock(ptr1, ptr2, len1, len2);
            }

            lastrecordpos = recordpos;
        }

        protected override void RecordingStopped()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
                asource.Stop();
        }
        /// <summary>
        /// Satisfy AudioClip data requests
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any processing here, just return them
        /// </summary>
        /// <param name="data"></param>
        void PCMReaderCallback(float[] data)
        {
            if (this.isRecording && sound.hasHandle())
            {
                var fArr = this.GetAudioOutputBuffer((uint)data.Length);

                if (this.isPaused)
                    goto zero;

                if (fArr != null)
                {
                    int length = fArr.Length;
                    for (int i = 0; i < length; ++i)
						data[i] = fArr[i] * this.gain;
                }

                return;
            }

            zero:
            {
                int length = data.Length;
                for (int i = 0; i < length; ++i)
                    data[i] = 0;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        List<byte> outputBuffer = new List<byte>();
        object outputBufferLock = new object();

        void AddBytesToOutputBuffer(byte[] arr)
        {
            lock (this.outputBufferLock)
            {
                outputBuffer.AddRange(arr);
            }
        }

        float[] oafrDataArr = null; // instance buffer
        float[] GetAudioOutputBuffer(uint _len)
        {
            lock (this.outputBufferLock)
            {
                // 2 bytes per 1 value - adjust requested length
                uint len = _len * 2;

                if (len > outputBuffer.Count)
                    return null;

                byte[] bArr = outputBuffer.GetRange(0, (int)len).ToArray();
                outputBuffer.RemoveRange(0, (int)len);

                // input format should be FMOD.SOUND_FORMAT.PCM16 -> 2 bytes per sample
                AudioStreamSupport.ByteArrayToFloatArray(bArr, (uint)bArr.Length, 2, FMOD.SOUND_FORMAT.PCM16, ref oafrDataArr);

                return this.oafrDataArr;
            }
        }

        #endregion
    }
}
