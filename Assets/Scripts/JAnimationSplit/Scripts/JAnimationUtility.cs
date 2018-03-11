using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor; // todo 去掉Editor！

using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#region JAnimationNode
public class JAnimationNode
{
	public JAnimationNode() { name = ""; full_name = ""; curve = null; children = null; }
	public JAnimationNode(string n, string f, AnimationCurve c) { name = n; full_name = f; curve = c; children = null; }
	public string name;
	public string full_name;
	public AnimationCurve curve;
	JAnimationNode[] children;
	public JAnimationNode[] Children { get { return children; } }

	public int GetChildIndex(string name)
	{
		if(children == null)
		{
			return -1;
		}
		for(int i = 0; i != children.Length; ++i)
		{
			if(children[i].name == name)
			{ 
				return i;
			}
		}
		return -1;
	}

	public JAnimationNode GetChild(int child_index)
	{
		return children[child_index];
	}
	
	public int AddChild(string name, string full_name)
	{
		return AddChild(name, full_name, null);
	}

	public int AddChild(string name, string full_name, AnimationCurve curve)
	{
		int old_len = children == null ? 0 : children.Length;
		JAnimationNode[] new_children = new JAnimationNode[old_len + 1];
		if(children != null)
		{
			System.Array.Copy(children, new_children, children.Length);
		}
		new_children[new_children.Length - 1] = new JAnimationNode(name, full_name, curve);
		children = new_children;
		return children.Length - 1;
	}

	public void SetChildCurve(int index, AnimationCurve curve)
	{
		children[index].curve = curve;
	}

	public enum TraverseCheckFuncResult
	{
		Exit = -1,
		False = 0,
		True = 1,
	}
	public delegate TraverseCheckFuncResult TraverseCheckFunc(JAnimationNode node);
	public TraverseCheckFuncResult TraversePreOrder(TraverseCheckFunc check_func)
	{
		TraverseCheckFuncResult check_child;
		TraverseCheckFuncResult return_value = check_func(this);
		if(return_value != TraverseCheckFuncResult.True || children == null)
		{
			return return_value;
		}

		foreach(JAnimationNode ja in children)
		{
			check_child = ja.TraversePreOrder(check_func);
			if (check_child == TraverseCheckFuncResult.Exit) // 严重错误：立即结束所有循环
			{
				return TraverseCheckFuncResult.Exit;
			}
		}
		return return_value;
	}

	public JAnimationNode FindInChild(string fullname)
	{
		JAnimationNode res = null;
		TraversePreOrder(delegate(JAnimationNode child)
		{
			if(child.full_name == fullname)
			{
				res = child;
				return TraverseCheckFuncResult.Exit;
			}
			else
			{
				return TraverseCheckFuncResult.True;
			}
		});
		return res;
	}
}

public class JAnimationNodeIterator
{
	public JAnimationNodeIterator(JAnimationNode r)
	{
		root = r;
		ani_stack = new Stack<JAnimationNode>();
		SetToBegin();
	}

	JAnimationNode root;
	Stack<JAnimationNode> ani_stack;

	public JAnimationNode MoveNext()
	{
		if(ani_stack.Count == 0)
		{
			return null;
		}
		JAnimationNode cur = ani_stack.Pop();
		if(cur.Children != null)
		{
			for(int i = cur.Children.Length - 1; i >= 0; --i)
			{
				ani_stack.Push(cur.Children[i]);
			}
		}
		return cur;
	}

	public void SetToBegin()
	{
		ani_stack.Clear();
		ani_stack.Push(root);
	}

	public JAnimationNode Current { get { return (ani_stack.Count != 0) ? ani_stack.Peek() : null; } }
}
#endregion

#region AnimationClip类
public class JAnimationClip
{
	public JAnimationClip()
	{
		name = "root";
		curve_map = new Dictionary<string, JAnimationNode>();
		wrapMode = WrapMode.Default; // todo 这里的loop等参数需要从文件中读取
	}
	public string name;
	public Dictionary<string, JAnimationNode> curve_map;
	public WrapMode wrapMode;
	public float cycleTime;

	JAnimationNode GetAniNode(string propertyName)
	{
		if(!curve_map.ContainsKey(propertyName))
		{
			curve_map[propertyName] = new JAnimationNode();
		}
		return curve_map[propertyName];
	}

