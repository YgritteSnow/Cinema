using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class JTest : MonoBehaviour
{
	// 从某个位置开始播放
	public string ani_path;
	public AnimationClip ani_clip;

	public void TestAniPlay()
	{
		float s00 = 0;
		float s01 = 1;
		float s10 = 0.7071068f;
		float s11 = 0.7071067f;

		float m0 = 0.78539824f;
		float m1 = 0.5555153f;

		float t_all = 1f;

		float step_time = 0.1f;

		AnimationCurve curve_x = new AnimationCurve();
		AnimationCurve curve_y = new AnimationCurve();
		AnimationCurve curve_z = new AnimationCurve();
		AnimationCurve curve_w = new AnimationCurve();

		JAnimationClip jc = JAnimationUtility.LoadAni(ani_path);
		AnimationCurve jc_curve_x = new AnimationCurve();
		AnimationCurve jc_curve_y = new AnimationCurve();
		AnimationCurve jc_curve_z = new AnimationCurve();
		AnimationCurve jc_curve_w = new AnimationCurve();

		foreach (EditorCurveBinding bind in AnimationUtility.GetCurveBindings(ani_clip))
		{
			if (bind.propertyName == "m_LocalRotation.x")
			{
				curve_x = AnimationUtility.GetEditorCurve(ani_clip, bind);
				jc_curve_x = jc.curve_map[bind.propertyName].FindInChild(bind.path).curve;
			}
			else if (bind.propertyName == "m_LocalRotation.y")
			{
				curve_y = AnimationUtility.GetEditorCurve(ani_clip, bind);
				jc_curve_y = jc.curve_map[bind.propertyName].FindInChild(bind.path).curve;
			}
			else if (bind.propertyName == "m_LocalRotation.z")
			{
				curve_z = AnimationUtility.GetEditorCurve(ani_clip, bind);
				jc_curve_z = jc.curve_map[bind.propertyName].FindInChild(bind.path).curve;
			}
			else if (bind.propertyName == "m_LocalRotation.w")
			{
				curve_w = AnimationUtility.GetEditorCurve(ani_clip, bind);
				jc_curve_w = jc.curve_map[bind.propertyName].FindInChild(bind.path).curve;
			}
		}

		Debug.Log("sample: A | B | C ");
		float tt = 0;
		while (tt < t_all)
		{
			tt += step_time;
			float lerp0 = JAnimationUtility.GetLerpParam(s00, s10, m0, m1, tt);
			float lerp1 = JAnimationUtility.GetLerpParam(s01, s11, m0, m1, tt);
			Vector2 v = new Vector2(lerp0, lerp1);
			v.Normalize();

			float l0 = Mathf.Lerp(s00, s10, tt);
			float l1 = Mathf.Lerp(s01, s11, tt);
			Vector2 lv = new Vector2(l0, l1);
			lv.Normalize();

			float qx = curve_x.Evaluate(tt);
			float qy = curve_y.Evaluate(tt);
			float qz = curve_z.Evaluate(tt);
			float qw = curve_w.Evaluate(tt);

			float jx = jc_curve_x.Evaluate(tt);
			float jy = jc_curve_y.Evaluate(tt);
			float jz = jc_curve_z.Evaluate(tt);
			float jw = jc_curve_w.Evaluate(tt);
			Debug.Log("sample:" + (Mathf.Atan2(v.x, v.y) * 2 - tt * Mathf.PI / 2)
				+ " | " + (Mathf.Atan2(qx, qw) * 2 - tt * Mathf.PI / 2)
				+ " | " + (Mathf.Atan2(jx, jw) * 2 - tt * Mathf.PI / 2)
				+ " | " + (Mathf.Atan2(lv.x, lv.y) * 2 - tt * Mathf.PI / 2)
				+ "\t" + qx + "\t" + qy + "\t" + qz + "\t" + qw);
		}
	}
}
