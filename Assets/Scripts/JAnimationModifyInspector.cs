using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JAnimationModify))]
public class JAnimationModifyInspector : Editor {
	JAnimationModify thisTarget;
	Animation m_animation;
	float scale = 0.3f;

	Vector3 upperarm_ang = Vector3.zero;
	float upperarm_offset = 0.0f;
	
	float spine_offset = 0.0f;

	// 1个部位的修改
	struct ModifyBoneOne
	{
		string bone_path_endswith; // 骨骼的路径的最后几个字符

		enum ModifyType
		{
			Scale, // 在原旋转的基础上进行缩放
			Offset, // 对原旋转整体偏移
		}
		Quaternion offset_towards; // 如果是整体偏移的话，偏移朝着的角度
		
		float[] child_smooth_percent; // 骨骼的孩子需要进行平滑（先不考虑父亲的平滑）
	}
	string[] fix_bone; // 需要固定朝向的骨骼

	void OnEnable()
	{
		thisTarget = serializedObject.targetObject as JAnimationModify;
		m_animation = thisTarget.GetComponent<Animation>();
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		scale = EditorGUILayout.FloatField("scale", scale);

		upperarm_ang = EditorGUILayout.Vector3Field("upperarm_ang", upperarm_ang);
		upperarm_offset = EditorGUILayout.FloatField("upperarm_offset", upperarm_offset);

		spine_offset = EditorGUILayout.FloatField("spine_offset", spine_offset);
		if (GUILayout.Button("Reset Clips"))
		{
			if(m_animation == null)
			{
				m_animation = thisTarget.GetComponent<Animation>();
			}
			ResetClip(m_animation.clip.name, scale, upperarm_offset, spine_offset);
		}
		if (GUILayout.Button("Replay Clip"))
		{
			if(m_animation != null)
			{
				m_animation.Play(m_animation.clip.name);
			}
		}
	}

	#region 废弃函数
	float EulerAngle_360To180(float angle)
	{
		return (angle+180f) % 360f - 180f;
	}
	float EulerAngle_180To360(float angle)
	{
		return (angle+360) % 360;
	}
	Vector3 EulerAngle_360To180(Vector3 angle)
	{
		return new Vector3(
			(angle.x + 180f) % 360f - 180f,
			(angle.y + 180f) % 360f - 180f,
			(angle.z + 180f) % 360f - 180f);
	}
	Vector3 EulerAngle_180To360(Vector3 angle)
	{
		return new Vector3(
			(angle.x + 360) % 360,
			(angle.y + 360) % 360,
			(angle.z + 360) % 360);
	}
	Vector3 Vec3Abs(Vector3 src)
	{
		return new Vector3(Mathf.Abs(src.x), Mathf.Abs(src.y), Mathf.Abs(src.z));
	}
	#endregion

	#region 使用矩阵的方法计算四元数的平均值
	Matrix4x4 getMatByVec4AndAdd(Vector4 v, Matrix4x4 src)
	{
		Matrix4x4 res = Matrix4x4.identity;
		res.SetRow(0, new Vector4(v.x * v.x, v.x * v.y, v.x * v.z, v.x * v.w) + src.GetRow(0));
		res.SetRow(1, new Vector4(v.y * v.x, v.y * v.y, v.y * v.z, v.y * v.w) + src.GetRow(1));
		res.SetRow(2, new Vector4(v.z * v.x, v.z * v.y, v.z * v.z, v.z * v.w) + src.GetRow(2));
		res.SetRow(3, new Vector4(v.w * v.x, v.w * v.y, v.w * v.z, v.w * v.w) + src.GetRow(3));
		return res;
	}

	Quaternion AverageQuaternionByMatrix(Quaternion[] src)
	{
		Matrix4x4 mat = Matrix4x4.zero;
		foreach(Quaternion q in src)
		{
			Vector4 v = new Vector4(q.x, q.y, q.z, q.w);
			mat = getMatByVec4AndAdd(v, mat);
		}
		return Quaternion.identity;
	}
	#endregion