	public void SetSettings(float time)
	{
		cycleTime = time;
	}

	// 新建/覆盖 曲线
	public void SetCurve(string path, System.Type type, string propertyName, AnimationCurve curve)
	{
		MatchCollection split_path = Regex.Matches(path + "/", "([^/]+)/");
		JAnimationNode parent = GetAniNode(propertyName);
		string cur_path = "";
		for(int i = 0; i < split_path.Count; ++i)
		{
			string name = split_path[i].Groups[1].Value;
			cur_path = (cur_path == "") ? name : cur_path + "/" + name;
			int child_idx = parent.GetChildIndex(name);
			if(child_idx < 0)
			{
				child_idx = parent.AddChild(name, cur_path);
			}
			if (i == split_path.Count - 1)
			{
				parent.SetChildCurve(child_idx, curve);
				break;
			}
			parent = parent.GetChild(child_idx);
		}
		return;
	}

	public AnimationClip GetAnimClip()
	{
		AnimationClip res = new AnimationClip();
		res.legacy = true;
		res.wrapMode = wrapMode;
		res.name = name;
		foreach (KeyValuePair<string, JAnimationNode> curve in curve_map)
		{
			string propertyName = curve.Key;
			curve.Value.TraversePreOrder(delegate(JAnimationNode node)
			{
				if (node.curve != null)
				{
					res.SetCurve(node.full_name, typeof(Transform), propertyName, node.curve);
				}
				return JAnimationNode.TraverseCheckFuncResult.True;
			});
		}

		return res;
	}

	public AnimationClip GetAnimClipByNode(string node_name)
	{
		AnimationClip res = new AnimationClip();
		res.legacy = true;
		res.wrapMode = wrapMode;
		res.name = name;
		foreach(KeyValuePair<string, JAnimationNode> curve in curve_map)
		{
			string propertyName = curve.Key;
			curve.Value.TraversePreOrder(delegate(JAnimationNode node){
				if(node.name == node_name)
				{
					node.TraversePreOrder(delegate(JAnimationNode child_node)
						{
							if (node.curve != null)
							{
								res.SetCurve(child_node.full_name, typeof(Transform), propertyName, child_node.curve);
							}
							return JAnimationNode.TraverseCheckFuncResult.True;
						});
					return JAnimationNode.TraverseCheckFuncResult.Exit;
				}
				return JAnimationNode.TraverseCheckFuncResult.True;
			});
		}

		return res;
	}

	public AnimationClip[] GetAniClipByNodeList(string[] node_name)
	{
		AnimationClip[] res = new AnimationClip[node_name.Length];
		for(int i = 0; i != node_name.Length; ++i)
		{
			res[i] = new AnimationClip();
			res[i].name = name + "_" + Regex.Match(node_name[i], @"(\w+)$").Groups[1].Value;
			res[i].legacy = true;
			res[i].wrapMode = wrapMode;
		}
		foreach (KeyValuePair<string, JAnimationNode> curve in curve_map)
		{
			string propertyName = curve.Key;

			curve.Value.TraversePreOrder(delegate(JAnimationNode node)
			{
				if (node.curve != null)
				{
					int cur_parent_idx = node_name.Length; // 当前位于node_name中哪个索引对应的结点的范围内
					while(!node.full_name.Contains(node_name[--cur_parent_idx]) && cur_parent_idx != 0); // 找到父亲
					res[cur_parent_idx].SetCurve(node.full_name, typeof(Transform), propertyName, node.curve);
				}
				return JAnimationNode.TraverseCheckFuncResult.True;
			});
		}
		return res;
	}

