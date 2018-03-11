using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JAnimationCombine : MonoBehaviour
{
	[SerializeField]
	public string[] m_combineAnims = new string[0];

	public AnimationClip DoCombine()
	{
		List<JAnimationClip> src_anims = new List<JAnimationClip>();
		foreach (string comb in m_combineAnims)
		{
			if (comb != null)
			{
				src_anims.Add(JAnimationUtility.LoadAni(comb));
			}
		}

		// 注册所有骨骼，为每个骨骼统计其所有动画曲线
		// Dictionary<property_name, Dictionary<bone_name, List<AnimationCurve>>>
		Dictionary<string, Dictionary<string, List<AnimationCurve>>> all_curves = new Dictionary<string, Dictionary<string, List<AnimationCurve>>>();
		foreach(JAnimationClip clip in src_anims)
		{
			foreach(KeyValuePair<string, JAnimationNode> root in clip.curve_map)
			{
				if(!all_curves.ContainsKey(root.Key))
				{
					all_curves[root.Key] = new Dictionary<string,List<AnimationCurve>>();
				}
				var cur_bone_root = all_curves[root.Key];
				root.Value.TraversePreOrder(delegate(JAnimationNode node)
					{
						if(!cur_bone_root.ContainsKey(node.full_name))
						{
							cur_bone_root[node.full_name] = new List<AnimationCurve>();
						}
						if(node.curve != null)
						{
							cur_bone_root[node.full_name].Add(node.curve);
						}
						return JAnimationNode.TraverseCheckFuncResult.True;
					});
			}
		}

		// 合并为新的曲线
		AnimationClip res = new AnimationClip();
		res.legacy = true;
		res.wrapMode = src_anims[0].wrapMode;
		foreach(var property_tree in all_curves)
		{
			var bone_curve_map = property_tree.Value;
			string property_name = property_tree.Key;

			foreach(var bone_curve in bone_curve_map)
			{
				if(bone_curve.Value.Count > 0)
				{
					// todo 这里可以改为blend
					res.SetCurve(bone_curve.Key, typeof(Transform), property_name, bone_curve.Value[0]);
				}
			}
		}
		return res;
	}
}
