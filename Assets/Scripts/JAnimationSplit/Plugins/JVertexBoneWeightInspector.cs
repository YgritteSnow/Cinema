using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JVertexBoneWeight))]
public class JVertexBoneWeightInspector : Editor {
	string selecting_name = "";

	JVertexBoneWeight thisTarget;

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JVertexBoneWeight;
	}

	// Update is called once per frame
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.PropertyField(serializedObject.FindProperty("focus_bone"));
		serializedObject.ApplyModifiedProperties();
		if (thisTarget.focus_bone != null && thisTarget.focus_bone.name != selecting_name)
		{
			selecting_name = thisTarget.focus_bone.name;
			thisTarget.ShowNode(selecting_name);
		}
	}
}
