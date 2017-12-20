// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class OutputDeviceDemo : MonoBehaviour
{
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<string> availableOutputs = new List<string>();

    public AudioStream.AudioStream audioStream;
    public AudioSourceOutputDevice audioSourceOutput;

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
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 should be system default
    int previousSelectedOutput = 0;

    IEnumerator Start()
    {
        if (Screen.dpi > 300) // ~~ retina
            this.dpiMult = 2;

        while (!this.audioStream.ready || !this.audioSourceOutput.ready)
            yield return null;

        // check for available outputs
        // (does not matter which instance of FMOD will be checked)
        if (Application.isPlaying)
        {
            string msg = "Available outputs:" + System.Environment.NewLine;

            this.availableOutputs = this.audioStream.AvailableOutputs(); // or this.audioSourceOutput.AvailableOutputs();

            for (int i = 0; i < this.availableOutputs.Count; ++i)
                msg += i.ToString() + " : " + this.availableOutputs[i] + System.Environment.NewLine;

            Debug.Log(msg);
        }
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
        GUILayout.Label("AudioStream v " + AudioStream.About.version + " © 2016, 2017 Martin Cvengros, using FMOD Studio by Firelight Technologies" + (this.audioStream ? " " + this.audioStream.fmodVersion : ""), this.guiStyleLabelMiddle);

        GUILayout.Label("Available output devices:", this.guiStyleLabelNormal);

        // selection of available audio outputs at runtime
        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.ToArray(), this.availableOutputs.Count);

        if (this.selectedOutput != this.previousSelectedOutput)
        {
            if (Application.isPlaying)
            {
                this.audioStream.SetOutput(this.selectedOutput);

                this.audioSourceOutput.SetOutput(this.selectedOutput);
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, this.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString = this.audioStream.GetLastError(out lastError);

        GUILayout.Label(this.audioStream.GetType() + "   ========================================", this.guiStyleLabelSmall);

        GUILayout.Label("Stream: " + this.audioStream.url, this.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0} {1} {2} {3}"
            , this.audioStream.isPlaying ? "Playing" + (this.audioStream.isPaused ? " / Paused" : "") : "Stopped"
            , this.audioStream.starving ? "(STARVING)" : ""
            , lastError + " " + lastErrorString
            , this.audioStream.deviceBusy ? "(refreshing)" : ""
            )
            , this.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Buffer Percentage = {0}", this.audioStream.bufferFillPercentage), this.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", this.guiStyleLabelNormal);

        var _as = this.audioStream.GetComponent<AudioSource>();
        _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", this.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.audioStream.isPlaying ? "Stop" : "Play", this.guiStyleButtonNormal))
            if (this.audioStream.isPlaying)
                this.audioStream.Stop();
            else
                this.audioStream.Play();

        if (this.audioStream.isPlaying)
        {
            if (GUILayout.Button(this.audioStream.isPaused ? "Resume" : "Pause", this.guiStyleButtonNormal))
                if (this.audioStream.isPaused)
                    this.audioStream.Pause(false);
                else
                    this.audioStream.Pause(true);
        }

        GUILayout.EndHorizontal();

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(audioStream.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, this.guiStyleLabelNormal);

        // standalone AudioSource reference
        _as = this.audioSourceOutput.GetComponent<AudioSource>();

        GUILayout.Label(_as.GetType() + "   ========================================", this.guiStyleLabelSmall);

        GUILayout.Label("Clip: " + _as.clip.name, this.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0}"
            , _as.isPlaying ? "Playing" : "Stopped"
            )
            , this.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", this.guiStyleLabelNormal);

        _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", this.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(_as.isPlaying ? "Stop" : "Play", this.guiStyleButtonNormal))
            if (_as.isPlaying)
            {
                _as.Stop();

                this.audioSourceOutput.StopRedirect();

                this.OnPlaybackStopped(_as.gameObject.name);
            }
            else
            {
                this.audioSourceOutput.SetOutput(this.selectedOutput);

                this.audioSourceOutput.StartRedirect();

                _as.Play();

                this.OnPlaybackStarted(_as.gameObject.name);
            }

        GUILayout.EndHorizontal();
    }
}
