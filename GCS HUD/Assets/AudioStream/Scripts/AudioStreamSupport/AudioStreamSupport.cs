// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System;
using UnityEngine;
using UnityEngine.Events;

namespace AudioStream
{
    // ========================================================================================================================================
    public static class About
    {
        public static string version = "1.7.2";
    }

    // ========================================================================================================================================
    #region Unity events
    [System.Serializable]
    public class EventWithStringParameter : UnityEvent<string> { };
    [System.Serializable]
    public class EventWithStringBoolParameter : UnityEvent<string, bool> { };
    [System.Serializable]
    public class EventWithStringStringParameter : UnityEvent<string, string> { };
    [System.Serializable]
    public class EventWithStringStringStringParameter : UnityEvent<string, string, string> { };
    #endregion

    public enum LogLevel
    {
        ERROR = 0
            , WARNING = 1 << 0
            , INFO = 1 << 1
            , DEBUG = 1 << 2
    }

    public static class AudioStreamSupport
    {
        // ========================================================================================================================================
        #region Logging
        public static void ERRCHECK(
            FMOD.RESULT result
            , LogLevel currentLogLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , string customMessage
            , bool throwOnError = true
            )
        {
            if (result != FMOD.RESULT.OK)
            {
                var m = string.Format("{0} {1} - {2}", customMessage, result, FMOD.Error.String(result));

                if (throwOnError)
                    throw new System.Exception(m);
                else
                    LOG(LogLevel.ERROR, currentLogLevel, gameObjectName, onError, m);
            }
            else
            {
                LOG(LogLevel.DEBUG, currentLogLevel, gameObjectName, onError, "{0} {1} - {2}", customMessage, result, FMOD.Error.String(result));
            }
        }

        public static void LOG(
            LogLevel requestedLogLevel
            , LogLevel currentLogLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , string format
            , params object[] args
            )
        {
            if (requestedLogLevel == LogLevel.ERROR)
            {
                var time = DateTime.Now.ToString("s");
                var msg = string.Format(format, args);

                Debug.LogError(
                    gameObjectName + " [ERROR][" + time + "] " + msg + "\r\n==============================================\r\n"
                    );

                if (onError != null)
                    onError.Invoke(gameObjectName, msg);
            }
            else if (currentLogLevel >= requestedLogLevel)
            {
                var time = DateTime.Now.ToString("s");

                if (requestedLogLevel == LogLevel.WARNING)
                    Debug.LogWarningFormat(
                        gameObjectName + " [WARNING][" + time + "] " + format + "\r\n==============================================\r\n"
                        , args);
                else
                    Debug.LogFormat(
                        gameObjectName + " [" + requestedLogLevel + "][" + time + "] " + format + "\r\n==============================================\r\n"
                        , args);
            }
        }
        #endregion

        // ========================================================================================================================================
        #region audio byte array
        public static byte BytesPerSample(FMOD.SOUND_FORMAT sound_format)
        {
            switch (sound_format)
            {
                case FMOD.SOUND_FORMAT.PCM8:
                    return 1;
                case FMOD.SOUND_FORMAT.PCM16:
                    return 2;
                case FMOD.SOUND_FORMAT.PCM24:
                    return 3;
                case FMOD.SOUND_FORMAT.PCM32:
                    return 4;
                case FMOD.SOUND_FORMAT.PCMFLOAT:
                    return 4;
                default:
                    return 0;
            }
        }
        /// <summary>
        /// stream -> Unity
        /// </summary>
        /// <param name="byteArray"></param>
        /// <param name="byteArray_length"></param>
        /// <param name="resultFloatArray"></param>
        /// <returns></returns>
        public static int ByteArrayToFloatArray(byte[] byteArray, uint byteArray_length, byte bytes_per_value, FMOD.SOUND_FORMAT sound_format, ref float[] resultFloatArray)
        {
            if (resultFloatArray == null || resultFloatArray.Length != (byteArray_length / bytes_per_value))
                resultFloatArray = new float[byteArray_length / bytes_per_value];

            int arrIdx = 0;
            for (int i = 0; i < byteArray_length; i += bytes_per_value)
            {
                var barr = new byte[bytes_per_value];
                for (int ii = 0; ii < bytes_per_value; ++ii) barr[ii] = byteArray[i + ii];

                if (sound_format == FMOD.SOUND_FORMAT.PCMFLOAT)
                {
                    resultFloatArray[arrIdx++] = BitConverter.ToSingle(barr, 0);
                }
                else
                {
                    var f = BytesToFloat(barr);

                    switch (sound_format)
                    {
                        case FMOD.SOUND_FORMAT.PCM8:
                            // PCM8 is unsigned:
                            if (f > 127)
                                f = f - 255f;
                            f = f / (float)127;
                            break;
                        case FMOD.SOUND_FORMAT.PCM16:
                            f = (Int16)f / (float)Int16.MaxValue;
                            break;
                        case FMOD.SOUND_FORMAT.PCM24:
                            if (f > 8388607)
                                f = f - 16777215;
                            f = f / (float)8388607;
                            break;
                        case FMOD.SOUND_FORMAT.PCM32:
                            f = (Int32)f / (float)Int32.MaxValue;
                            break;
                    }

                    resultFloatArray[arrIdx++] = f;
                }
            }

            return resultFloatArray.Length;
        }