	public delegate bool TraverseAndSampleFunc_rotation(Quaternion rot, string full_name);
	public void TraverseAndSample_rotation(float time, TraverseAndSampleFunc_rotation func)
	{
		if (curve_map.ContainsKey("m_LocalRotation.x"))
		{
			JAnimationNode rot_x = curve_map["m_LocalRotation.x"];
			JAnimationNode rot_y = curve_map["m_LocalRotation.y"];
			JAnimationNode rot_z = curve_map["m_LocalRotation.z"];
			JAnimationNode rot_w = curve_map["m_LocalRotation.w"];
			JAnimationNodeIterator rot_x_iter = new JAnimationNodeIterator(rot_x);
			JAnimationNodeIterator rot_y_iter = new JAnimationNodeIterator(rot_y);
			JAnimationNodeIterator rot_z_iter = new JAnimationNodeIterator(rot_z);
			JAnimationNodeIterator rot_w_iter = new JAnimationNodeIterator(rot_w);
			while (rot_x_iter.Current != null)
			{

				if(rot_x_iter.Current.curve != null)
				{
					Quaternion rot = new Quaternion(
							rot_x_iter.Current.curve.Evaluate(time),
							rot_y_iter.Current.curve.Evaluate(time),
							rot_z_iter.Current.curve.Evaluate(time),
							rot_w_iter.Current.curve.Evaluate(time)
						);
					if(!func(rot, rot_x_iter.Current.full_name))
					{
						break;
					}
				}

				rot_x_iter.MoveNext();
				rot_y_iter.MoveNext();
				rot_z_iter.MoveNext();
				rot_w_iter.MoveNext();
			}
		}
	}

	public delegate bool TraverseKeyframeFunc_rotation(float time, Quaternion rot, string full_name);
	public void TraverseKeyframe_rotation(TraverseKeyframeFunc_rotation func)
	{
		if (curve_map.ContainsKey("m_LocalRotation.x"))
		{
			JAnimationNode rot_x = curve_map["m_LocalRotation.x"];
			JAnimationNode rot_y = curve_map["m_LocalRotation.y"];
			JAnimationNode rot_z = curve_map["m_LocalRotation.z"];
			JAnimationNode rot_w = curve_map["m_LocalRotation.w"];
			JAnimationNodeIterator rot_x_iter = new JAnimationNodeIterator(rot_x);
			JAnimationNodeIterator rot_y_iter = new JAnimationNodeIterator(rot_y);
			JAnimationNodeIterator rot_z_iter = new JAnimationNodeIterator(rot_z);
			JAnimationNodeIterator rot_w_iter = new JAnimationNodeIterator(rot_w);
			while (rot_x_iter.Current != null)
			{

				if (rot_x_iter.Current.curve != null)
				{
					for(int ki = 0; ki != rot_x_iter.Current.curve.keys.Length; ++ki)
					{
						float time = rot_x_iter.Current.curve[ki].time;
						float x = rot_x_iter.Current.curve[ki].value;
						float y = rot_y_iter.Current.curve[ki].value;
						float z = rot_z_iter.Current.curve[ki].value;
						float w = rot_w_iter.Current.curve[ki].value;
						if(!func(time, new Quaternion(x, y, z, w), rot_x_iter.Current.full_name))
						{
							return;
						}
					}
				}

				rot_x_iter.MoveNext();
				rot_y_iter.MoveNext();
				rot_z_iter.MoveNext();
				rot_w_iter.MoveNext();
			}
		}
	}

	public delegate bool TraverseAndSampleFunc_position(Vector3 rot, string full_name);
	public void TraverseAndSample_position(float time, TraverseAndSampleFunc_position func)
	{
		if (curve_map.ContainsKey("localPosition.x"))
		{
			JAnimationNode pos_x = curve_map["localPosition.x"];
			JAnimationNode pos_y = curve_map["localPosition.y"];
			JAnimationNode pos_z = curve_map["localPosition.z"];
			JAnimationNodeIterator pos_x_iter = new JAnimationNodeIterator(pos_x);
			JAnimationNodeIterator pos_y_iter = new JAnimationNodeIterator(pos_y);
			JAnimationNodeIterator pos_z_iter = new JAnimationNodeIterator(pos_z);
			while (pos_x_iter.Current != null)
			{

				if (pos_x_iter.Current.curve != null)
				{
					Vector3 pos = new Vector3(
							pos_x_iter.Current.curve.Evaluate(time),
							pos_y_iter.Current.curve.Evaluate(time),
							pos_z_iter.Current.curve.Evaluate(time)
						);
					if (!func(pos, pos_x_iter.Current.full_name))
					{
						break;
					}
				}

				pos_x_iter.MoveNext();
				pos_y_iter.MoveNext();
				pos_z_iter.MoveNext();
			}
		}
	}
}
#endregion

