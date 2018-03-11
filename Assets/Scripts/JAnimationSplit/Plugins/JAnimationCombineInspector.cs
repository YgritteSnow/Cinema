using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JAnimationCombine))]
public class JAnimationCombineInspector : Editor
{
	JAnimationCombine thisTarget;

	string m_outputPath = "Assets/JAnimation/JAnimationSplit/SplitedAnim";
	Rect m_outputRect;

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JAnimationCombine;
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.LabelField("OutputPath:");
		m_outputRect = EditorGUILayout.GetControlRect(GUILayout.Width(300));
		m_outputPath = EditorGUI.TextField(m_outputRect, m_outputPath);

		if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
		 && m_outputRect.Contains(Event.current.mousePosition))
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
			if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
			{
				m_outputPath = DragAndDrop.paths[0];
			}
		}

		int list_count = int.Parse(EditorGUILayout.TextField("CombineFiles", thisTarget.m_combineAnims.Length.ToString()));
		if (list_count != thisTarget.m_combineAnims.Length)
		{
			string[] new_list = new string[list_count];
			System.Array.Copy(thisTarget.m_combineAnims, new_list, Mathf.Min(list_count, thisTarget.m_combineAnims.Length));
			thisTarget.m_combineAnims = new_list;
		}
		for(int i = 0; i != thisTarget.m_combineAnims.Length; ++i)
		{
			Rect rec = EditorGUILayout.GetControlRect(GUILayout.Width(300));
			thisTarget.m_combineAnims[i] = EditorGUI.TextField(rec, thisTarget.m_combineAnims[i]);

			if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
			 && rec.Contains(Event.current.mousePosition))
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
				if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
				{
					thisTarget.m_combineAnims[i] = DragAndDrop.paths[0];
				}
			}
		}

		if (GUILayout.Button("Combine"))
		{
			AnimationClip ani = thisTarget.DoCombine();
			JAnimationUtility.SaveAni(ani, m_outputPath, "_comb");
			JAnimationUtility.SaveAssets();
		}
	}
}
