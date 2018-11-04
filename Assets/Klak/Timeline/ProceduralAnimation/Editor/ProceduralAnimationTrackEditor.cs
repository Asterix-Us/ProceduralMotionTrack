// Custom timeline track for procedural motion
// https://github.com/keijiro/ProceduralMotionTrack

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Klak.Timeline
{
    [CustomEditor(typeof(ProceduralAnimationTrack))]
    class ProceduralAnimationTrackEditor : Editor
    {
        #region Inspector implementation

        SerializedProperty _componentName;
        SerializedProperty _propertyName;
        SerializedProperty _fieldName;

        // Used in component selection drop-down
        string [] _componentNames;
        GameObject _cachedGameObject;

        // Used in property selection drop-down
        string [] _propertyNames;
        string [] _propertyLabels;
        string [] _fieldNames;
        System.Type _cachedComponentType;

        void OnEnable()
        {
            _componentName = serializedObject.FindProperty("template.componentName");
            _propertyName = serializedObject.FindProperty("template.propertyName");
            _fieldName = serializedObject.FindProperty("template.fieldName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Retrieves the track-bound game object.
            var go = TimelineEditor.inspectedDirector?.
                GetGenericBinding((TrackAsset)target) as GameObject;

            if (go == null)
            {
                // No game object: Simply present a normal text field.
                EditorGUILayout.PropertyField(_componentName);
            }
            else
            {
                // Retrieve and cache components in the game object.
                CacheComponentsInGameObject(go);

                // Component selection drop-down
                var name = _componentName.stringValue;
                var index0 = System.Array.IndexOf(_componentNames, name);
                var index1 = EditorGUILayout.Popup
                    ("Component", Mathf.Max(0, index0), _componentNames);

                // Update the target on selection changes.
                if (index0 != index1)
                {
                    _componentName.stringValue = _componentNames[index1];
                    TimelineEditor.Refresh(RefreshReason.ContentsModified);
                }
            }

            var component = go?.GetComponent(_componentName.stringValue);

            if (component == null)
            {
                // No component selection: Simple present a normal text field.
                EditorGUILayout.PropertyField(_propertyName);
            }
            else
            {
                // Retrieve and cache properties in the component.
                CachePropertiesInComponent(component);

                if (_propertyNames.Length == 0)
                {
                    // There is no supported property in the component.
                    // Clear the property selection.
                    _propertyName.stringValue = "";
                    _fieldName.stringValue = "";
                }
                else
                {
                    // Property selection drop-down
                    var name = _propertyName.stringValue;
                    var index0 = System.Array.IndexOf(_propertyNames, name);
                    var index1 = EditorGUILayout.Popup
                        ("Property", Mathf.Max(index0, 0), _propertyLabels);

                    // Update the target on selection changes.
                    if (index0 != index1)
                    {
                        _propertyName.stringValue = _propertyNames[index1];
                        _fieldName.stringValue = _fieldNames[index1];
                        TimelineEditor.Refresh(RefreshReason.ContentsModified);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Internal method for component/property enumeration

        //
        // Guess a property name from a given field name.
        //
        // As far as we know, there are four types of naming conventions for
        // a serialized field name.
        //
        // - Simple camelCase: "fooBar"
        // - Simple PascalCase: "FooBar"
        // - Space separated: "foo bar"
        // - Hangarion fashioned: "m_fooBar", "_fooBar", "fooBar_"
        //
        // This function converts them into a simple camelCased name.
        //
        static string FieldToPropertyName(string name)
        {
            // Remove Hangarian-fashioned pre/post-fixes.
            if (name.StartsWith("m_"))
                name = name.Substring(2);
            else if (name.StartsWith("_"))
                name = name.Substring(1);
            else if (name.EndsWith("_"))
                name = name.Substring(0, name.Length - 1);

            // Split the name into words and normalize the head characters.
            var words = name.Split();
            for (var i = 0; i < words.Length; i++)
            {
                var w = words[i];
                words[i] = (i == 0 ? System.Char.ToLower(w[0]) :
                                     System.Char.ToUpper(w[0])) + w.Substring(1);
            }
            return string.Join("", words);
        }

        // Enumerate components attached to a given game object.
        void CacheComponentsInGameObject(GameObject go)
        {
            if (_cachedGameObject == go) return;

            _componentNames = go.GetComponents<Component>().
                Select(x => x.GetType().Name).ToArray();

            _cachedGameObject = go;
        }

        // Enumerate component properties that have corresponding serialized
        // fields.
        void CachePropertiesInComponent(Component component)
        {
            var componentType = component.GetType();

            if (_cachedComponentType == componentType) return;

            var itr = (new SerializedObject(component)).GetIterator();

            var pnames = new List<string>();
            var labels = new List<string>();
            var fnames = new List<string>();

            if (itr.NextVisible(true))
            {
                while (true)
                {
                    // Check if the field is supported type.
                    if (itr.propertyType == SerializedPropertyType.Float)
                    {
                        // Check if the field has a corresponding property.
                        var pname = FieldToPropertyName(itr.name);
                        if (componentType.GetProperty(pname) != null)
                        {
                            // Append this field.
                            pnames.Add(pname);
                            labels.Add(itr.displayName);
                            fnames.Add(itr.name);
                        }
                    }

                    if (!itr.NextVisible(false)) break;
                }

                _propertyNames = pnames.ToArray();
                _propertyLabels = labels.ToArray();
                _fieldNames = fnames.ToArray();
            }
            else
            {
                // Failed to retrieve properties.
                _propertyNames = _fieldNames = _propertyLabels = new string [0];
            }

            _cachedComponentType = componentType;
        }

        #endregion
    }
}