public class JAnimationUtility : MonoBehaviour
{
	#region 加载动作曲线
	static public JAnimationClip LoadAni(string filename)
	{
		if(!System.IO.File.Exists(filename))
		{
			return null;
		}
		JAnimationClip res = new JAnimationClip();

		float start_time = 0;
		float stop_time = 1;

		StreamReader sr = new StreamReader(filename, Encoding.Default);
		string line = sr.ReadLine();
		while (line != null)
		{
			if (Regex.Match(line, @"m_Name:").Success)
			{
				res.name = Regex.Match(line, @"m_Name:\s*(\w+)").Groups[1].Value + "_jjj";
			}
			else if (Regex.Match(line, @"m_WrapMode:").Success)
			{
				res.wrapMode = (WrapMode)System.Enum.Parse(typeof(WrapMode), Regex.Match(line, @"m_WrapMode:\s*(\d+)").Groups[1].Value);
			}
			else if (Regex.Match(line, @"m_StartTime:").Success)
			{
				start_time = float.Parse(Regex.Match(line, @"m_StartTime:\s*([\d.\-e]+)").Groups[1].Value);
			}
			else if (Regex.Match(line, @"m_StopTime:").Success)
			{
				stop_time = float.Parse(Regex.Match(line, @"m_StopTime:\s*([\d.\-e]+)").Groups[1].Value);
			}
			else if (Regex.Match(line, @"m_RotationCurves[^\[\]]*$").Success)
			{
				line = LoadAni_rotationCurve(ref sr, ref res);
				continue;
			}
			else if (Regex.Match(line, @"m_PositionCurves[^\[\]]*$").Success)
			{
				line = LoadAni_positionCurve(ref sr, ref res);
				continue;
			}
			line = sr.ReadLine();
		}
		res.cycleTime = stop_time - start_time;

		return res;
	}
	static public string LoadAni_rotationCurve(ref StreamReader sr, ref JAnimationClip res)
	{
		string curve_line;
		string path = "";
		AnimationCurve curve_x = null;
		AnimationCurve curve_y = null;
		AnimationCurve curve_z = null;
		AnimationCurve curve_w = null;
		curve_line = sr.ReadLine();
		while (curve_line != null)
		{
			bool has_curve = Regex.Match(curve_line, @"m_Curve:").Success;
			if (has_curve)
			{
				curve_x = new AnimationCurve();
				curve_y = new AnimationCurve();
				curve_z = new AnimationCurve();
				curve_w = new AnimationCurve();

				curve_line = sr.ReadLine();
				while (Regex.Match(curve_line, "serializedVersion").Success)
				{
					float time = float.Parse(Regex.Match(curve_line = ReadUntilBracketMatch(sr), @"time:\s*([\d.\-e+]+)").Groups[1].Value);
					GroupCollection value_group = Regex.Match(curve_line = ReadUntilBracketMatch(sr), @" value:\s*{x:\s*([\d.\-e+]+),\s*y:\s*([\d.\-e+]+),\s*z:\s*([\d.\-e+]+),\s*w:\s*([\d.\-e+]+)").Groups;
					GroupCollection inSlope_group = Regex.Match(curve_line = ReadUntilBracketMatch(sr), @" inSlope:\s*{x:\s*([\d.\-e+]+),\s*y:\s*([\d.\-e+]+),\s*z:\s*([\d.\-e+]+),\s*w:\s*([\d.\-e+]+)").Groups;
					GroupCollection outSlope_group = Regex.Match(curve_line = ReadUntilBracketMatch(sr), @" outSlope:\s*{x:\s*([\d.\-e+]+),\s*y:\s*([\d.\-e+]+),\s*z:\s*([\d.\-e+]+),\s*w:\s*([\d.\-e+]+)").Groups;
					if (value_group.Count < 5 || inSlope_group.Count < 5 || outSlope_group.Count < 5)
					{
						Debug.Log("Error!!!");
					}
					Vector4 value = new Vector4(float.Parse(value_group[1].Value), float.Parse(value_group[2].Value), float.Parse(value_group[3].Value), float.Parse(value_group[4].Value));
					Vector4 inSlope = new Vector4(float.Parse(inSlope_group[1].Value), float.Parse(inSlope_group[2].Value), float.Parse(inSlope_group[3].Value), float.Parse(inSlope_group[4].Value));
					Vector4 outSlope = new Vector4(float.Parse(outSlope_group[1].Value), float.Parse(outSlope_group[2].Value), float.Parse(outSlope_group[3].Value), float.Parse(outSlope_group[4].Value));
					curve_x.AddKey(new Keyframe(time, value.x, inSlope.x, outSlope.x));
					curve_y.AddKey(new Keyframe(time, value.y, inSlope.y, outSlope.y));
					curve_z.AddKey(new Keyframe(time, value.z, inSlope.z, outSlope.z));
					curve_w.AddKey(new Keyframe(time, value.w, inSlope.w, outSlope.w));

					curve_line = sr.ReadLine(); // tangentMode: 0
					curve_line = sr.ReadLine(); // serializedVersion: 2
				}
			}

			if (has_curve)
			{
				while(!Regex.Match(curve_line, @"path:").Success)
				{
					curve_line = sr.ReadLine();
				}
				string real_path = curve_line;
				curve_line = ReadUntilNextColon(sr, ref real_path);
				path = Regex.Match(real_path, @"path:\s*(.+)\s*$").Groups[1].Value;
				res.SetCurve(path, typeof(Transform), "m_LocalRotation.x", curve_x);
				res.SetCurve(path, typeof(Transform), "m_LocalRotation.y", curve_y);
				res.SetCurve(path, typeof(Transform), "m_LocalRotation.z", curve_z);
				res.SetCurve(path, typeof(Transform), "m_LocalRotation.w", curve_w);
			}
			else
			{
				curve_line = sr.ReadLine();
			}

			if (Regex.Match(curve_line, @"^  [^- ]").Success)
			{
				break;
			}
		}
		return curve_line;
	}
	static public string LoadAni_positionCurve(ref StreamReader sr, ref JAnimationClip res)
	{
		string curve_line;
		string path = "";
		AnimationCurve curve_x = null;
		AnimationCurve curve_y = null;
		AnimationCurve curve_z = null;
		curve_line = sr.ReadLine();
		while (curve_line != null)
		{
			bool has_curve = Regex.Match(curve_line, @"m_Curve:").Success;
			if (has_curve)
			{
				curve_x = new AnimationCurve();
				curve_y = new AnimationCurve();
				curve_z = new AnimationCurve();

				curve_line = sr.ReadLine();
				while (Regex.Match(curve_line, "serializedVersion").Success)
				{
					float time = float.Parse(Regex.Match(curve_line = ReadUntilBracketMatch(sr), @"time:\s*([\d.\-e+]+)").Groups[1].Value);
					GroupCollection value_group = Regex.Match(curve_line = ReadUntilBracketMatch(sr), @" value:\s*{x:\s*([\d.\-e+]+),\s*y:\s*([\d.\-e+]+),\s*z:\s*([\d.\-e+]+)").Groups;
					GroupCollection inSlope_group = Regex.Match(curve_line = ReadUntilBracketMatch(sr), @" inSlope:\s*{x:\s*([\d.\-e+]+),\s*y:\s*([\d.\-e+]+),\s*z:\s*([\d.\-e+]+)").Groups;
					GroupCollection outSlope_group = Regex.Match(curve_line = ReadUntilBracketMatch(sr), @" outSlope:\s*{x:\s*([\d.\-e+]+),\s*y:\s*([\d.\-e+]+),\s*z:\s*([\d.\-e+]+)").Groups;
					if (value_group.Count < 4 || inSlope_group.Count < 4 || outSlope_group.Count < 4)
					{
						Debug.Log("Error!!!");
					}
					Vector3 value = new Vector3(float.Parse(value_group[1].Value), float.Parse(value_group[2].Value), float.Parse(value_group[3].Value));
					Vector3 inSlope = new Vector3(float.Parse(inSlope_group[1].Value), float.Parse(inSlope_group[2].Value), float.Parse(inSlope_group[3].Value));
					Vector3 outSlope = new Vector3(float.Parse(outSlope_group[1].Value), float.Parse(outSlope_group[2].Value), float.Parse(outSlope_group[3].Value));
					curve_x.AddKey(new Keyframe(time, value.x, inSlope.x, outSlope.x));
					curve_y.AddKey(new Keyframe(time, value.y, inSlope.y, outSlope.y));
					curve_z.AddKey(new Keyframe(time, value.z, inSlope.z, outSlope.z));

					curve_line = sr.ReadLine(); // tangentMode: 0
					curve_line = sr.ReadLine(); // serializedVersion: 2
				}
			}

			if (has_curve)
			{
				while (!Regex.Match(curve_line, @"path:").Success)
				{
					curve_line = sr.ReadLine();
				}
				string real_path = curve_line;
				curve_line = ReadUntilNextColon(sr, ref real_path);
				path = Regex.Match(real_path, @"path:\s*(.+)\s*$").Groups[1].Value;
				res.SetCurve(path, typeof(Transform), "localPosition.x", curve_x);
				res.SetCurve(path, typeof(Transform), "localPosition.y", curve_y);
				res.SetCurve(path, typeof(Transform), "localPosition.z", curve_z);
			}
			else
			{
				curve_line = sr.ReadLine();
			}

			if (Regex.Match(curve_line, @"^  [^- ]").Success)
			{
				break;
			}
		}
		return curve_line;
	}
	#endregion

