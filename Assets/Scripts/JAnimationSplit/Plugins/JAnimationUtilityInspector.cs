using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

[CustomEditor(typeof(JAnimationUtility))]
public class JAnimationUtilityInspector : Editor {
	JAnimationUtility thisTarget;
	Animation m_animation;

	string m_filePath = "E:/WorkProjects/nine_art/trunk/Assets/JAnimation/JAnimationSplit/Models/Animation/jj_run.anim";
	Rect m_fileRect;

	void OnEnable ()
	{
		thisTarget = serializedObject.targetObject as JAnimationUtility;
		m_animation = thisTarget.GetComponent<Animation>();
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.LabelField("FilePath:");
		m_fileRect = EditorGUILayout.GetControlRect(GUILayout.Width(300));
		m_filePath = EditorGUI.TextField(m_fileRect, m_filePath);

		if ((Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragExited)
		 && m_fileRect.Contains(Event.current.mousePosition))
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
			if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
			{
				m_filePath = DragAndDrop.paths[0];
			}
		}

		if (GUILayout.Button("LoadTest"))
		{
			string filename = m_filePath;
			JAnimationClip jclip = JAnimationUtility.LoadAni(filename);
			AnimationClip clip = jclip.GetAnimClip();
			m_animation.AddClip(clip, clip.name);
			m_animation.Play(clip.name);

			string asset_path = Regex.Match(m_filePath, @"(Assets/.*)/[^/]*$").Groups[1].Value;
			JAnimationUtility.SaveAni(clip, asset_path, "_copy");
			JAnimationUtility.SaveAssets();
		}
	}
}
