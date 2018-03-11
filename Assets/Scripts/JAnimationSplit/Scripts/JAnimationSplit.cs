using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

public class JAnimationSplit : MonoBehaviour
{
	static public void DoSplitAll(string input_path, string output_path)
	{
		if (!Directory.Exists(input_path))
		{
			Debug.Log("Input path invalid!!! " + input_path);
			return;
		}
		if (!Directory.Exists(output_path))
		{
			Debug.Log("Output path invalid!!! " + output_path);
			return;
		}
		DirectoryInfo dir_info = new System.IO.DirectoryInfo(input_path);
		FileInfo[] files_info = dir_info.GetFiles();
		foreach (FileInfo file in files_info)
		{
			if (file.Name.EndsWith(".anim"))
			{
				DoSplitOne(file.FullName, output_path);
			}
		}
		JAnimationUtility.SaveAssets();
	}

	static public void DoSplitOne(string file, string output_path)
	{
		JAnimationClip ja = JAnimationUtility.LoadAni(file);
		string[] all_cut = { "Root", "Bip01 R Clavicle", "Bip01 L Thigh" };
		AnimationClip[] all = ja.GetAniClipByNodeList(all_cut);
		foreach (AnimationClip clip in all)
		{
			JAnimationUtility.SaveAni(clip, output_path, "");
		}
	}
}
