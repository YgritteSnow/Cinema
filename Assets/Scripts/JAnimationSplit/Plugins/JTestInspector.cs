using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JTest))]
public class JTestInspector : Editor
{
	JTest thisTarget;

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JTest;
	}

	// Update is called once per frame
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		EditorGUILayout.PropertyField(serializedObject.FindProperty("ani_clip"), GUILayout.Width(200));
		serializedObject.ApplyModifiedProperties();

		Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(300));
		thisTarget.ani_path = EditorGUI.TextField(rect, thisTarget.ani_path);

		if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
		 && rect.Contains(Event.current.mousePosition))
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
			if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
			{
				thisTarget.ani_path = DragAndDrop.paths[0];
			}
		}

		if (GUILayout.Button("PlayAt", GUILayout.Width(50)))
		{
			thisTarget.TestAniPlay();
		}
	}
}