	#region 使用逐个相加的方法计算四元数的平均值
	Quaternion CalAverageQuaternion(string logmsg, AnimationCurve curve_x, AnimationCurve curve_y, AnimationCurve curve_z, AnimationCurve curve_w
		, out Quaternion[] all_quat, out bool one_cycle)
	{
		all_quat = new Quaternion[curve_x.keys.Length];
		for (int i = 0; i < curve_x.keys.Length; ++i)
		{
			all_quat[i] = new Quaternion(curve_x[i].value
				, curve_y[i].value
				, curve_z[i].value
				, curve_w[i].value);
		}

		return AverageQuaternionByCumulate(all_quat, logmsg, out one_cycle);
	}

	Quaternion AverageQuaternionByCumulate(Quaternion[] src, string logmsg, out bool one_cycle)
	{
		one_cycle = false;

		logmsg += ": ";

		float add_count = 0;
		Quaternion last_quat = Quaternion.identity;
		Quaternion first_quat = src.Length > 0 ? src[0] : Quaternion.identity;
		if (Quaternion.Dot(first_quat, Quaternion.identity) < 0)
		{
			first_quat = new Quaternion(-first_quat.x, -first_quat.y, -first_quat.z, -first_quat.w);
		}

		Quaternion cur_sum = new Quaternion(0, 0, 0, 0);
		foreach(Quaternion q in src)
		{
			Quaternion cur_quat = q;
			if(Quaternion.Dot(q, cur_sum) < 0)
			{
				//Debug.Log(logmsg + Quaternion.Dot(q, cur_sum) + "," + -Quaternion.Dot(cur_quat, first_quat));
				cur_quat = new Quaternion(-q.x, -q.y, -q.z, -q.w);
			}
			cur_sum = Quaternion.Slerp(cur_sum, cur_quat, 1/(add_count+1));
			//cur_sum.x = (add_count * cur_sum.x + 1 * cur_quat.x) / (add_count + 1);
			//cur_sum.y = (add_count * cur_sum.y + 1 * cur_quat.y) / (add_count + 1);
			//cur_sum.z = (add_count * cur_sum.z + 1 * cur_quat.z) / (add_count + 1);
			//cur_sum.w = (add_count * cur_sum.w + 1 * cur_quat.w) / (add_count + 1);

			if (Quaternion.Dot(cur_quat, last_quat) < 0)
			{
				//Debug.Log(logmsg + Quaternion.Dot(cur_quat, first_quat));
				last_quat = new Quaternion(-q.x, -q.y, -q.z, -q.w);
			}
			else
			{
				last_quat = q;
			}
			if(Quaternion.Dot(last_quat, first_quat) < 0)
			{
				one_cycle = true;
			}

			++ add_count;
		}

		return cur_sum;
	}
	#endregion

	#region 使用欧拉角的方法计算Rotation的平均值
	Vector3[] CalAverageEuler(AnimationCurve curve_x, AnimationCurve curve_y, AnimationCurve curve_z, AnimationCurve curve_w
		, out Vector3 avg_euler)
	{
		Vector3[] res_euler = new Vector3[curve_x.keys.Length];
		avg_euler = Vector3.zero;


		Vector3[] tmp_euler = new Vector3[curve_x.keys.Length];

		Vector3 rot_cycle = Vector3.zero;
		Vector3 last_euler = Vector3.zero;
		for (int i = 0; i < curve_x.keys.Length; ++i)
		{
			Quaternion quat = new Quaternion(curve_x[i].value
				, curve_y[i].value
				, curve_z[i].value
				, curve_w[i].value);

			Vector3 cur_euler = quat.eulerAngles;
			tmp_euler[i] = cur_euler;
			if(cur_euler.x > 180)
			{
				cur_euler.x = 180 - cur_euler.x;
				cur_euler.y = cur_euler.y - 180;
				cur_euler.z = cur_euler.z - 180;
			}
			cur_euler.x = CycleAngleToBeSmooth(last_euler.x, cur_euler.x);
			cur_euler.y = CycleAngleToBeSmooth(last_euler.y, cur_euler.y);
			cur_euler.z = CycleAngleToBeSmooth(last_euler.z, cur_euler.z);
			last_euler = cur_euler;
			res_euler[i] = cur_euler;

			avg_euler += cur_euler;
		}
		
		avg_euler /= curve_x.keys.Length;
		return res_euler;
	}

