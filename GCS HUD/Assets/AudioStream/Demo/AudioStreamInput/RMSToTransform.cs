// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using UnityEngine;

public class RMSToTransform : MonoBehaviour
{
    const float zeroOffset = 1.5849e-13f;
    const float refLevel = 0.70710678118f; // 1/sqrt(2)
    const float minDB = -60.0f;

    float squareSum;
    int sampleCount;
    float xRot;
    float yRot;

    void Update()
    {
        if (sampleCount < 1) return;

        var rms = Mathf.Min(1.0f, Mathf.Sqrt(squareSum / sampleCount));
        var db = 20.0f * Mathf.Log10(rms / refLevel + zeroOffset);
        // var meter = -Mathf.Log10(0.1f + db / (minDB * 1.1f));
        var someReactiveVariable = db + 60f;

        transform.localScale = Vector3.one * someReactiveVariable;
        transform.localRotation = Quaternion.Euler(0f, (yRot += someReactiveVariable) / 20f, 0f);

        squareSum = 0;
        sampleCount = 0;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (var i = 0; i < data.Length; ++i)
        {
            var level = data[i];
            squareSum += level * level;
        }

        sampleCount += data.Length;
    }
}
