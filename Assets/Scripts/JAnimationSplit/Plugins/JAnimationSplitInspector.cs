using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(JAnimationSplit))]
public class JAnimationSplitInspector : Editor {
	//JAnimationSplit thisTarget;

	string m_inputPath = "Assets/JAnimation/JAnimationSplit/Models/Animation";
	string m_outputPath = "Assets/JAnimation/JAnimationSplit/SplitedAnim";

	Rect m_inputRect;
	Rect m_outputRect;

	// Use this for initialization
	void OnEnable ()
	{
		//thisTarget = serializedObject.targetObject as JAnimationSplit;
	}

	// Update is called once per frame
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.LabelField("input:");
		m_inputRect = EditorGUILayout.GetControlRect(GUILayout.Width(300));
		EditorGUILayout.LabelField("output:");
		m_outputRect = EditorGUILayout.GetControlRect(GUILayout.Width(300));
		m_inputPath = EditorGUI.TextField(m_inputRect, m_inputPath);
		m_outputPath = EditorGUI.TextField(m_outputRect, m_outputPath);

		if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
		 && m_inputRect.Contains(Event.current.mousePosition))
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
			if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
			{
				m_inputPath = DragAndDrop.paths[0];
			}
		}
		if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
		 && m_outputRect.Contains(Event.current.mousePosition))
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
			if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
			{
				m_outputPath = DragAndDrop.paths[0];
			}
		}

		if (GUILayout.Button("Split"))
		{
			JAnimationSplit.DoSplitAll(m_inputPath, m_outputPath);
		}
	}
}
