// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using UnityEngine;

namespace AudioStream
{
    public class AudioSourceMute : MonoBehaviour
    {
        [Tooltip("Supress AudioSource signal here.\nNote: this is implemented via OnAudioFilterRead, which might not be optimal - you can consider e.g. mixer routing and supress signal there.")]
        public bool mute = true;

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (mute)
                for (var i = 0; i < data.Length; i++)
                    data[i] = 0;
        }
    }
}