	#region 工具函数：读取下一行，如果其没有冒号，说明是前一行遗留的，那么将其和前一行拼接起来。返回逻辑上的下一行的字符串
	public static string ReadUntilNextColon(StreamReader sr, ref string origin_line)
	{
		string line = "";
		while((line = sr.ReadLine()) != null)
		{
			if(Regex.Match(line, ":").Success)
			{
				return line;
			}
			else
			{
				origin_line += " " + Regex.Match(line, @"^\s*(.*)$").Groups[1].Value;
			}
		}
		Debug.Log("Error! " + "colon cannot match!"); 
		return line;
	}
	#endregion

	#region 工具函数：读取一整行，如果有右括号遗留，那么继续读取下一行，并且拼接起来返回
	public static string ReadUntilBracketMatch(StreamReader sr)
	{
		string line;
		string res_line = "";
		string reg_left = @"({)";
		string reg_right = @"(})";
		int left_count = 0;
		int right_count = 0;
		while ((line = sr.ReadLine()) != null)
		{
			left_count += Regex.Match(line, reg_left).Groups.Count;
			right_count += Regex.Match(line, reg_right).Groups.Count;
			res_line = res_line + line;
			if(left_count <= right_count)
			{
				return res_line;
			}
		}
		return res_line;
	}
	#endregion

