// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using UnityEngine;
using System.Runtime.InteropServices;

public static class iOSSpeaker
{
#if UNITY_IPHONE
	[DllImport("__Internal")]
	private static extern void _RouteForPlayback();
	[DllImport("__Internal")]
	private static extern void _RouteForRecording();
#endif

    public static void RouteForPlayback()
    {
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer)
			_RouteForPlayback();
#endif
    }

    public static void RouteForRecording()
    {
#if UNITY_IPHONE
		if (Application.platform == RuntimePlatform.IPhonePlayer)
			_RouteForRecording();
#endif
    }
}
