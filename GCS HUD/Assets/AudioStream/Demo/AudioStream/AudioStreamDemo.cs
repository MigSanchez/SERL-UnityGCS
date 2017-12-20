// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using AudioStream;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class AudioStreamDemo : MonoBehaviour
{
    public AudioStreamBase[] audioStreams;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        this.streamsStatesFromEvents[goName] = "playing";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }

    public void OnPlaybackStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "stopped";
    }

    public void OnTagChanged(string goName, string _key, string _value)
    {
        // care only about 'meaningful' tags
        var key = _key.ToLower();

        if (key == "artist" || key == "title")
        {
            // little juggling around dictionaries..

            if (this.tags.ContainsKey(goName))
                this.tags[goName][_key] = _value;
            else
                this.tags[goName] = new Dictionary<string, string>() { { _key, _value } };
        }
    }

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    #endregion
    /// <summary>
    /// try to make font visible on high DPI resolutions
    /// </summary>
    int dpiMult = 1;

    void Start()
    {
        if (Screen.dpi > 300) // ~~ retina
            this.dpiMult = 2;
    }

    GUIStyle guiStyleLabelSmall = null;
    GUIStyle guiStyleLabelMiddle = null;
    GUIStyle guiStyleLabelNormal = null;
    GUIStyle guiStyleButtonNormal = null;

    void OnGUI()
    {
        if (this.guiStyleLabelSmall == null)
        {
            this.guiStyleLabelSmall = new GUIStyle(GUI.skin.GetStyle("Label"));
            this.guiStyleLabelSmall.fontSize = 7 * this.dpiMult;
            this.guiStyleLabelSmall.margin = new RectOffset(0, 0, 0, 0);
        }

        if (this.guiStyleLabelMiddle == null)
        {
            this.guiStyleLabelMiddle = new GUIStyle(GUI.skin.GetStyle("Label"));
            this.guiStyleLabelMiddle.fontSize = 8 * this.dpiMult;
        }

        if (this.guiStyleLabelNormal == null)
        {
            this.guiStyleLabelNormal = new GUIStyle(GUI.skin.GetStyle("Label"));
            this.guiStyleLabelNormal.fontSize = 11 * this.dpiMult;
            this.guiStyleLabelNormal.margin = new RectOffset(0, 0, 0, 0);
        }

        if (this.guiStyleButtonNormal == null)
        {
            this.guiStyleButtonNormal = new GUIStyle(GUI.skin.GetStyle("Button"));
            this.guiStyleButtonNormal.fontSize = 11 * this.dpiMult;
        }

        GUILayout.Label("", this.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", this.guiStyleLabelSmall);
        GUILayout.Label("AudioStream v " + AudioStream.About.version + " © 2016, 2017 Martin Cvengros, using FMOD Studio by Firelight Technologies" + (this.audioStreams != null && this.audioStreams.Length > 0 ? " " + this.audioStreams[0].fmodVersion : ""), this.guiStyleLabelMiddle);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, this.guiStyleLabelNormal);

        GUI.color = Color.white;

        foreach (var audioStream in this.audioStreams)
        {
            FMOD.RESULT lastError;
            string lastErrorString = audioStream.GetLastError(out lastError);

            GUILayout.Label(audioStream.GetType() + "   ========================================", this.guiStyleLabelSmall);

            GUILayout.Label("Stream: " + audioStream.url, this.guiStyleLabelNormal);
            GUILayout.Label(string.Format("State = {0} {1} {2} {3}"
                , audioStream.isPlaying ? "Playing" + (audioStream.isPaused ? " / Paused" : "") : "Stopped"
                , audioStream.starving ? "(STARVING)" : ""
                , lastError + " " + lastErrorString
                , audioStream.deviceBusy ? "(refreshing)" : ""
                )
                , this.guiStyleLabelNormal);
            GUILayout.Label(string.Format("Buffer Percentage = {0}", audioStream.bufferFillPercentage), this.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Volume: ", this.guiStyleLabelNormal);

            if (audioStream is AudioStream.AudioStream)
            {
                var _as = (audioStream as AudioStream.AudioStream).GetComponent<AudioSource>();
                _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
                GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", this.guiStyleLabelNormal);
            }
            else
            {
                var _as = (audioStream as AudioStream.AudioStreamMinimal);
                _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
                GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", this.guiStyleLabelNormal);
            }

            GUILayout.EndHorizontal();

            /*
             * this for testing stream type 
             * 
             * GUILayout.BeginHorizontal();
             * audioStream.streamType = (AudioStreamBase.StreamAudioType)GUILayout.SelectionGrid((int)audioStream.streamType, System.Enum.GetNames(typeof(AudioStreamBase.StreamAudioType)), 5);
             * GUILayout.EndHorizontal();
            */

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(audioStream.isPlaying ? "Stop" : "Play", this.guiStyleButtonNormal))
                if (audioStream.isPlaying)
                    audioStream.Stop();
                else
                    audioStream.Play();

            if (audioStream.isPlaying)
            {
                if (GUILayout.Button(audioStream.isPaused ? "Resume" : "Pause", this.guiStyleButtonNormal))
                    if (audioStream.isPaused)
                        audioStream.Pause(false);
                    else
                        audioStream.Pause(true);
            }

            GUILayout.EndHorizontal();

            Dictionary<string, string> _tags;
            if (this.tags.TryGetValue(audioStream.name, out _tags))
                foreach (var d in _tags)
                    GUILayout.Label(d.Key + ": " + d.Value, this.guiStyleLabelNormal);
        }
    }
}
