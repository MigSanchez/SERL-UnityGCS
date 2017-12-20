// (c) 2016, 2017 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD Studio by Firelight Technologies

// custom editor for conditional displaying of fields in the editor by Mr.Jwolf ( thank you Mr.Jwolf whoever you are )
// https://forum.unity3d.com/threads/inspector-enum-dropdown-box-hide-show-variables.83054/#post-951401

// Directivity texture visualization from Resonance Audio for Unity
// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioStream.AudioStreamBase), true)]
[CanEditMultipleObjects]
public class AudioStreamEditor : Editor
{
    /// <summary>
    /// GVR plugin
    /// </summary>
    Texture2D directivityTexture = null;

    void SetFieldCondition()
    {
        // . custom inspector is sometimes buggily invoked for different base class what
        if (target == null)
            return;

        // the reflection system cares only about the final enum member name
        ShowOnEnum("streamType", "RAW", "RAWSoundFormat");
        ShowOnEnum("streamType", "RAW", "RAWFrequency");
        ShowOnEnum("streamType", "RAW", "RAWChannels");
        ShowOnEnum("speakerMode", "RAW", "numOfRawSpeakers");
    }

    /// <summary>
    /// Use this function to set when witch fields should be visible.
    /// </summary>
    /// <param name='enumFieldName'>
    /// The name of the Enum field.
    /// </param>
    /// <param name='enumValue'>
    /// When the Enum value is this in the editor, the field is visible.
    /// </param>
    /// <param name='fieldName'>
    /// The Field name that should only be visible when the chosen enum value is set.
    /// </param>
    void ShowOnEnum(string enumFieldName, string enumValue, string fieldName)
    {
        p_FieldCondition newFieldCondition = new p_FieldCondition()
        {
            p_enumFieldName = enumFieldName,
            p_enumValue = enumValue,
            p_fieldName = fieldName,
            p_isValid = true

        };

        //Valildating the "enumFieldName"
        newFieldCondition.p_errorMsg = "";
        FieldInfo enumField = target.GetType().GetField(newFieldCondition.p_enumFieldName);
        if (enumField == null)
        {
            newFieldCondition.p_isValid = false;
            newFieldCondition.p_errorMsg = "Could not find a enum-field named: '" + enumFieldName + "' in '" + target + "'. Make sure you have spelled the field name for the enum correct in the script '" + this.ToString() + "'";
        }

        //Valildating the "enumValue"
        if (newFieldCondition.p_isValid)
        {
            var currentEnumValue = enumField.GetValue(target);
            var enumNames = currentEnumValue.GetType().GetFields();
            //var enumNames =currentEnumValue.GetType().GetEnumNames();
            bool found = false;
            foreach (FieldInfo enumName in enumNames)
            {
                if (enumName.Name == enumValue)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                newFieldCondition.p_isValid = false;
                newFieldCondition.p_errorMsg = "Could not find the enum value: '" + enumValue + "' in the enum '" + currentEnumValue.GetType().ToString() + "'. Make sure you have spelled the value name correct in the script '" + this.ToString() + "'";
            }
        }

        //Valildating the "fieldName"
        if (newFieldCondition.p_isValid)
        {
            FieldInfo fieldWithCondition = target.GetType().GetField(fieldName);
            if (fieldWithCondition == null)
            {
                newFieldCondition.p_isValid = false;
                newFieldCondition.p_errorMsg = "Could not find the field: '" + fieldName + "' in '" + target + "'. Make sure you have spelled the field name correct in the script '" + this.ToString() + "'";
            }
        }

        if (!newFieldCondition.p_isValid)
        {
            newFieldCondition.p_errorMsg += "\nYour error is within the Custom Editor Script to show/hide fields in the inspector depending on the an Enum." +
                    "\n\n" + this.ToString() + ": " + newFieldCondition.ToStringFunction() + "\n";
        }

        fieldConditions.Add(newFieldCondition);
    }

    List<p_FieldCondition> fieldConditions;
    public void OnEnable()
    {
        fieldConditions = new List<p_FieldCondition>();
        SetFieldCondition();

        this.directivityTexture = Texture2D.blackTexture;
    }