        static float BytesToFloat(byte firstByte, byte secondByte)
        {
            return (float)((short)((int)secondByte << 8 | (int)firstByte << 0)) / 32768f;
        }

        static float BytesToFloat(byte[] arr)
        {
            int result = 0;
            for ( int i = 0; i < arr.Length; ++i)
                result |= ((int)arr[i] << (8 * i));

			return (float)(short)result;
        }

        /// <summary>
        /// Unity -> stream
        /// </summary>
        /// <param name="floatArray"></param>
        /// <param name="floatArray_length"></param>
        /// <param name="resultByteArray"></param>
        /// <returns></returns>
        public static int FloatArrayToByteArray(float[] floatArray, uint floatArray_length, ref byte[] resultByteArray)
        {
            if (resultByteArray == null || resultByteArray.Length != (floatArray_length * 2))
                resultByteArray = new byte[floatArray_length * 2];

            for (int i = 0; i < floatArray_length; ++i)
            {
                var bArr = FloatToByteArray(floatArray[i] * 32768f);

                resultByteArray[i * 2] = bArr[0];
                resultByteArray[(i * 2) + 1] = bArr[1];
            }

            return resultByteArray.Length;
        }

        static byte[] FloatToByteArray(float _float)
        {
            var result = new byte[2];

            var fa = (UInt16)(_float);
            byte b0 = (byte)(fa >> 8);
            byte b1 = (byte)(fa & 0xFF);

            result[0] = b1;
            result[1] = b0;

            return result;

            // BitConverter preserves endianess, but is slower..
            // return BitConverter.GetBytes(Convert.ToInt16(_float));
        }

        #endregion

        // ========================================================================================================================================
        #region Channel count conversions
        public static int ChannelsFromAudioSpeakerMode(FMOD.SPEAKERMODE sm)
        {
            switch (sm)
            {
                case FMOD.SPEAKERMODE._5POINT1:
                    return 6;
                case FMOD.SPEAKERMODE._7POINT1:
                    return 8;
                case FMOD.SPEAKERMODE.MONO:
                    return 1;
                case FMOD.SPEAKERMODE.QUAD:
                    return 4;
                case FMOD.SPEAKERMODE.RAW:
                    return 2; // hard to say what a sane value is in this case; TODO: let the use specify
                case FMOD.SPEAKERMODE.STEREO:
                    return 2;
                case FMOD.SPEAKERMODE.SURROUND:
                    return 5;
                default:
                    return 2; // hard to say what a sane value is in this case; TODO: let the use specify
            }
        }

        public static int ChannelsFromAudioSpeakerMode(AudioSpeakerMode sm)
        {
            switch (sm)
            {
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Prologic:
                    return 2;
                case AudioSpeakerMode.Quad:
                    return 4;
                case AudioSpeakerMode.Raw:
                    return 2; // hard to say what a sane value is in this case; TODO: let the use specify
                case AudioSpeakerMode.Stereo:
                    return 2;
                case AudioSpeakerMode.Surround:
                    return 5;
                default:
                    return 2; // hard to say what a sane value is in this case; TODO: let the use specify
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Platform
        /// <summary>
        /// returns true if the project is running on 64-bit architecture, false if 32-bit 
        /// </summary>
        /// <returns></returns>
        public static bool Is64bitArchitecture()
        {
            int sizeOfPtr = System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr));
            return (sizeOfPtr > 4);
        }

        /// <summary>
        /// Gets string from native pointer - adopted from FMOD StringHelper which is not public
        /// </summary>
        /// <param name="nativePtr"></param>
        /// <returns></returns>
        public static string stringFromNative(IntPtr nativePtr)
        {
            if (nativePtr == IntPtr.Zero)
            {
                return "";
            }

            int strlen = 0;
            while (System.Runtime.InteropServices.Marshal.ReadByte(nativePtr, strlen) != 0)
            {
                strlen++;
            }
            if (strlen > 0)
            {
                byte[] buffer = new byte[128];

                if (buffer.Length < strlen)
                {
                    int newLength = buffer.Length;
                    while (newLength < strlen)
                    {
                        newLength *= 2;
                    }
                    buffer = new byte[newLength];
                }
                System.Runtime.InteropServices.Marshal.Copy(nativePtr, buffer, 0, strlen);
                return System.Text.Encoding.UTF8.GetString(buffer, 0, strlen);
            }
            else
            {
                return "";
            }
        }
        #endregion
    }
}
