using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JAnimationStatistics))]
public class JAnimationStatisticsInspector : Editor
{
	JAnimationStatistics thisTarget;
	string m_inputPath = "";

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JAnimationStatistics;
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.LabelField("Scale:");
		thisTarget.m_scale = GUILayout.HorizontalSlider(thisTarget.m_scale, 0, 1, GUILayout.Width(100));

		EditorGUILayout.LabelField("InputPath:");
		Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(300));
		m_inputPath = EditorGUI.TextField(rect, m_inputPath);

		if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
		 && rect.Contains(Event.current.mousePosition))
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
			if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
			{
				m_inputPath = DragAndDrop.paths[0];
			}
		}

		if(GUILayout.Button("Statistics"))
		{
			thisTarget.DoStatistics(m_inputPath);
		}
	}
}