	#region 加载Editor曲线（废弃）
	[System.Obsolete]
	static public AnimationClip LoadAni_editorCurve(string filename)
	{
		AnimationClip res = new AnimationClip();

		StreamReader sr = new StreamReader(filename, Encoding.Default);
		string line;
		while ((line = sr.ReadLine()) != null)
		{
			if (Regex.Match(line, @"m_Name:").Success)
			{
				res.name = Regex.Match(line, @"m_Name:\s*(\w+)").Groups[1].Value + "_jjj";
			}
			else if (Regex.Match(line, @"m_EditorCurve").Success)
			{
				string curve_line;
				string path = "";
				string propertyName = "";
				AnimationCurve curve = null;
				while ((curve_line = sr.ReadLine()) != null)
				{
					if (Regex.Match(curve_line, @"- curve:").Success)
					{
						curve = new AnimationCurve();

						string time_line;
						while ((time_line = sr.ReadLine()) != null)
						{
							if (Regex.Match(time_line, @"time:").Success)
							{
								float time = float.Parse(Regex.Match(time_line, @"time:\s*([\d.\-e+]+)").Groups[1].Value);
								float value = float.Parse(Regex.Match((time_line = sr.ReadLine()), @"value:\s*([\d.\-e+]+)").Groups[1].Value);
								float inSlope = float.Parse(Regex.Match((time_line = sr.ReadLine()), @"inSlope:\s*([\d.\-e+]+)").Groups[1].Value);
								float outSlope = float.Parse(Regex.Match((time_line = sr.ReadLine()), @"outSlope:\s*([\d.\-e+]+)").Groups[1].Value);
								curve.AddKey(new Keyframe(time, value, inSlope, outSlope));
							}

							if (Regex.Match(time_line, @"attribute:").Success)
							{
								propertyName = Regex.Match(time_line, @"attribute:\s*([\w.]+)").Groups[1].Value;
							}
							else if (Regex.Match(time_line, @"path:").Success)
							{
								path = Regex.Match(time_line, @"path:\s*([\w.]+)").Groups[1].Value;
							}

							if (Regex.Match(time_line, @"m_RotationOrder:").Success)
								break;
						}
					}
					else if (Regex.Match(curve_line, @"script:").Success)
						break;
				}
				if(curve != null)
				{
					res.SetCurve(path, typeof(Transform), propertyName, curve);
				}
			}
		}

		return res;
	}
	#endregion