    public override void OnInspectorGUI()
    {
        // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
        serializedObject.Update();

        var obj = serializedObject.GetIterator();

        if (obj.NextVisible(true))
        {
            // GVR plugin
            float? directivity = null;
            float? directivitySharpness = null;

            // Loops through all visible fields
            do
            {
                bool shouldBeVisible = true;
                // Tests if the field is a field that should be hidden/shown due to the enum value
                foreach (var fieldCondition in fieldConditions)
                {
                    //If the fieldcondition isn't valid, display an error msg.
                    if (!fieldCondition.p_isValid)
                    {
                        Debug.LogError(fieldCondition.p_errorMsg);
                    }
                    else if (fieldCondition.p_fieldName == obj.name)
                    {
                        FieldInfo enumField = target.GetType().GetField(fieldCondition.p_enumFieldName);
                        var currentEnumValue = enumField.GetValue(target);
                        //If the enum value isn't equal to the wanted value the field will be set not to show
                        if (currentEnumValue.ToString() != fieldCondition.p_enumValue)
                        {
                            shouldBeVisible = false;
                            break;
                        }
                    }
                }

                if (shouldBeVisible)
                    EditorGUILayout.PropertyField(obj, true);

                // GVR plugin
                // (these should be always visible...)
                if (serializedObject.targetObject.GetType() == typeof(AudioStream.GVRSource))
                {
                    if (obj.name == "directivity")
                        directivity = obj.floatValue;

                    if (obj.name == "directivitySharpness")
                        directivitySharpness = obj.floatValue;

                    if (directivity.HasValue && directivitySharpness.HasValue)
                    {
                        GUI.skin.label.wordWrap = true;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Approximate spatial spread strength of this audio source:");
                        DrawDirectivityPattern(directivity.Value, directivitySharpness.Value,
                                               ResonanceAudio_sourceDirectivityColor,
                                               (int)(3.0f * EditorGUIUtility.singleLineHeight));
                        GUILayout.EndHorizontal();

                        directivity = null;
                        directivitySharpness = null;
                    }
                }

            } while (obj.NextVisible(false));
        }

        // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
        serializedObject.ApplyModifiedProperties();
    }

    class p_FieldCondition
    {
        public string p_enumFieldName { get; set; }
        public string p_enumValue { get; set; }
        public string p_fieldName { get; set; }
        public bool p_isValid { get; set; }
        public string p_errorMsg { get; set; }

        public string ToStringFunction()
        {
            return "'" + p_enumFieldName + "', '" + p_enumValue + "', '" + p_fieldName + "'.";
        }
    }

    /// Source directivity GUI color.
    readonly Color ResonanceAudio_sourceDirectivityColor = 0.65f * Color.blue;

    void DrawDirectivityPattern(float alpha, float sharpness, Color color, int size)
    {
        directivityTexture.Resize(size, size);
        // Draw the axes.
        Color axisColor = color.a * Color.black;
        for (int i = 0; i < size; ++i)
        {
            directivityTexture.SetPixel(i, size / 2, axisColor);
            directivityTexture.SetPixel(size / 2, i, axisColor);
        }
        // Draw the 2D polar directivity pattern.
        float offset = 0.5f * size;
        float cardioidSize = 0.45f * size;
        Vector2[] vertices = this.ResonanceAudio_Generate2dPolarPattern(alpha, sharpness, 180);
        for (int i = 0; i < vertices.Length; ++i)
        {
            directivityTexture.SetPixel((int)(offset + cardioidSize * vertices[i].x),
                                        (int)(offset + cardioidSize * vertices[i].y), color);
        }
        directivityTexture.Apply();
        // Show the texture.
        GUILayout.Box(directivityTexture);
    }

    /// Generates a set of points to draw a 2D polar pattern.
    Vector2[] ResonanceAudio_Generate2dPolarPattern(float alpha, float order, int resolution)
    {
        Vector2[] points = new Vector2[resolution];
        float interval = 2.0f * Mathf.PI / resolution;
        for (int i = 0; i < resolution; ++i)
        {
            float theta = i * interval;
            // Magnitude |r| for |theta| in radians.
            float r = Mathf.Pow(Mathf.Abs((1 - alpha) + alpha * Mathf.Cos(theta)), order);
            points[i] = new Vector2(r * Mathf.Sin(theta), r * Mathf.Cos(theta));
        }
        return points;
    }
}