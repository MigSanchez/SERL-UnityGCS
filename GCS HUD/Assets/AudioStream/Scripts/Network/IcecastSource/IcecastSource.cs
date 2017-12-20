// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using OggVorbisEncoder;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    public class IcecastSource : MonoBehaviour
    {
        public enum IcecastSourceCodec
        {
            OGGVORBIS
                , PCM // raw PCM data of this GO
        }

        [Header("[Icecast source setup]")]
        [Tooltip("Hostname of the Icecast source to connect to. This should be the same as <listen-socket>::<bind-address> in Icecast config.")]
        public string hostname = "localhost";
        [Tooltip("Port of the Icecast source to connect to. This should be the same as <listen-socket>::<port> in Icecast config.")]
        public ushort port = 8000;
        [Tooltip("Mount point of the Icecast source to connect to. This should be the same as <listen-socket>::<shoutcast-mount> in Icecast config.")]
        public string mountPoint = "/stream";
        /// <summary>
        /// Username for source is not configurable.
        /// </summary>
        string username = "source";
        [Tooltip("Password for source username of the Icecast source to connect to. This should be the same as <authentication>::<source-password> in Icecast config.")]
        public string password = "hackme";
        [Tooltip("Source name - just description on Icecast server")]
        public string sourceName;
        [Tooltip("Source description - just description on Icecast server")]
        public string sourceDescription;
        [Tooltip("Source genre - just description on Icecast server")]
        public string sourceGenre;
        [Tooltip("Server announce url - used only for Icy-url announce in stream tag")]
        public string url;
        // TODO: bitrate derived from source? seems to be computed by Icecast automatically.
        /// <summary>
        /// bitrate
        /// </summary>
        //[Tooltip("Desired bitrate in kbit/s")]
        //public ushort KBitrate = 128;
        /// <summary>
        /// Is this for public stream directory ?
        /// </summary>
        bool _public = false;
        [Tooltip("User agent of this source connection")]
        public string userAgent = "";
        [Tooltip("Default ARTIST tag for this stream which is sent initially. You should call UpdateTags(...) later when streaming.")]
        public string tagDefaultArtist = "DEFAULT_ARTIST";
        [Tooltip("Default TITLE tag for this stream which is sent initially. You should call UpdateTags(...) later when streaming.")]
        public string tagDefaultTitle = "DEFAULT_TITLE";


        [Header("[Audio]")]
        // TODO: custom editor for IcecastSourceCodec
        [Tooltip("Source can be for now pushed as OGGVORBIS encoded or raw PCM data.\r\n\r\nRaw PCM stream does not use any codec, but requires (significantly) higher bandwidth. Client has to configured to have the same signal properties as this machine output, i.e. the same samplerate, channel count and byte format.\r\n\r\nOPUS encoded in OGG container - OGGOPUS - coming later. Note that this format is not supported by FMOD, but can be played in most common streaming clients/browsers.")]
        public IcecastSourceCodec codec = IcecastSourceCodec.OGGVORBIS;
        [Tooltip("Set this to channel count of the audio source.\r\n\r\nNote: OGGVORBIS currently supports only 40k+ Stereo VBR encoding.")]
        public byte channels = 2;
        [Tooltip("If disabled silences sent audio afterwards.")]
        public bool listen = false;

        #region VORBIS/OGGVORBIS settings
        [Range(0f,1f)]
        float vorbis_baseQuality = 0.99f;
        ProcessingState processingState = null;
        OggPage page = null;
        OggStream oggStream = null;
        float[][] vorbis_buffer = null;
        VorbisInfo vorbis_info = null;
        #endregion

        #region OPUS/OGGOPUS settings
        #endregion

        IcecastWriter icecastWriter = null;
        public bool Connected
        {
            get { return this.icecastWriter != null && this.icecastWriter.Connected; }
        }

        void Start()
        {
            string contentType = string.Empty;
            switch (this.codec)
            {
                case IcecastSourceCodec.OGGVORBIS:
                    contentType = "audio/ogg";
                    break;
                case IcecastSourceCodec.PCM:
                    contentType = "audio/raw";
                    break;
            }

            this.icecastWriter = new IcecastWriter()
            {
                Hostname = this.hostname,
                Port = this.port,
                Mountpoint = this.mountPoint,
                Username = this.username,
                Password = this.password,
                ContentType = contentType,
                Name = this.sourceName,
                Description = this.sourceDescription,
                Genre = this.sourceGenre,
                Url = this.url,
                // TODO: bitrate derived from source? seems to be computed by Icecast automatically.
                // , KBitrate = this.KBitrate
                Public = this._public,
                UserAgent = string.IsNullOrEmpty(this.userAgent) ? "AudioStream " + About.version : this.userAgent
            };

            Debug.LogFormat("[{0}:{1}] Testing connection to master server...", this.icecastWriter.Hostname, this.icecastWriter.Port);
            if (!this.icecastWriter.Open())
            {
                Debug.LogErrorFormat("[{0}:{1}] Connection declined: Master server was unavailable", this.icecastWriter.Hostname, this.icecastWriter.Port);
                return;
            }
            else
            {
                Debug.LogFormat("[{0}:{1}] Connection accepted", this.icecastWriter.Hostname, this.icecastWriter.Port);
            }

            switch (this.codec)
            {
                case IcecastSourceCodec.OGGVORBIS:

                    // Stores all the static vorbis bitstream settings

                    // TODO:
                    // Interval 0.3..0.59999 is out of bounds...
                    // IndexOutOfRangeException: Array index is out of range.
                    // OggVorbisEncoder.VorbisInfo.ToneMaskSetup(OggVorbisEncoder.CodecSetup codecSetup, Double toneMaskSetting, Int32 block, OggVorbisEncoder.Setup.Att3[] templatePsyToneMasterAtt, System.Int32[] templatePsyTone0Decibel, OggVorbisEncoder.Setup.AdjBlock[] templatePsyToneAdjLong)
                    // OggVorbisEncoder.VorbisInfo.InitVariableBitRate(Int32 channels, Int32 sampleRate, Single baseQuality)
                    if (this.vorbis_baseQuality >= 0.3f && this.vorbis_baseQuality < 0.6f)
                    {
                        var d1 = Mathf.Abs(this.vorbis_baseQuality - 0.3f);
                        var d2 = Mathf.Abs(this.vorbis_baseQuality - 0.6f);

                        if (d1 < d2)
                            this.vorbis_baseQuality = 0.29999f;
                        else
                            this.vorbis_baseQuality = 0.6f;
                    }

                    // Currently only supports 40k+ Stereo VBR encoding
                    var sr = AudioSettings.outputSampleRate;
                    if (sr < 40000)
                        throw new NotSupportedException("Currently only 40k+ Stereo VBR encoding is supported and the output sample rate is: " + sr);

                    this.vorbis_info = VorbisInfo.InitVariableBitRate(this.channels, AudioSettings.outputSampleRate, this.vorbis_baseQuality);

                    // set up our packet->stream encoder
                    var serial = new System.Random().Next();
                    this.oggStream = new OggStream(serial);

                    // =========================================================
                    // HEADER
                    // =========================================================
                    // Vorbis streams begin with three headers; the initial header (with
                    // most of the codec setup parameters) which is mandated by the Ogg
                    // bitstream spec.  The second header holds any comment fields.  The
                    // third header holds the bitstream codebook.

                    var headerBuilder = new HeaderPacketBuilder();

                    var infoPacket = headerBuilder.BuildInfoPacket(this.vorbis_info);
                    var booksPacket = headerBuilder.BuildBooksPacket(this.vorbis_info);

                    this.oggStream.PacketIn(infoPacket);
                    this.UpdateTags(new Dictionary<string, string>() { { "ARTIST", this.tagDefaultArtist }, { "TITLE", this.tagDefaultTitle } });
                    this.oggStream.PacketIn(booksPacket);

                    // Flush to force audio data onto its own page per the spec
                    while (this.oggStream.PageOut(out page, true))
                    {
                        this.icecastWriter.Push(page.Header);
                        this.icecastWriter.Push(page.Body);
                    }

                    // =========================================================
                    // BODY (Audio Data)
                    // =========================================================
                    this.processingState = ProcessingState.Create(this.vorbis_info);

                    break;
            }
        }

        /// <summary>
        /// This should be called for each tags change when appropriate. You can send any arbitrary tags in dictionary, typically {{"ARTIST":"ARTIST_NAME"},{"TITLE":"TITLE"}, etc. }
        /// </summary>
        /// <param name="withTags"></param>
        public void UpdateTags(Dictionary<string, string> withTags)
        {
            if (this.oggStream != null)
            {
                if (withTags.Count > 0)
                {
                    var headerBuilder = new HeaderPacketBuilder();
                    var comments = new Comments();

                    foreach (var tag in withTags)
                        comments.AddTag(tag.Key, tag.Value);

                    var commentsPacket = headerBuilder.BuildCommentsPacket(comments);
                    this.oggStream.PacketIn(commentsPacket);
                }
            }
        }

        void OnDestroy()
        {
            if (this.processingState != null)
                this.processingState.WriteEndOfStream();

            if (this.icecastWriter != null)
                this.icecastWriter.Close();
        }

        byte[] bArr = null;

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (this.icecastWriter != null && this.icecastWriter.Connected)
            {
                switch (this.codec)
                {
                    case IcecastSourceCodec.OGGVORBIS:

                        if (channels != 2)
                            throw new NotSupportedException("Currently only 40k+ Stereo VBR encoding is supported and the audio does not have 2 channels");

                        var samples = data.Length / 2;

                        if (this.vorbis_buffer == null)
                        {
                            this.vorbis_buffer = new float[this.vorbis_info.Channels][];
                            this.vorbis_buffer[0] = new float[samples];
                            this.vorbis_buffer[1] = new float[samples];
                        }

                        // uninterleave samples
                        for (var i = 0; i < samples; i++)
                        {
                            this.vorbis_buffer[0][i] = data[i * 2];
                            this.vorbis_buffer[1][i] = data[(i * 2) + 1];
                        }

                        this.processingState.WriteData(this.vorbis_buffer, samples);

                        OggPacket packet;
                        if (this.processingState.PacketOut(out packet))
                        {
                            this.oggStream.PacketIn(packet);

							if (this.oggStream.PageOut(out page, true))
                            {
                                this.icecastWriter.Push(page.Header);
                                this.icecastWriter.Push(page.Body);
                            }
                        }

                        break;

                    case IcecastSourceCodec.PCM:

                        AudioStreamSupport.FloatArrayToByteArray(data, (uint)data.Length, ref this.bArr);
                        this.icecastWriter.Push(this.bArr);

                        break;
                }
            }

            if (!this.listen)
                Array.Clear(data, 0, data.Length);
        }
    }
}