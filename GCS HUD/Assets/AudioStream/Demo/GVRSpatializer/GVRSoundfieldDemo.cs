// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using UnityEngine;

[ExecuteInEditMode()]
public class GVRSoundfieldDemo : MonoBehaviour
{
    /// <summary>
    /// Demo references - this just to display FMOD version..
    /// </summary>
    public AudioStream.GVRSoundfield gVRSoundfield;
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

        GUILayout.Label("", this.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", this.guiStyleLabelSmall);
        GUILayout.Label("AudioStream v " + AudioStream.About.version + " © 2016, 2017 Martin Cvengros, using FMOD Studio by Firelight Technologies" + (this.gVRSoundfield ? " " + this.gVRSoundfield.fmodVersion : ""), this.guiStyleLabelMiddle);
    }
}
