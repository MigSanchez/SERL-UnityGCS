// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioStreamInputDemo : MonoBehaviour
{
    public AudioStreamInput audioStreamInput;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<string> availableInputs = new List<string>();

    #region UI events
    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();

    public void OnRecordingStarted(string goName)
    {
        this.streamsStatesFromEvents[goName] = "recording";
    }

    public void OnRecordingPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "recording";
    }

    public void OnRecordingStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "stopped";
    }

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }
    #endregion
    /// <summary>
    /// try to make font more visible on high DPI resolutions
    /// </summary>
    int dpiMult = 1;
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedInput = 0; // 0 should be system default
    int previousSelectedInput = 0;


    // Use this for initialization
    IEnumerator Start()
    {
        if (Screen.dpi > 300) // ~~ retina
            this.dpiMult = 2;

        while (!this.audioStreamInput.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            this.availableInputs = this.audioStreamInput.AvailableInputs();

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += i.ToString() + " : " + this.availableInputs[i] + System.Environment.NewLine;

            Debug.Log(msg);
        }
    }

    float[] recBuffer = new float[512];

    void Update()
    {
        if (this.audioStreamInput.isRecording)
        {
            // access the recording buffer and look at some values
            this.signalEnergy = 0;

            var _as = this.audioStreamInput.GetComponent<AudioSource>();
            for (int ch = 0; ch < this.audioStreamInput.recChannels; ++ch)
            {
                _as.GetOutputData(this.recBuffer, ch);

                for (int i = 0; i < this.recBuffer.Length; ++i)
                    this.signalEnergy += this.recBuffer[i] * this.recBuffer[i];
            }
        }
    }

    GUIStyle guiStyleLabelSmall = null;
    GUIStyle guiStyleLabelMiddle = null;
    GUIStyle guiStyleLabelNormal = null;
    GUIStyle guiStyleButtonNormal = null;

    float signalEnergy = 0f;

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
        GUILayout.Label("AudioStream v " + AudioStream.About.version + " © 2016, 2017 Martin Cvengros, using FMOD Studio by Firelight Technologies" + (this.audioStreamInput ? " " + this.audioStreamInput.fmodVersion : ""), this.guiStyleLabelMiddle);

        GUILayout.Label("Available recording devices:", this.guiStyleLabelNormal);

        // selection of available audio inputs at runtime
        this.selectedInput = GUILayout.SelectionGrid(this.selectedInput, this.availableInputs.ToArray(), 1);

        if (this.selectedInput != this.previousSelectedInput)
        {
            if (Application.isPlaying)
            {
                this.audioStreamInput.Stop();
                this.audioStreamInput.recordDeviceId = this.selectedInput;
            }

            this.previousSelectedInput = this.selectedInput;
        }

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, this.guiStyleLabelNormal);

        // wait for startup

        if (this.availableInputs.Count > 0)
        {
            GUI.color = Color.white;

            FMOD.RESULT lastError;
            string lastErrorString = this.audioStreamInput.GetLastError(out lastError);

            GUILayout.Label(this.audioStreamInput.GetType() + "   ========================================", this.guiStyleLabelSmall);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.audioStreamInput.isRecording ? "Recording" + (this.audioStreamInput.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , this.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Signal energy from GetOutputData: ");
            GUILayout.Label(this.signalEnergy.ToString());

            GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal ();

			GUILayout.Label("Gain: ", this.guiStyleLabelNormal);

			this.audioStreamInput.gain = GUILayout.HorizontalSlider (this.audioStreamInput.gain, 0f, 1.2f);
			GUILayout.Label(Mathf.Round(this.audioStreamInput.gain * 100f) + " %", this.guiStyleLabelNormal);

			GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.audioStreamInput.isRecording ? "Stop" : "Record", this.guiStyleButtonNormal))
                if (this.audioStreamInput.isRecording)
                    this.audioStreamInput.Stop();
                else
                    StartCoroutine(this.audioStreamInput.Record());

            if (this.audioStreamInput.isRecording)
            {
                if (GUILayout.Button(this.audioStreamInput.isPaused ? "Resume" : "Pause", this.guiStyleButtonNormal))
                    if (this.audioStreamInput.isPaused)
                        this.audioStreamInput.Pause(false);
                    else
                        this.audioStreamInput.Pause(true);
            }

            GUILayout.EndHorizontal();

			this.audioStreamInput.GetComponent<AudioSourceMute>().mute = GUILayout.Toggle (this.audioStreamInput.GetComponent<AudioSourceMute>().mute, "Mute output");
        }
    }
}
