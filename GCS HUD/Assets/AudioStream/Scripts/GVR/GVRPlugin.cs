// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public class GVRPlugin
    {
        // ========================================================================================================================================
        #region Editor simulacrum
        LogLevel logLevel = LogLevel.INFO;
        string gameObjectName = "GVR Plugin";
        #endregion

        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = this.result;
            AudioStreamSupport.ERRCHECK(this.result, this.logLevel, this.gameObjectName, null, customMessage, throwOnError);
        }

        void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            AudioStreamSupport.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, null, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }
        #endregion

        // ========================================================================================================================================
        #region FMOD
        FMOD.System system;
        FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;
        #endregion

        // ========================================================================================================================================
        #region FMOD nested plugins
        uint gvrPlugin_handle = 0;

        const int GVRListener_nestedPluginID = 0;
        const int GVRListener_paramID_Gain = 0;
        const int GVRListener_paramID_RoomProperties = 1;

        public FMOD.DSP GVRListener_DSP;


        const int GVRSoundfield_nestedPluginID = 1;
        const int GVRSoundfield_paramID_Gain = 0;
        const int GVRSoundfield_paramID_3DAttributes = 1;

        public FMOD.DSP GVRSoundfield_DSP;


        const int GVRSource_nestedPluginID = 2;
        const int GVRSource_paramID_Gain = 0;
        const int GVRSource_paramID_Spread = 1;
        const int GVRSource_paramID_MinDistance = 2;
        const int GVRSource_paramID_MaxDistance = 3;
        const int GVRSource_paramID_DistanceRolloff = 4;
        const int GVRSource_paramID_Occlusion = 5;
        const int GVRSource_paramID_Directivity = 6;
        const int GVRSource_paramID_DirectivitySharpness = 7;
        const int GVRSource_paramID_3DAttributes = 8;
        const int GVRSource_paramID_BypassRoom = 9;

        public FMOD.DSP GVRSource_DSP;


        [System.Serializable()]
        public enum DistanceRolloff
        {
            LINEAR = 0
                , LOGARITHMIC = 1
                , OFF = 2
        }
        #endregion

        public GVRPlugin(FMOD.System forSystem, LogLevel _logLevel)
        {
            this.system = forSystem;
            this.logLevel = _logLevel;

            /*
             * Load GVR plugin
             * On platforms which support it, load dynamically
             * On iOS/tvOS plugin is statically linked, and enabled via Plugins/FMOD/fmodplugins.cpp
             */
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {

                // suppose (somehow naively) that plugins are indexed in order they were registered -

                uint handle; // handlet of hte registered DSP plugin
                /*
                 * check DSP parameters list
                 */
                int numparams; // parameters check fro loaded DSP

                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, 0, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out this.GVRListener_DSP);
                ERRCHECK(result, "system.createDSPByPlugin");

                result = this.GVRListener_DSP.getNumParameters(out numparams);
                ERRCHECK(result, "dsp.getNumParameters");

                for (var p = 0; p < numparams; ++p)
                {
                    FMOD.DSP_PARAMETER_DESC paramdesc;
                    result = this.GVRListener_DSP.getParameterInfo(p, out paramdesc);
                    ERRCHECK(result, "dsp.getParameterInfo");

                    // padded '\0' in unmanaged  marshaled strings completely fuck up managed strings for whatever the fuck reason
                    string p_name = string.Empty; for (var i = 0; i < paramdesc.name.Length; ++i) if (paramdesc.name[i] != '\0') p_name += paramdesc.name[i];
                    string p_label = string.Empty; for (var i = 0; i < paramdesc.label.Length; ++i) if (paramdesc.label[i] != '\0') p_label += paramdesc.label[i];
                    var p_description = paramdesc.description;

                    LOG(LogLevel.DEBUG, "DSP {0} || param: {1} || type: {2} || name: {3} || label: {4} || description: {5}", 0, p, paramdesc.type, p_name, p_label, p_description);
                }





                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, 1, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out this.GVRSoundfield_DSP);
                ERRCHECK(result, "system.createDSPByPlugin");


                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, 2, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out this.GVRSource_DSP);
                ERRCHECK(result, "system.createDSPByPlugin");

            }
            else
            {
                string pluginName = string.Empty;
                var pluginsPath = Path.Combine(Application.dataPath, "Plugins");
                bool arch64 = AudioStreamSupport.Is64bitArchitecture();

                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.LinuxEditor:
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "x86_64"), "gvraudio");
                        else
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "x86"), "gvraudio");
                        break;

                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.LinuxPlayer:
                        pluginName = Path.Combine(pluginsPath, "gvraudio");
                        break;

                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer:
                        pluginName = Path.Combine(pluginsPath, "gvraudio.bundle");
                        break;

                    case RuntimePlatform.Android:
                        /*
                         * load library with fully qualified name from hinted folder
                         */
                        pluginsPath = Path.Combine(Application.dataPath, "lib");

                        result = system.setPluginPath(pluginsPath);
                        ERRCHECK(result, "system.setPluginPath");

                        pluginName = "libgvraudio.so";
                        break;

                    default:
                        throw new NotSupportedException("Platform not supported.");
                }

                LOG(LogLevel.DEBUG, "Loading '{0}'", pluginName);

                result = system.loadPlugin(pluginName, out this.gvrPlugin_handle);
                ERRCHECK(result, "system.loadPlugin");

                /*
                 * Create DSPs from all nested plugins, test enumerate && info for parameters
                 */
                int numNestedPlugins;
                result = system.getNumNestedPlugins(this.gvrPlugin_handle, out numNestedPlugins);
                ERRCHECK(result, "system.getNumNestedPlugins");

                LOG(LogLevel.DEBUG, "Got {0} nested plugins", numNestedPlugins);

                for (var n = 0; n < numNestedPlugins; ++n)
                {
                    /*
                     * Load nested plugin
                     */
                    uint nestedHandle;
                    result = system.getNestedPlugin(this.gvrPlugin_handle, n, out nestedHandle);
                    ERRCHECK(result, "system.getNestedPlugin");

                    FMOD.PLUGINTYPE pluginType;
                    int namelen = 255;
                    string dspPluginName;
                    uint version;
                    result = system.getPluginInfo(nestedHandle, out pluginType, out dspPluginName, namelen, out version);

                    LOG(LogLevel.DEBUG, "DSP {0} || plugin type: {1} || plugin name: {2} || version: {3}", n, pluginType, dspPluginName, version);

                    /*
                     * Create DSP effect
                     */
                    FMOD.DSP dsp;
                    result = system.createDSPByPlugin(nestedHandle, out dsp);
                    ERRCHECK(result, "system.createDSPByPlugin");

                    /*
                     * dsp.getInfo seems to be unused
                     */

                    /*
                     * check DSP parameters list
                     */
                    int numparams;
                    result = dsp.getNumParameters(out numparams);
                    ERRCHECK(result, "dsp.getNumParameters");

                    for (var p = 0; p < numparams; ++p)
                    {
                        FMOD.DSP_PARAMETER_DESC paramdesc;
                        result = dsp.getParameterInfo(p, out paramdesc);
                        ERRCHECK(result, "dsp.getParameterInfo");

                        // padded '\0' in unmanaged  marshaled strings completely fuck up managed strings for whatever the fuck reason
                        string p_name = string.Empty; for (var i = 0; i < paramdesc.name.Length; ++i) if (paramdesc.name[i] != '\0') p_name += paramdesc.name[i];
                        string p_label = string.Empty; for (var i = 0; i < paramdesc.label.Length; ++i) if (paramdesc.label[i] != '\0') p_label += paramdesc.label[i];
                        var p_description = paramdesc.description;

                        LOG(LogLevel.DEBUG, "DSP {0} || param: {1} || type: {2} || name: {3} || label: {4} || description: {5}", n, p, paramdesc.type, p_name, p_label, p_description);
                    }

                    /*
                     * save DSPs
                     */
                    if (dspPluginName.ToString() == "Google GVR Listener")
                        GVRListener_DSP = dsp;

                    if (dspPluginName.ToString() == "Google GVR Soundfield")
                        GVRSoundfield_DSP = dsp;

                    if (dspPluginName.ToString() == "Google GVR Source")
                        GVRSource_DSP = dsp;
                }
            }
        }

        public void Release()
        {
            result = this.GVRListener_DSP.disconnectAll(true, true);
            ERRCHECK(result, "dsp.disconnectAll", false);

            result = this.GVRSoundfield_DSP.disconnectAll(true, true);
            ERRCHECK(result, "dsp.disconnectAll", false);

            result = this.GVRSource_DSP.disconnectAll(true, true);
            ERRCHECK(result, "dsp.disconnectAll", false);

            result = this.GVRListener_DSP.release();
            ERRCHECK(result, "dsp.release", false);

            result = this.GVRSoundfield_DSP.release();
            ERRCHECK(result, "dsp.release", false);

            result = this.GVRSource_DSP.release();
            ERRCHECK(result, "dsp.release", false);

            // this call caused too much issues
            // it was not possible to call it cleanly - with success return code -, and after invoked Unity crashed occasionally.
            // I suspect/hope FMOD cleans everything when releasing system, and not calling it won't cause further issues with unreleased memory in the editor.
            // result = this.system.unloadPlugin(this.gvrPlugin_handle);
            // ERRCHECK(result, "system.unloadPlugin", false);
        }

        // ========================================================================================================================================
        #region GVRListener
        public void GVRListener_SetGain(float gain)
        {
            result = GVRListener_DSP.setParameterFloat(GVRListener_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRListener_GetGain()
        {
            float fvalue;
            result = GVRListener_DSP.getParameterFloat(GVRListener_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRListener_SetRoomProperties()
        {
            // TODO: finish room

            // Set the room properties to a null room, which will effectively disable the room effects.
            result = GVRListener_DSP.setParameterData(GVRListener_paramID_RoomProperties, IntPtr.Zero.ToBytes(0));
            ERRCHECK(result, "dsp.setParameterData", false);
        }
        #endregion

        // ========================================================================================================================================
        #region GVRSoundfield
        public void GVRSoundfield_SetGain(float gain)
        {
            result = GVRSoundfield_DSP.setParameterFloat(GVRSoundfield_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSoundfield_GetGain()
        {
            float fvalue;
            result = GVRSoundfield_DSP.getParameterFloat(GVRSoundfield_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSoundfield_Set3DAttributes(Vector3 relative_position, Vector3 relative_velocity, Vector3 relative_forward, Vector3 relative_up
            , Vector3 absolute_position, Vector3 absolute_velocity, Vector3 absolute_forward, Vector3 absolute_up)
        {
            FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI attributes = new FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI();

            attributes.numlisteners = 1;

            attributes.relative = new FMOD.ATTRIBUTES_3D[1];
            attributes.relative[0].position = relative_position.ToFMODVector();
            attributes.relative[0].velocity = relative_velocity.ToFMODVector();
            attributes.relative[0].forward = relative_forward.ToFMODVector();
            attributes.relative[0].up = relative_up.ToFMODVector();

            attributes.weight = new float[1];
            attributes.weight[0] = 1f;

            attributes.absolute.position = absolute_position.ToFMODVector();
            attributes.absolute.velocity = absolute_velocity.ToFMODVector();
            attributes.absolute.forward = absolute_forward.ToFMODVector();
            attributes.absolute.up = absolute_up.ToFMODVector();

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            int attributes_size = Marshal.SizeOf(attributes);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(attributes, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = this.GVRSource_DSP.setParameterData(GVRSource_paramID_3DAttributes, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI));

            Marshal.FreeHGlobal(attributes_ptr);
        }
        #endregion

        // ========================================================================================================================================
        #region GVRSource
        public void GVRSource_SetGain(float gain)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetGain()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_SetSpread(float spread)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_Spread, spread);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetSpread()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_Spread, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_SetMinDistance(float mindistance)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_MinDistance, mindistance);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetMinDistance()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_MinDistance, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_SetMaxDistance(float maxdistance)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_MaxDistance, maxdistance);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetMaxDistance()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_MaxDistance, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_SetDistanceRolloff(DistanceRolloff distanceRolloff)
        {
            result = GVRSource_DSP.setParameterInt(GVRSource_paramID_DistanceRolloff, (int)distanceRolloff);
            ERRCHECK(result, "dsp.setParameterInt", false);
        }

        public DistanceRolloff GVRSource_GetDistanceRolloff()
        {
            int ivalue;
            result = GVRSource_DSP.getParameterInt(GVRSource_paramID_DistanceRolloff, out ivalue);
            ERRCHECK(result, "dsp.getParameterInt", false);

            return (DistanceRolloff)ivalue;
        }

        public void GVRSource_SetOcclusion(float occlusion)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_Occlusion, occlusion);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetOcclusion()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_Occlusion, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_SetDirectivity(float directivity)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_Directivity, directivity);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetDirectivity()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_Directivity, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_SetDirectivitySharpness(float directivitySharpness)
        {
            result = GVRSource_DSP.setParameterFloat(GVRSource_paramID_DirectivitySharpness, directivitySharpness);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float GVRSource_GetDirectivitySharpness()
        {
            float fvalue;
            result = GVRSource_DSP.getParameterFloat(GVRSource_paramID_DirectivitySharpness, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void GVRSource_Set3DAttributes(Vector3 relative_position, Vector3 relative_velocity, Vector3 relative_forward, Vector3 relative_up
            , Vector3 absolute_position, Vector3 absolute_velocity, Vector3 absolute_forward, Vector3 absolute_up)
        {
            FMOD.DSP_PARAMETER_3DATTRIBUTES attributes = new FMOD.DSP_PARAMETER_3DATTRIBUTES();

            attributes.relative.position = relative_position.ToFMODVector();
            attributes.relative.velocity = relative_velocity.ToFMODVector();
            attributes.relative.forward = relative_forward.ToFMODVector();
            attributes.relative.up = relative_up.ToFMODVector();

            attributes.absolute.position = absolute_position.ToFMODVector();
            attributes.absolute.velocity = absolute_velocity.ToFMODVector();
            attributes.absolute.forward = absolute_forward.ToFMODVector();
            attributes.absolute.up = absolute_up.ToFMODVector();

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            int attributes_size = Marshal.SizeOf(attributes);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(attributes, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = this.GVRSource_DSP.setParameterData(GVRSource_paramID_3DAttributes, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_3DATTRIBUTES));

            Marshal.FreeHGlobal(attributes_ptr);
        }

        public void GVRSource_SetBypassRoom(bool bypassRoom)
        {
            result = GVRSource_DSP.setParameterBool(GVRSource_paramID_BypassRoom, bypassRoom);
            ERRCHECK(result, "dsp.setParameterBool", false);
        }

        public bool GVRSource_GetBypassRoom()
        {
            bool bvalue;
            result = GVRSource_DSP.getParameterBool(GVRSource_paramID_BypassRoom, out bvalue);
            ERRCHECK(result, "dsp.getParameterBool", false);

            return bvalue;
        }

        #endregion
    }
}