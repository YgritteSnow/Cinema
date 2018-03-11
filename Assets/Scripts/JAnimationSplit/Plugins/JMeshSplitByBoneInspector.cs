using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JMeshSplitByBone))]
public class JMeshSplitByBoneInspector : Editor
{
	JMeshSplitByBone thisTarget;

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JMeshSplitByBone;
	}

	// Update is called once per frame
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.PropertyField(serializedObject.FindProperty("obj"), GUILayout.Width(200));
		serializedObject.ApplyModifiedProperties();

		int list_count = int.Parse(EditorGUILayout.TextField("FocusBones", thisTarget.m_focus_bones.Length.ToString()));
		if (list_count != thisTarget.m_focus_bones.Length)
		{
			string[] new_list = new string[list_count];
			System.Array.Copy(thisTarget.m_focus_bones, new_list, Mathf.Min(list_count, thisTarget.m_focus_bones.Length));
			thisTarget.m_focus_bones = new_list;
		}
		for (int i = 0; i != thisTarget.m_focus_bones.Length; ++i)
		{
			string new_str = GUILayout.TextField(thisTarget.m_focus_bones[i] == null ? "" : thisTarget.m_focus_bones[i]);
			if (new_str != thisTarget.m_focus_bones[i])
			{
				thisTarget.m_focus_bones[i] = new_str;
			}
		}

		if (GUILayout.Button("Split", GUILayout.Width(50)))
		{
			thisTarget.DoSplit();
		}
	}
}
