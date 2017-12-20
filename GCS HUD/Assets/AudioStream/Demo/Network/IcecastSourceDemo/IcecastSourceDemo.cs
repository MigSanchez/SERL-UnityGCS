// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStream;
using UnityEngine;

[ExecuteInEditMode()]
public class IcecastSourceDemo : MonoBehaviour
{
    public AudioSource audioSource;
    public IcecastSource icecastSource;

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
        GUILayout.Label("AudioStream v " + AudioStream.About.version + " © 2016, 2017 Martin Cvengros", this.guiStyleLabelMiddle);

        GUILayout.Label(this.audioSource.GetType() + "   ========================================", this.guiStyleLabelSmall);
        if ( this.audioSource.clip != null )
        {
            GUILayout.Label("Source audio: " + this.audioSource.clip.name, this.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Volume: ", this.guiStyleLabelNormal);

            this.audioSource.volume = GUILayout.HorizontalSlider(this.audioSource.volume, 0f, 1f);
            GUILayout.Label(Mathf.Round(this.audioSource.volume * 100f) + " %", this.guiStyleLabelNormal);

            GUILayout.EndHorizontal();
        }

        GUILayout.Label(this.icecastSource.GetType() + "   ========================================", this.guiStyleLabelSmall);

        this.icecastSource.listen = GUILayout.Toggle(this.icecastSource.listen, "Listen here");

        GUILayout.Label("Host: " + this.icecastSource.hostname, this.guiStyleLabelNormal);
        GUILayout.Label("Port: " + this.icecastSource.port, this.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0}"
            , this.icecastSource.Connected ? "Connected" : "Disconnected"
            )
            , this.guiStyleLabelNormal);
    }
}
