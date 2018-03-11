using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JAnimationMultiplePlay))]
public class JAnimationMultiplePlayInspector : Editor
{
	JAnimationMultiplePlay thisTarget;
	float m_time;

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JAnimationMultiplePlay;
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		int list_count = int.Parse(EditorGUILayout.TextField("AnimationClips:", thisTarget.clips.Length.ToString()));
		thisTarget.ResetAnimCount(list_count);
		for(int i = 0; i != thisTarget.clips.Length; ++i)
		{
			EditorGUILayout.BeginHorizontal();

			MultipleParam mp = thisTarget.clips[i];

			float blend_weight = EditorGUILayout.FloatField(mp.blend_weight, GUILayout.Width(30));
			bool is_normalise = EditorGUILayout.Toggle(mp.is_blend_normalise, GUILayout.Width(10));


			Rect rec = EditorGUILayout.GetControlRect(GUILayout.Width(300));
			string new_clip = EditorGUI.TextField(rec, mp.clip_path);

			if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
			 && rec.Contains(Event.current.mousePosition))
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
				if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
				{
					new_clip = DragAndDrop.paths[0];
				}
			}

			if (new_clip != mp.clip_path || blend_weight != mp.blend_weight || is_normalise != mp.is_blend_normalise)
			{
				thisTarget.ChangeMultipleParam(i, new MultipleParam(new_clip, blend_weight, is_normalise));
			}

			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.BeginHorizontal();
		float new_time = GUILayout.HorizontalSlider(m_time, 0, 1, GUILayout.Width(200));
		thisTarget.is_playing = EditorGUILayout.Toggle(thisTarget.is_playing, GUILayout.Width(10));
		if (GUILayout.Button("StopAt", GUILayout.Width(50)) || new_time != m_time)
		{
			thisTarget.is_playing = false;
			m_time = new_time;
			thisTarget.StopAt(m_time);
		}

		EditorGUILayout.EndHorizontal();
		serializedObject.ApplyModifiedProperties();
	}
}
