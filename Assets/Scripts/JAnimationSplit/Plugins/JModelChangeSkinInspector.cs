using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JModelChangeSkin))]
public class JModelChangeSkinInspector : Editor
{
	JModelChangeSkin thisTarget;

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JModelChangeSkin;
	}

	// Update is called once per frame
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.PropertyField(serializedObject.FindProperty("obj"), GUILayout.Width(200));
		serializedObject.ApplyModifiedProperties();

		if (GUILayout.Button("CopySkinnedMesh", GUILayout.Width(50)))
		{
			thisTarget.CopySkinnedMesh();
		}
	}
}