	#region 工具函数：保存动画 todo AssetDatabase属于Editor
	public static void SaveAni(AnimationClip clip, string path, string postfix)
	{
		string new_filename = path + "/" + clip.name + postfix + ".anim";
		AssetDatabase.DeleteAsset(new_filename);
		AssetDatabase.CreateAsset(clip, new_filename);
		//AssetDatabase.SaveAssets();
		//AssetDatabase.Refresh();
	}
	public static void SaveAssets()
	{
		AssetDatabase.SaveAssets();
	}
	#endregion

	#region 工具函数：获得Hermit插值
	public static float GetLerpParam(float left_value, float right_value, float left_slope, float right_slope, float t)
	{
		return (1 + 2 * t) * (1 - t) * (1 - t) * left_value
			+ t * (1 - t) * (1 - t) * left_slope
			+ t * t * (3 - 2 * t) * right_value
			+ t * t * (t - 1) * right_slope;
	}
	#endregion

	#region 工具函数：四元数及其导数 => 欧拉角及其导数
	public void TransQuaternionSlopeToEulerSlope(Quaternion q, Vector4 qd, out Vector3 e, out Vector3 ed)
	{
		e = q.eulerAngles;
		ed = Vector3.zero;

		float px_mole = 2 * (q.w * q.x + q.y * q.z);
		float px_deno = q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z;
		float px_1 = px_mole / px_deno;
		float px_2 = 2 * (qd.w * q.x + q.w * qd.x + qd.y * q.z + q.y * qd.z) / px_deno;
		float px_3 = - px_mole / px_deno / px_deno * 2 * (qd.w * q.w - qd.x * q.x - qd.y * q.y + qd.z * q.z);

		float py_1 = 2 * (q.w * q.y - q.x * q.z);
		float py_2 = 2 * (qd.w * q.y + q.w * qd.y - qd.x * q.z - q.x * qd.z);

		float pz_mole = 2 * (q.w * q.z + q.x * q.y);
		float pz_deno = (q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z);
		float pz_1 = pz_mole / pz_deno;
		float pz_2 = 2 * (qd.w * q.z + q.w * qd.z + qd.x * q.y + q.x * qd.y) / pz_deno;
		float pz_3 = - pz_mole / pz_deno / pz_deno * 2 * (qd.w * q.w + qd.x * q.x - qd.y * q.y - qd.z * q.z);

		ed.x = 1 / (1 + px_1 * px_1) * (px_2 + px_3);
		ed.y = 1 / Mathf.Sqrt(1 - py_1 * py_1) * py_2;
		ed.z = 1 / (1 + pz_1 * pz_1) * (pz_2 + pz_3);
	}
	#endregion

	#region 工具函数：获得所有孩子的path到Transform的映射（方便索引用）
	static public Dictionary<string, Transform> GetChildrenPathToTransform(GameObject parent)
	{
		Dictionary<string, Transform> trans_cache = new Dictionary<string, Transform>();
		for (int i = 0; i != parent.transform.childCount; ++i)
		{
			Transform child_trans = parent.transform.GetChild(i);
			InitBones(child_trans, "", trans_cache);
		}
		return trans_cache;
	}

	static void InitBones(Transform o, string cur_path, Dictionary<string, Transform> trans_cache)
	{
		if (cur_path != "")
		{
			cur_path += "/";
		}
		string new_path = cur_path + o.name;
		trans_cache[new_path] = o;
		for (int i = 0; i != o.transform.childCount; ++i)
		{
			Transform child_trans = o.transform.GetChild(i);
			InitBones(child_trans, new_path, trans_cache);
		}
	}
	#endregion

	#region 工具函数：在孩子中递归查找某个名字
	public static Transform TraverseFind(Transform parent, string name)
	{
		Transform res = parent.Find(name);
		if (res != null)
		{
			return res;
		}
		for (int i = 0; i != parent.childCount; ++i)
		{
			Transform child_res = TraverseFind(parent.GetChild(i), name);
			if (child_res != null)
			{
				return child_res;
			}
		}
		return null;
	}
	#endregion
}
