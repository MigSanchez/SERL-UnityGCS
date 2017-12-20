// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Needs OnAudioFilterRead somewhere on the GO - this is typically AudioSource, AudioListener or custom OnAudioFilterRead
    /// Saves audio being played on this GO into a WAV file in StreamingAssets with the name with format SoundRecording_{clip name or game object name}_{YYYY}{MM}{DD}_{HH}{MIN}{SS}.wav
    /// </summary>
    public class GOAudioSaveToFile : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        [Header("Setup")]
        [Tooltip("Start saving automatically on Start\r\n\r\n - Saves audio being played on this GO into a WAV file in StreamingAssets folder with the name in format SoundRecording_{clip name or game object name}_{YYYY}{MM}{DD}_{HH}{MIN}{SS}.wav")]
        public bool autoStart = false;
        [Tooltip("If enabled, saves this' GameObject playing audio output (default). Otherwise supply the data to be saved as needed via AddToSave")]
        public bool useThisGameObjectAudio = true;
        #endregion
        // ========================================================================================================================================
        #region File format, I/O
        string saveFileName = string.Empty;
        FileStream f;
        BinaryWriter fp;
        bool savingRunning = false;
        uint datalength = 0;
        uint samplerate;
        ushort channels;
        uint bits;
        #endregion
        // ========================================================================================================================================
        #region Lifecycle
        void Start()
        {
            this.samplerate = (uint)AudioSettings.outputSampleRate;

            if (this.autoStart)
                this.StartSaving();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!this.savingRunning || !this.useThisGameObjectAudio)
                return;

            this.AddToSave(data);
        }

        void OnDisable()
        {
            this.StopSaving();
        }
        #endregion
        // ========================================================================================================================================
        #region Saving
        public void StartSaving()
        {
            var date = DateTime.Now;
            string filename = this.gameObject.name;

            if (this.GetComponent<AudioSource>() != null)
                if (this.GetComponent<AudioSource>().clip != null)
                    filename = this.GetComponent<AudioSource>().clip.name;

            filename = WWW.EscapeURL(filename);

            this.saveFileName = string.Format("SoundRecording_{0}_{1}{2:D2}{3:D2}_{4:D2}{5:D2}{6:D2}.wav"
                , filename
                , date.Year
                , date.Month
                , date.Day
                , date.Hour
                , date.Minute
                , date.Second
                );

            this.saveFileName = Path.Combine(Application.streamingAssetsPath, this.saveFileName);

            this.f = File.Open(this.saveFileName, FileMode.Create);
            this.fp = new BinaryWriter(this.f);

            /*
             * determine signal properties - these _should_ match OAFR
             */
            var aconfig = AudioSettings.GetConfiguration();
            this.channels = (ushort)AudioStreamSupport.ChannelsFromAudioSpeakerMode(aconfig.speakerMode);
            this.samplerate = (uint)aconfig.sampleRate;
            this.bits = 16; // two bytes per sample - 

            /*
            Write out the wav header.  As we don't know the length yet it will be 0.
            */
            WriteWavHeader(fp, 0, this.channels, this.samplerate, this.bits);

            this.savingRunning = true;
        }

        byte[] byteArr = null;
        public void AddToSave(float[] data)
        {
            if (fp == null)
                return;

            int len1 = AudioStreamSupport.FloatArrayToByteArray(data, (uint)data.Length, ref this.byteArr);
            datalength += (uint)len1;
            fp.Write(this.byteArr);
        }

        public void StopSaving()
        {
            if (!this.savingRunning)
                return;

            this.savingRunning = false;

            if (this.fp != null)
            {
                /*
                Write back the wav header now that we know its length.
                */
                WriteWavHeader(fp, (int)datalength, this.channels, this.samplerate, this.bits);

                fp.Close();

                fp = null;

                if (this.f != null)
                    f.Close();

                f = null;
            }
        }
        #endregion
        // ========================================================================================================================================
        #region WAV file / basic format structure
        /*
        WAV Structures
        */
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct RiffChunk
        {
            public char[] id;
            public int size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct FmtChunk
        {
            public RiffChunk chunk;
            public ushort wFormatTag;       /* format type  */
            public ushort nChannels;        /* number of channels (i.e. mono, stereo...)  */
            public uint nSamplesPerSec;     /* sample rate  */
            public uint nAvgBytesPerSec;    /* for buffer estimation  */
            public ushort nBlockAlign;      /* block size of data  */
            public ushort wBitsPerSample;   /* number of bits per sample of mono data */
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct DataChunk
        {
            public RiffChunk chunk;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct WavHeader
        {
            public RiffChunk chunk;
            public char[] rifftype;
        };

        void WriteWavHeader(BinaryWriter fp, int length, ushort channels, uint rate, uint bits)
        {
            fp.Seek(0, SeekOrigin.Begin);

            FmtChunk fmtChunk = new FmtChunk();
            fmtChunk.chunk = new RiffChunk();
            fmtChunk.chunk.id = new char[] { 'f', 'm', 't', ' ' };
            fmtChunk.chunk.size = Marshal.SizeOf(typeof(FmtChunk)) - Marshal.SizeOf(typeof(RiffChunk));
            fmtChunk.wFormatTag = 1;
            fmtChunk.nChannels = channels;
            fmtChunk.nSamplesPerSec = rate;
            fmtChunk.nAvgBytesPerSec = rate * channels * bits / 8;
            fmtChunk.nBlockAlign = (ushort)(1 * channels * bits / 8);
            fmtChunk.wBitsPerSample = (ushort)bits;

            DataChunk dataChunk = new DataChunk();
            dataChunk.chunk = new RiffChunk();
            dataChunk.chunk.id = new char[] { 'd', 'a', 't', 'a' };
            dataChunk.chunk.size = length;

            WavHeader wavHeader = new WavHeader();
            wavHeader.chunk = new RiffChunk();
            wavHeader.chunk.id = new char[] { 'R', 'I', 'F', 'F' };
            wavHeader.chunk.size = Marshal.SizeOf(typeof(FmtChunk)) + Marshal.SizeOf(typeof(RiffChunk)) + length;
            wavHeader.rifftype = new char[] { 'W', 'A', 'V', 'E' };

            /*
            Write out the WAV header.
            */
            fp.Write(wavHeader.chunk.id);
            fp.Write(wavHeader.chunk.size);
            fp.Write(wavHeader.rifftype);

            fp.Write(fmtChunk.chunk.id);
            fp.Write(fmtChunk.chunk.size);
            fp.Write(fmtChunk.wFormatTag);
            fp.Write(fmtChunk.nChannels);
            fp.Write(fmtChunk.nSamplesPerSec);
            fp.Write(fmtChunk.nAvgBytesPerSec);
            fp.Write(fmtChunk.nBlockAlign);
            fp.Write(fmtChunk.wBitsPerSample);

            fp.Write(dataChunk.chunk.id);
            fp.Write(dataChunk.chunk.size);
        }

        #endregion
    }
}