	float CycleAngleToBeSmooth(float lh, float rh)
	{
		return ((rh - lh) % 360 + 360 + 180) % 360 - 180 + lh;
	}
	#endregion

	void ResetClip(string clipname
		, float scale
		, float upperarm_offset
		, float spine_offset)
	{
		AnimationClip old_clip = m_animation.GetClip(clipname);
		AnimationClip new_clip = new AnimationClip();
		new_clip.legacy = true;
		new_clip.wrapMode = WrapMode.Loop;
		AnimationClipSettings new_setting = AnimationUtility.GetAnimationClipSettings(old_clip);
		AnimationUtility.SetAnimationClipSettings(new_clip, new_setting);
		new_clip.ClearCurves();
		foreach(EditorCurveBinding bind in AnimationUtility.GetCurveBindings(old_clip))
		{
			AnimationCurve old_curve = AnimationUtility.GetEditorCurve(old_clip, bind);
			if(bind.propertyName.StartsWith("m_LocalRotation"))
			{
				if(bind.propertyName == "m_LocalRotation.x")
				{
					EditorCurveBinding bind_y = bind;
					bind_y.propertyName = "m_LocalRotation.y";
					AnimationCurve old_curve_y = AnimationUtility.GetEditorCurve(old_clip, bind_y);
					EditorCurveBinding bind_z = bind;
					bind_z.propertyName = "m_LocalRotation.z";
					AnimationCurve old_curve_z = AnimationUtility.GetEditorCurve(old_clip, bind_z);
					EditorCurveBinding bind_w = bind;
					bind_w.propertyName = "m_LocalRotation.w";
					AnimationCurve old_curve_w = AnimationUtility.GetEditorCurve(old_clip, bind_w);

					AnimationCurve new_curve_x = new AnimationCurve();
					AnimationCurve new_curve_y = new AnimationCurve();
					AnimationCurve new_curve_z = new AnimationCurve();
					AnimationCurve new_curve_w = new AnimationCurve();

					#region 使用所有 Rotation 的欧拉角数据
					/* 
					Vector3 avg_euler;
					Vector3[] all_euler = CalAverageEuler(old_curve, old_curve_y, old_curve_z, old_curve_w, out avg_euler);

					for(int i = 0; i < all_euler.Length; ++i)
					{
						Vector3 new_euler = all_euler[i] * scale;
						Quaternion new_quat = Quaternion.Euler(new_euler);

						new_curve_x.AddKey(new Keyframe(old_curve[i].time, new_quat.x, old_curve[i].inTangent * scale, old_curve[i].outTangent * scale));
						new_curve_y.AddKey(new Keyframe(old_curve_y[i].time, new_quat.y, old_curve_y[i].inTangent * scale, old_curve_y[i].outTangent * scale));
						new_curve_z.AddKey(new Keyframe(old_curve_z[i].time, new_quat.z, old_curve_z[i].inTangent * scale, old_curve_z[i].outTangent * scale));
						new_curve_w.AddKey(new Keyframe(old_curve_w[i].time, new_quat.w, old_curve_w[i].inTangent * scale, old_curve_w[i].outTangent * scale));
					}
					 */
					 #endregion

					#region 处理单个 Rotation
					/* 
					for(int i = 0; i < old_curve.keys.Length; ++i)
					{
						Quaternion old_quat = new Quaternion(old_curve[i].value
							, old_curve_y[i].value
							, old_curve_z[i].value
							, old_curve_w[i].value);

						//-- 对四元数的 angle 进行 scale
						//Vector3 old_axis;
						//float old_angle;
						//old_quat.ToAngleAxis(out old_angle, out old_axis);
						//Quaternion new_quat = Quaternion.AngleAxis(old_angle * scale, old_axis);

						//-- 将四元数与0使用scale插值
						Quaternion new_quat = Quaternion.Slerp(old_quat, Quaternion.identity, scale);

						Keyframe old_frame = old_curve[i];
						new_curve_x.AddKey(new Keyframe(old_curve[i].time, new_quat.x, old_curve[i].inTangent * scale, old_curve[i].outTangent * scale));
						new_curve_y.AddKey(new Keyframe(old_curve_y[i].time, new_quat.y, old_curve_y[i].inTangent * scale, old_curve_y[i].outTangent * scale));
						new_curve_z.AddKey(new Keyframe(old_curve_z[i].time, new_quat.z, old_curve_z[i].inTangent * scale, old_curve_z[i].outTangent * scale));
						new_curve_w.AddKey(new Keyframe(old_curve_w[i].time, new_quat.w, old_curve_w[i].inTangent * scale, old_curve_w[i].outTangent * scale));
					}
					 */
					 #endregion

					#region 使用所有 Rotateion 的四元数数据
					Quaternion[] all_quat;
					bool one_cycle;
					Quaternion avg_rot = CalAverageQuaternion(bind.path, old_curve, old_curve_y, old_curve_z, old_curve_w, out all_quat, out one_cycle);

					// 如果超过1圈，那么不调整了
					if(one_cycle)
					{
						AnimationUtility.SetEditorCurve(new_clip, bind, old_curve);
						AnimationUtility.SetEditorCurve(new_clip, bind_y, old_curve_y);
						AnimationUtility.SetEditorCurve(new_clip, bind_z, old_curve_z);
						AnimationUtility.SetEditorCurve(new_clip, bind_w, old_curve_w);
						continue;
					}

					for (int i = 0; i < all_quat.Length; ++i)
					{
						// 向着平均值的方向进行缩放
						Quaternion new_quat = Quaternion.Slerp(all_quat[i], avg_rot, 1-scale);

						// 对 upperarm 的角度进行移动
						if(bind.path.EndsWith("UpperArm"))
						{
							new_quat = Quaternion.Slerp(new_quat, Quaternion.Euler(upperarm_ang * (bind.path.EndsWith("L UpperArm") ? -1 : 1))
							, upperarm_offset);
						}
						else if(bind.path.EndsWith("Spine"))
						{
							Vector3 angles = new_quat.eulerAngles;
							angles *= 1+spine_offset;
							//new_quat = Quaternion.Euler(angles);
						}

						new_curve_x.AddKey(new Keyframe(old_curve[i].time, new_quat.x, old_curve[i].inTangent * scale, old_curve[i].outTangent * scale));
						new_curve_y.AddKey(new Keyframe(old_curve_y[i].time, new_quat.y, old_curve_y[i].inTangent * scale, old_curve_y[i].outTangent * scale));
						new_curve_z.AddKey(new Keyframe(old_curve_z[i].time, new_quat.z, old_curve_z[i].inTangent * scale, old_curve_z[i].outTangent * scale));
						new_curve_w.AddKey(new Keyframe(old_curve_w[i].time, new_quat.w, old_curve_w[i].inTangent * scale, old_curve_w[i].outTangent * scale));
					}
					#endregion

					//new_clip.SetCurve(bind.path, bind.type, bind.propertyName, new_curve);
					AnimationUtility.SetEditorCurve(new_clip, bind, new_curve_x);
					AnimationUtility.SetEditorCurve(new_clip, bind_y, new_curve_y);
					AnimationUtility.SetEditorCurve(new_clip, bind_z, new_curve_z);
					AnimationUtility.SetEditorCurve(new_clip, bind_w, new_curve_w);
				}
			}
			else
			{
				//new_clip.SetCurve(bind.path, bind.type, bind.propertyName, old_curve);
				AnimationUtility.SetEditorCurve(new_clip, bind, old_curve);
			}
		}
		string new_clipname = clipname + "_jjj";
		string new_assetname = "Assets/" + clipname + "_jjj" + ".anim";
		m_animation.AddClip(new_clip, new_clipname);
		m_animation.Play(new_clipname);
		AssetDatabase.DeleteAsset(new_assetname);
		AssetDatabase.CreateAsset(new_clip, new_assetname);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}
}
