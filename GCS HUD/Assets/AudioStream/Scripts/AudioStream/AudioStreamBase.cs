// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public abstract class AudioStreamBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Required descendant's implementation
        protected abstract void StreamStarting(int samplerate, int channels, FMOD.SOUND_FORMAT sound_format);
        protected abstract bool StreamStarving();
        protected abstract void StreamPausing(bool pause);
        protected abstract void StreamStopping();
        protected abstract void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format);
        public abstract void SetOutput(int outputDriverId);
        #endregion

        // ========================================================================================================================================
        #region Editor

        public enum StreamAudioType
        {
            AUTODETECT      /* let FMOD guess the stream format */
                , MPEG      /* MP2/MP3 MPEG. */
                , OGGVORBIS /* Ogg vorbis. */
                , WAV       /* Microsoft WAV. */
                , RAW       /* Raw PCM data. */
        }

        [Header("[Source]")]

        [Tooltip("Audio stream - such as shoutcast/icecast - direct URL or m3u/8/pls playlist URL,\r\nor direct URL link to a single audio file.\r\n\r\nNOTE: it is possible to stream a local file. Pass complete file path WITHOUT the 'file://' prefix in that case. Stream type is ignored in that case.")]
        public string url = string.Empty;

        [Tooltip("Audio format of the stream\r\n\r\nAutodetect lets FMOD autodetect the stream format and is default and recommended for desktop and Android platforms.\r\n\r\nFor iOS please select correct type - autodetecting there most often does not work.\r\n\r\nBe aware that if you select incorrect format for a given radio/stream you will risk problems such as unability to connect and stop stream.\r\n\r\nFor RAW audio format user must specify at least frequency, no. of channles and byte format.")]
        public StreamAudioType streamType = StreamAudioType.AUTODETECT;

        [Header("[RAW codec parameters]")]
        public FMOD.SOUND_FORMAT RAWSoundFormat = FMOD.SOUND_FORMAT.PCM16;
        public int RAWFrequency = 44100;
        public int RAWChannels = 2;

        [Header("[Setup]")]

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("When checked the stream will play on start. Otherwise use Play() method of this GameObject.")]
        public bool playOnStart = true;

        [Tooltip("Default is fine in most cases")]
        public FMOD.SPEAKERMODE speakerMode = FMOD.SPEAKERMODE.DEFAULT;
        [Tooltip("No. of speakers for RAW speaker mode. You must also provide mix matrix for custom setups,\r\nsee remarks at http://www.fmod.org/documentation/#content/generated/FMOD_SPEAKERMODE.html, \r\nand http://www.fmod.org/documentation/#content/generated/FMOD_Channel_SetMixMatrix.html about how to setup the matrix.")]
        // Specify 0 to ignore; when raw speaker mode is selected that defaults to 2 speakers ( stereo ), unless set by user.
        public int numOfRawSpeakers = 0;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnPlaybackStarted;
        public EventWithStringBoolParameter OnPlaybackPaused;
        public EventWithStringParameter OnPlaybackStopped;
        public EventWithStringStringStringParameter OnTagChanged;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Advanced]")]
        [Tooltip("Do not change this unless you have problems opening certain streamed files over the network.\nGenerally increasing this to some bigger value of few tens of kB should help when having trouble opening the stream with ERR_FILE_COULDNOTSEEK error - this often occurs with e.g. mp3s containing tags with embedded artwork.\nFor more info see https://www.fmod.org/docs/content/generated/FMOD_System_SetFileSystem.html and 'blockalign' parameter discussion.")]
        public int streamBlockAlignment = 16 * 1024;
        [Tooltip("It can take some time until the stream is caught on unreliable/slow network connections. You can increase frame count before giving up here.\r\n\r\nDefault is 60 frames which on reliable network is almost never reached.")]
        public int initialConnectionRetryCount = 60;
        [Tooltip("This is frame count after which the connection is dropped when the network is starving continuosly.\r\nDefault is 300 which for 60 fps means ~ 5 secs.")]
        public int starvingRetryCount = 300;

        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region Init && FMOD structures
        /// <summary>
        /// Component startup sync
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;

        protected FMOD.System system;
        protected FMOD.Sound sound;
        [HideInInspector()]
        public FMOD.Channel channel;
        [HideInInspector()]
        public FMOD.RESULT result = FMOD.RESULT.OK;
        protected FMOD.OPENSTATE openstate = FMOD.OPENSTATE.READY;
        uint version = 0;
        System.IntPtr extradriverdata = System.IntPtr.Zero;

        FMOD.RESULT lastError = FMOD.RESULT.OK;
        const int streamBufferSize = 64 * 1024;

        protected virtual IEnumerator Start()
        {
            this.gameObjectName = this.gameObject.name;

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
                FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
            */
            var versionString = System.Convert.ToString(version, 16).PadLeft(8, '0');
            this.fmodVersion = string.Format("{0}.{1}.{2}", System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

            /*
             * initial internal FMOD samplerate should be 48000 on desktop; we change it on the sound only when stream requests it.
             */
            result = system.setSoftwareFormat(AudioSettings.outputSampleRate, this.speakerMode, this.numOfRawSpeakers);
            ERRCHECK(result, "system.setSoftwareFormat");

            int rate;
            FMOD.SPEAKERMODE sm;
            int smch;

            result = system.getSoftwareFormat(out rate, out sm, out smch);
            ERRCHECK(result, "system.getSoftwareFormat");

            LOG(LogLevel.INFO, "FMOD samplerate: {0}, speaker mode: {1}, num. of raw speakers: {2}", rate, sm, smch);

            // must be be4 init on iOS ...
            //result = system.setOutput(FMOD.OUTPUTTYPE.NOSOUND);
            //ERRCHECK(result, "system.setOutput");

            if (this is AudioStreamMinimal)
                result = system.init(10, FMOD.INITFLAGS.NORMAL, extradriverdata);
            else
                result = system.init(10, FMOD.INITFLAGS.STREAM_FROM_UPDATE, extradriverdata);
            ERRCHECK(result, "system.init");

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

            if (this.playOnStart)
                this.Play();

            yield return null;

            this.ready = true;
        }

        #endregion

        // ========================================================================================================================================
        #region Playback
        [Header("[Playback info]")]
        [Range(0f, 100f)]
        [Tooltip("Set during playback. Stream buffer fullness")]
        public uint bufferFillPercentage = 0;
        [Tooltip("Set during playback.")]
        public bool isPlaying = false;
        [Tooltip("Set during playback.")]
        public bool isPaused = false;
        /// <summary>
        /// starving flag doesn't seem to work without playSound
        /// this is updated from Sound::readData/AudioStream and from getOpenState/AudioStreamMinimal
        /// </summary>
        [Tooltip("Set during playback.")]
        public bool starving = false;
        [Tooltip("Set during playback when stream is refreshing data.")]
        public bool deviceBusy = false;
        [Tooltip("Radio station title. Set from PLS playlist.")]
        public string title;
        [Tooltip("Set during playback.")]
        public int streamChannels;
        //: - [Tooltip("Tags supplied by the stream. Varies heavily from stream to stream")]
        Dictionary<string, string> tags = new Dictionary<string, string>();
        /// <summary>
        /// Don't allow Play / Stop to reenter and interfere with each other
        /// </summary>
        bool inStartup = false;
        /// <summary>
        /// Stop playback after too many dropped frames,
        /// allow for a bit of a grace period during which some loss is recoverable / acceptable
        /// getOpenState and update in base are still OK (connection is open) although playback is finished
        /// starving condition is determined in each descendant individually depending on their method used - see starving flag description
        /// </summary>
        int starvingFrames = 0;

        public void Play()
        {
            if (this.inStartup)
            {
                LOG(LogLevel.ERROR, "In startup - will not Play again.");
                return;
            }

            if (this.isPlaying)
            {
                LOG(LogLevel.WARNING, "Already playing.");
                return;
            }

            if (!this.isActiveAndEnabled)
            {
                LOG(LogLevel.WARNING, "Will not start on disabled GameObject.");
                return;
            }

            /*
             * url format check
             */
            if (string.IsNullOrEmpty(this.url))
            {
                var msg = "Can't stream empty URL";

                LOG(LogLevel.ERROR, msg);

                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);

                return;
            }

            if (this.url.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase) && (this.streamType != StreamAudioType.OGGVORBIS && this.streamType != StreamAudioType.AUTODETECT))
            {
                var msg = "It looks like you're trying to play OGGVORBIS stream, but have not selected proper 'Stream Type'. This might result in various problems while playing and stopping unsuccessful connection with this setup.";

                LOG(LogLevel.ERROR, msg);

                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);

                return;
            }

            this.tags = new Dictionary<string, string>();

            this.isPlaying = false;
            this.isPaused = false;
            this.inStartup = true;
            this.starvingFrames = 0;

            StartCoroutine(this.PlayCR());
        }

        enum PlaylistType
        {
            PLS
                , M3U
                , M3U8
        }

        PlaylistType? playlistType;

        IEnumerator PlayCR()
        {
            var _url = this.url;

            this.playlistType = null;

            if (this.url.EndsWith("pls", System.StringComparison.OrdinalIgnoreCase))
                this.playlistType = PlaylistType.PLS;
            else if (this.url.EndsWith("m3u", System.StringComparison.OrdinalIgnoreCase))
                this.playlistType = PlaylistType.M3U;
            else if (this.url.EndsWith("m3u8", System.StringComparison.OrdinalIgnoreCase))
                this.playlistType = PlaylistType.M3U8;

            if (this.playlistType.HasValue)
            {
                string playlist = string.Empty;

                // allow local playlist
                if (!this.url.StartsWith("http", System.StringComparison.OrdinalIgnoreCase) && !this.url.StartsWith("file", System.StringComparison.OrdinalIgnoreCase))
                    this.url = "file://" + this.url;

                /*
                 * UnityWebRequest introduced in 5.2, but WWW still worked on standalone/mobile
                 * However, in 5.3 is WWW hardcoded to Abort() on iOS on non secure requests - which is likely a bug - so from 5.3 on we require UnityWebRequest
                 */
#if UNITY_5_3_OR_NEWER
#if UNITY_5_3
                using (UnityEngine.Experimental.Networking.UnityWebRequest www = UnityEngine.Experimental.Networking.UnityWebRequest.Get(this.url))
#else
                using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(this.url))
#endif
                {
                    LOG(LogLevel.INFO, "Retrieving {0}", this.url);

#if UNITY_2017_2_OR_NEWER
                    yield return www.SendWebRequest();
#else
                    yield return www.Send();
#endif

                    if (
#if UNITY_2017_1_OR_NEWER
                        www.isNetworkError
#else
                        www.isError
#endif
                        || !string.IsNullOrEmpty(www.error)
                        )
                    {
                        var msg = string.Format("Can't read playlist from {0} - {1}", this.url, www.error);

                        LOG(LogLevel.ERROR, msg);

                        this.inStartup = false;

                        if (this.OnError != null)
                            this.OnError.Invoke(this.gameObjectName, msg);

                        yield break;
                    }

                    playlist = www.downloadHandler.text;
                }
#else
                using (WWW www = new WWW(this.url))
                {
					LOG(LogLevel.INFO, "Retrieving {0}", this.url );

                    yield return www;

                    if (!string.IsNullOrEmpty(www.error))
                    {
                        var msg = string.Format("Can't read playlist from {0} - {1}", this.url, www.error);

                        LOG(LogLevel.ERROR, msg);
                
                        this.inStartup = false;
                
                        if (this.OnError != null)
                            this.OnError.Invoke(this.gameObjectName, msg);

                        yield break;
                    }

                    playlist = www.text;
                }
#endif
                // TODO: 
                // - relative entries
                // - recursive entries
                // - AAC - streaming chunks ?

                if (this.playlistType.Value == PlaylistType.M3U
            || this.playlistType.Value == PlaylistType.M3U8)
                {
                    _url = this.URLFromM3UPlaylist(playlist);
                    LOG(LogLevel.INFO, "URL from M3U/8 playlist: {0}", _url);
                }
                else
                {
                    _url = this.URLFromPLSPlaylist(playlist);
                    LOG(LogLevel.INFO, "URL from PLS playlist: {0}", _url);
                }

                if (string.IsNullOrEmpty(_url))
                {
                    var msg = string.Format("Can't parse playlist {0}", this.url);

                    LOG(LogLevel.ERROR, msg);

                    this.inStartup = false;

                    if (this.OnError != null)
                        this.OnError.Invoke(this.gameObjectName, msg);

                    yield break;
                }

                // allow FMOD to stream locally
                if (_url.StartsWith("file://", System.StringComparison.OrdinalIgnoreCase))
                    _url = _url.Substring(7);
            }


            /*
             * opening flags for streaming createSound
             */
            var flags = FMOD.MODE.CREATESTREAM
                | FMOD.MODE.NONBLOCKING
                ;

            // ref. http://www.fmod.org/questions/question/releasing-soundsystem-on-stream-which-couldnt-been-opened/
            // .open only does not play well with PCMReaderCallback/readData on iOS.
            // if (this is AudioStream)
            //    flags |= FMOD.MODE.OPENONLY;

            /*
             * pass empty / default CREATESOUNDEXINFO, otherwise it hits nomarshalable unmanaged structure path on IL2CPP 
             */
            var extInfo = new FMOD.CREATESOUNDEXINFO();
            // must be hinted on iOS due to ERR_FILE_COULDNOTSEEK on getOpenState
            // allow any type for local files
            switch (this.streamType)
            {
                case StreamAudioType.MPEG:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.MPEG;
                    break;
                case StreamAudioType.OGGVORBIS:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.OGGVORBIS;
                    break;
                case StreamAudioType.WAV:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.WAV;
                    break;
                case StreamAudioType.RAW:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.RAW;

                    // raw data needs to ignore audio format and
                    // Use FMOD_CREATESOUNDEXINFO to specify format.Requires at least defaultfrequency, numchannels and format to be specified before it will open.Must be little endian data.
                    flags |= FMOD.MODE.OPENRAW;

                    extInfo.format = this.RAWSoundFormat;
                    extInfo.defaultfrequency = this.RAWFrequency;
                    extInfo.numchannels = this.RAWChannels;

                    break;

                default:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.UNKNOWN;
                    break;
            }

            extInfo.cbsize = Marshal.SizeOf(extInfo);

            /*
             * Additional streaming setup
             */

            /* Increase the file buffer size a little bit to account for Internet lag. */
            result = system.setStreamBufferSize(streamBufferSize, FMOD.TIMEUNIT.RAWBYTES);
            ERRCHECK(result, "system.setStreamBufferSize");

            /* tags ERR_FILE_COULDNOTSEEK:
                http://stackoverflow.com/questions/7154223/streaming-mp3-from-internet-with-fmod
                http://www.fmod.org/docs/content/generated/FMOD_System_SetFileSystem.html
                */
            result = system.setFileSystem(null, null, null, null, null, null, this.streamBlockAlignment);
            ERRCHECK(result, "system.setFileSystem");


            /*
             * Start streaming
             */
            result = system.createSound(_url
                , flags
                , ref extInfo
                , out sound);
            ERRCHECK(result, "system.createSound");


            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                LOG(LogLevel.INFO, "Setting playback output to speaker...");
                iOSSpeaker.RouteForPlayback();
            }

            LOG(LogLevel.INFO, "About to play...");

            yield return StartCoroutine(this.StreamCR());
        }

        IEnumerator StreamCR()
        {
            var isNetworkSource = this.url.StartsWith("http");

            for (; ; )
            {
                if (this.isPaused)
                    yield return null;


                result = system.update();
                ERRCHECK(result, null, false);

                result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                ERRCHECK(result, null, false);

                LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);

                if (!this.isPlaying)
                {
                    int c = 0;
                    do
                    {
                        result = system.update();
                        ERRCHECK(result, null, false);

                        result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                        ERRCHECK(result, null, false);

                        LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);

                        if (result == FMOD.RESULT.OK && openstate == FMOD.OPENSTATE.READY)
                        {
                            /*
                             * stream caught
                             */
                            FMOD.SOUND_TYPE _streamType;
                            FMOD.SOUND_FORMAT _streamFormat;
                            int _streamBits;

                            result = sound.getFormat(out _streamType, out _streamFormat, out this.streamChannels, out _streamBits);
                            ERRCHECK(result, null);

                            float freq; int prio;
                            result = sound.getDefaults(out freq, out prio);
                            ERRCHECK(result, null);

                            LOG(LogLevel.INFO, "Stream format {0} {1} {2} channels {3} bits {4} samplerate", _streamType, _streamFormat, this.streamChannels, _streamBits, freq);

                            this.StreamStarting((int)freq, this.streamChannels, _streamFormat);

                            this.isPlaying = true;
                            this.inStartup = false;
                            this.starvingFrames = 0;

                            if (this.OnPlaybackStarted != null)
                                this.OnPlaybackStarted.Invoke(this.gameObjectName);

                            break;
                        }
                        else
                        {
                            /*
                             * Unable to stream
                             */
                            if (++c > this.initialConnectionRetryCount)
                            {
                                if (isNetworkSource)
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Please make sure that correct audio type of stream is selected, network is reachable and possibly check Advanced setting.");
#if UNITY_EDITOR
                                    LOG(LogLevel.ERROR, "If everything seems to be ok, restarting the editor often helps while having trouble connecting to especially OGG streams.");
#endif
                                }
                                else
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Unrecognized audio type.");
                                }

                                // clear this since not started yet to allow trying stopping the stream
                                this.inStartup = false;

                                this.Stop();

                                yield break;
                            }
                        }

                        yield return new WaitForSeconds(0.1f);

                    } while (result != FMOD.RESULT.OK || openstate != FMOD.OPENSTATE.READY);
                }

                if (this.StreamStarving())
                {
                    LOG(LogLevel.DEBUG, "Starving frame: {0}", this.starvingFrames);

                    if (++this.starvingFrames > this.starvingRetryCount)
                    {
                        LOG(LogLevel.WARNING, "Stream buffer starving - stopping playback");

                        this.Stop();

                        yield break;
                    }
                }
                else
                {
                    this.starvingFrames = 0;
                }

                FMOD.TAG streamTag;
                /*
                    Read any tags that have arrived, this could happen if a radio station switches
                    to a new song.
                */

                // Have to use FMOD >= 1.10.01 for tags to work - https://github.com/fmod/UnityIntegration/pull/11
                while (sound.getTag(null, -1, out streamTag) == FMOD.RESULT.OK)
                {
                    /*
                     * do some tag examination and logging for unhandled tag types
                     * special FMOD tag type for detecting sample rate change
                     */

                    string tagName = (string)streamTag.name;
                    string tagData = null;

                    if (streamTag.type == FMOD.TAGTYPE.FMOD)
                    {
                        /* When a song changes, the samplerate may also change, so update here. */
                        if (tagName == "Sample Rate Change")
                        {
                            // TODO: actual float and samplerate change test - is there a way to test this ?

                            // resampling is done via the AudioClip - but we have to recreate it for AudioStream ( will cause noticeable pop/pause, but there's probably no other way )
                            // , do it via direct calls without events

                            // float frequency = *((float*)streamTag.data);
                            float[] frequency = new float[1];
                            Marshal.Copy(streamTag.data, frequency, 0, sizeof(float));

                            // get current sound_format
                            FMOD.SOUND_TYPE _streamType;
                            FMOD.SOUND_FORMAT _streamFormat;
                            int _streamBits;
                            result = sound.getFormat(out _streamType, out _streamFormat, out this.streamChannels, out _streamBits);
                            ERRCHECK(result, null);

                            this.StreamChanged(frequency[0], streamChannels, _streamFormat);
                        }
                    }
                    else
                    {
                        switch (streamTag.datatype)
                        {
                            case FMOD.TAGDATATYPE.BINARY:
                                tagData = Marshal.PtrToStructure(streamTag.data, typeof(bool)).ToString();
                                break;

                            case FMOD.TAGDATATYPE.CDTOC:
                                tagData = "FMOD.TAGDATATYPE.CDTOC";
                                break;

                            case FMOD.TAGDATATYPE.FLOAT:
                                float f;
                                if (float.TryParse(Marshal.PtrToStructure(streamTag.data, typeof(float)).ToString(), out f))
                                    tagData = f.ToString();
                                else
                                    tagData = "FMOD.TAGDATATYPE.FLOAT";
                                break;

                            case FMOD.TAGDATATYPE.INT:
                                int i;
                                if (int.TryParse(Marshal.PtrToStructure(streamTag.data, typeof(int)).ToString(), out i))
                                    tagData = i.ToString();
                                else
                                    tagData = "FMOD.TAGDATATYPE.INT";
                                break;

                            case FMOD.TAGDATATYPE.STRING:

                                tagData = AudioStreamSupport.stringFromNative(streamTag.data);
                                this.tags[tagName] = tagData;

                                if (this.OnTagChanged != null)
                                    this.OnTagChanged.Invoke(this.gameObjectName, tagName, tagData);

                                break;

                            case FMOD.TAGDATATYPE.STRING_UTF16BE:
                            case FMOD.TAGDATATYPE.STRING_UTF8:

                                tagData = AudioStreamSupport.stringFromNative(streamTag.data);
                                this.tags[tagName] = tagData;

                                if (this.OnTagChanged != null)
                                    this.OnTagChanged.Invoke(this.gameObjectName, tagName, tagData);

                                break;
                        }
                    }

                    LOG(LogLevel.INFO, "{0} tag: {1}, value: {2}", streamTag.type, tagName, tagData);
                }

                yield return null;
            }
        }

        public void Pause(bool pause)
        {
            if (this.inStartup)
            {
                LOG(LogLevel.WARNING, "Starting up..");
                return;
            }

            if (!this.isPlaying)
            {
                LOG(LogLevel.WARNING, "Not playing..");
                return;
            }

            this.StreamPausing(pause);

            this.isPaused = pause;

            LOG(LogLevel.INFO, "{0}", this.isPaused ? "paused." : "resumed.");

            if (this.OnPlaybackPaused != null)
                this.OnPlaybackPaused.Invoke(this.gameObjectName, this.isPaused);
        }

        #endregion

        // ========================================================================================================================================
        #region Shutdown
        /// <summary>
        /// wrong combination of requested audio type and actual stream type leads to still BUFFERING/LOADING state of the stream
        /// don't release sound and system in that case and notify user
        /// </summary>
        bool unstableShutdown = false;

        public void Stop()
        {
            if (this.inStartup)
            {
                LOG(LogLevel.ERROR, "In startup - won't be stopping now.");
                return;
            }

            LOG(LogLevel.INFO, "Stopping..");

            this.StreamStopping();

            this.StopAllCoroutines();

            this.bufferFillPercentage = 0;
            this.isPlaying = false;
            this.isPaused = false;
            this.inStartup = false;
            this.starving = false;
            this.deviceBusy = false;
            this.tags = new Dictionary<string, string>();

            /*
             * try to release FMOD sound resources
             */

            /*
             * Stop the channel, then wait for it to finish opening before we release it.
             */
            if (channel.hasHandle())
            {
                result = channel.stop();
                ERRCHECK(result, "channel.stop", false);

                channel.clearHandle();
            }

            /*
             * If the sound is still buffering at this point (but not trying to connect without available connection), we can't do much - namely we can't release sound and system since FMOD deadlocks in this state
             * This happens when requesting wrong audio type for stream.
             */
            this.unstableShutdown = false;

            result = FMOD.RESULT.OK;
            openstate = FMOD.OPENSTATE.READY;

            if (system.hasHandle())
            {
                result = system.update();
                ERRCHECK(result, "system.update", false);
            }

            if (sound.hasHandle())
            {
                result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                ERRCHECK(result, "sound.getOpenState", false);

                LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);
            }

            if (openstate == FMOD.OPENSTATE.BUFFERING || openstate == FMOD.OPENSTATE.LOADING)
            {
                // If buffering not on wrong stream type but on unaccessible network, release normally
                if (result != FMOD.RESULT.ERR_NET_URL
                    && result != FMOD.RESULT.ERR_NET_CONNECT
                    && result != FMOD.RESULT.ERR_NET_SOCKET_ERROR
                    && result != FMOD.RESULT.ERR_NET_WOULD_BLOCK
                    )
                {
                    this.unstableShutdown = true;
                    LOG(LogLevel.ERROR, "AudioStreamer is in unstable state - sound (and system) won't be released which might lead to memory leaks / exhaustion of resources / instabilities on subsequent attempts to play something or disabling/destroying this game object. [{0} {1}]", openstate, result);
                }
            }

            /*
             * Shut down
             */
            if (sound.hasHandle() && !this.unstableShutdown)
            {
                result = sound.release();
                ERRCHECK(result, "sound.release", false);

                sound.clearHandle();
            }

            if (this.OnPlaybackStopped != null)
                this.OnPlaybackStopped.Invoke(this.gameObjectName);
        }

        protected virtual void OnDisable()
        {
            this.Stop();

            if (system.hasHandle() && !this.unstableShutdown)
            {
                result = system.close();
                ERRCHECK(result, "system.close", false);

                result = system.release();
                ERRCHECK(result, "system.release", false);

                system.clearHandle();
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Support

        public void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            if (throwOnError)
            {
                try
                {
                    AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
                }
                catch (System.Exception ex)
                {
                    // clear the startup flag only when requesting abort on error
                    this.inStartup = false;
                    throw ex;
                }
            }
            else
            {
                AudioStreamSupport.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
            }
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

        /// <summary>
        /// M3U/8 = its own simple format: https://en.wikipedia.org/wiki/M3U
        /// </summary>
        /// <param name="_playlist"></param>
        /// <returns></returns>
        string URLFromM3UPlaylist(string _playlist)
        {
            System.IO.StringReader source = new System.IO.StringReader(_playlist);

            string s = source.ReadLine();
            while (s != null)
            {
                // If the read line isn't a metadata, it's a file path
                if ((s.Length > 0) && (s[0] != '#'))
                    return s;

                s = source.ReadLine();
            }

            return null;
        }

        /// <summary>
        /// PLS ~~ INI format: https://en.wikipedia.org/wiki/PLS_(file_format)
        /// </summary>
        /// <param name="_playlist"></param>
        /// <returns></returns>
        string URLFromPLSPlaylist(string _playlist)
        {
            System.IO.StringReader source = new System.IO.StringReader(_playlist);

            string s = source.ReadLine();

            int equalIndex;
            while (s != null)
            {
                if (s.Length > 4)
                {
                    // If the read line isn't a metadata, it's a file path
                    if ("FILE" == s.Substring(0, 4).ToUpper())
                    {
                        equalIndex = s.IndexOf("=") + 1;
                        s = s.Substring(equalIndex, s.Length - equalIndex);

                        return s;
                    }
                }

                s = source.ReadLine();
            }

            return null;
        }
        #endregion

        // ========================================================================================================================================
        #region User support
        /// <summary>
        /// Enumerates available audio outputs in the system and returns their names.
        /// </summary>
        /// <returns></returns>
        public List<string> AvailableOutputs()
        {
            if (!system.hasHandle())
            {
                LOG(LogLevel.ERROR, "AudioSourceOutputDevice not properly initialized before usage.");
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
