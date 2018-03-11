using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class JAngleRange
{
	private JAngleRange() { _min = 0; _max = 0; is_full = false; vec_left = new List<float>(); vec_right = new List<float>(); _totalRange = 0; }
	public JAngleRange(float min, float max) { _min = min; _max = max; is_full = false; vec_left = new List<float>(); vec_right = new List<float>(); _totalRange = 0; }
	float _min;
	float _max;
	public List<float> vec_left;
	public List<float> vec_right;
	bool is_full;
	float _totalRange;
	public float range { get { return _totalRange / (_max - _min); } }

	void SetFull()
	{
		is_full = true;
		vec_left = new List<float>();
		vec_right = new List<float>();
	}

	public void AddRange(float left, float right)
	{
		if(is_full)
		{
			return;
		}

		int changed_idx = MergeToList(left, right);
		MergeListItem(changed_idx);
		RefreshRangeValue();
	}

	int MergeToList(float left, float right)
	{
		if(vec_left.Count == 0)
		{
			vec_left.Add(left);
			vec_right.Add(right);
			return -1;
		}

		for (int i = 0; i != vec_left.Count; ++i)
		{
			float dst_left = vec_left[i];
			float dst_right = vec_right[i];
			MergeState state = CanMerge(ref dst_left, ref dst_right, left, right);
			if (state == MergeState.NormalMerge)
			{
				vec_left[i] = dst_left;
				vec_right[i] = dst_right;
				return i;
			}
			else if (state == MergeState.IsFull)
			{
				SetFull();
				return -1;
			}
		}
		vec_left.Add(left);
		vec_right.Add(right);
		return -1;
	}

	void MergeListItem(int changed_idx)
	{
		while (changed_idx > 0)
		{
			int new_changed_idx = -1;
			float new_dst_left = 0;
			float new_dst_right = 0;
			for (int j = 0; j != vec_left.Count; ++j)
			{
				if (changed_idx == j)
					continue;

				new_dst_left = vec_left[j];
				new_dst_right = vec_right[j];
				MergeState new_state = CanMerge(ref new_dst_left, ref new_dst_right, vec_left[changed_idx], vec_right[changed_idx]);
				if (new_state == MergeState.NormalMerge)
				{
					new_changed_idx = j;
					break;
				}
				else if (new_state == MergeState.IsFull)
				{
					SetFull();
					return;
				}
			}

			if (new_changed_idx == -1)
			{
				break;
			}
			else
			{
				int lower_idx = Mathf.Min(changed_idx, new_changed_idx);
				int higher_idx = Mathf.Max(changed_idx, new_changed_idx);
				vec_left[lower_idx] = new_dst_left;
				vec_right[lower_idx] = new_dst_right;
				vec_left.RemoveAt(higher_idx);
				vec_right.RemoveAt(higher_idx);
				changed_idx = lower_idx;
			}
		}
	}

	enum MergeState
	{
		IsFull = 1,
		NormalMerge = 0,
		CannotMerge = -1,
	}
	
	MergeState CanMerge(ref float dst_left, ref float dst_right, float src_left, float src_right)
	{
		if (dst_left > dst_right)
		{
			dst_right += _max - _min;
		}
		if (src_left > src_right)
		{
			src_right += _max - _min;
		}

		if(src_left > dst_right || dst_left > src_right)
		{
			return MergeState.CannotMerge;
		}
		else
		{
			dst_left = Mathf.Min(src_left, dst_left);
			dst_right = Mathf.Max(src_right, dst_right);
			if(dst_right > _max)
			{
				dst_right -= (_max - _min);
				if(dst_right > dst_left)
				{
					return MergeState.IsFull;
				}
			}
			return MergeState.NormalMerge;
		}
	}

	[System.Obsolete("Use CanMerge instead")]
	MergeState CanMerge_old(ref float dst_left, ref float dst_right, float src_left, float src_right)
	{
		bool dst_isWide = dst_left > dst_right;
		bool src_isWide = src_left > src_right;
		if(dst_isWide && src_isWide)
		{
			dst_left = Mathf.Min(dst_left, src_left);
			dst_right = Mathf.Min(dst_right, src_right);
			return dst_left > dst_right ? MergeState.NormalMerge : MergeState.IsFull;
		}
		else if(dst_isWide && !src_isWide)
		{
			if(src_left <= dst_right)
			{
				dst_right = Mathf.Max(src_right, dst_right);
				return dst_left > dst_right ? MergeState.NormalMerge : MergeState.IsFull;
			}
			else if(src_right >= dst_left)
			{
				dst_left = Mathf.Min(dst_left, src_left);
				return dst_left > dst_right ? MergeState.NormalMerge : MergeState.IsFull;
			}
			else
			{
				return MergeState.CannotMerge;
			}
		}
		else if (!dst_isWide && src_isWide)
		{
			if (src_left <= dst_right)
			{
				dst_left = Mathf.Min(dst_left, src_left);
				dst_right = src_right;
				return dst_left > dst_right ? MergeState.NormalMerge : MergeState.IsFull;
			}
			else if (src_right >= dst_left)
			{
				dst_right = Mathf.Max(src_right, dst_right);
				dst_left = src_left;
				return dst_left > dst_right ? MergeState.NormalMerge : MergeState.IsFull;
			}
			else
			{
				return MergeState.CannotMerge;
			}
		}
		else
		{
			if(dst_right < src_left || src_right < dst_left)
			{
				return MergeState.CannotMerge;
			}
			else
			{
				dst_left = Mathf.Min(dst_left, src_left);
				dst_right = Mathf.Max(src_right, dst_right);
				return MergeState.IsFull;
			}
		}
	}

	void RefreshRangeValue()
	{
		if(is_full)
		{
			_totalRange = _max - _min;
			return;
		}

		_totalRange = 0;
		for(int i = 0; i != vec_left.Count; ++i)
		{
			float left = vec_left[i];
			float right = vec_right[i];
			_totalRange += (right < left) ? right + (_max - _min) - left : right - left;
		}
	}
}

public class JAnimationStatistics : MonoBehaviour {
	Dictionary<string, JAngleRange> m_bone_range_x; // {path: xyz_range};
	Dictionary<string, JAngleRange> m_bone_range_y; // {path: xyz_range};
	Dictionary<string, JAngleRange> m_bone_range_z; // {path: xyz_range};

	Dictionary<string, Transform> m_bone_rangeObj;
	Dictionary<string, Transform> m_trans_cache;

	public float m_scale = 0.04f;

	Vector3 debug_range_min = Vector3.zero;
	Vector3 debug_range_max = Vector3.zero;

	void Awake()
	{
		m_bone_range_x = new Dictionary<string, JAngleRange>();
		m_bone_range_y = new Dictionary<string, JAngleRange>();
		m_bone_range_z = new Dictionary<string, JAngleRange>();
		m_bone_rangeObj = new Dictionary<string, Transform>();
		m_trans_cache = JAnimationUtility.GetChildrenPathToTransform(this.gameObject);
	}

	void Update()
	{
		ShowBoneRange();
	}
	
	public void DoStatistics(string input_path)
	{
		if (!Directory.Exists(input_path))
		{
			Debug.Log("Input path invalid!!! " + input_path);
			return;
		}

		m_bone_range_x = new Dictionary<string, JAngleRange>();
		m_bone_range_y = new Dictionary<string, JAngleRange>();
		m_bone_range_z = new Dictionary<string, JAngleRange>();
		DirectoryInfo dir_info = new System.IO.DirectoryInfo(input_path);
		FileInfo[] files_info = dir_info.GetFiles();
		foreach (FileInfo file in files_info)
		{
			if (file.Name.EndsWith(".anim"))
			{
				DoStatisticsOne(file.FullName);
			}
		}
	}

	void DoStatisticsOne(string filename)
	{
		JAnimationClip clip = JAnimationUtility.LoadAni(filename);

		Quaternion last_rot = Quaternion.identity;
		float last_time = -1;
		clip.TraverseKeyframe_rotation(delegate(float time, Quaternion rot, string full_name)
			{
				if (!m_bone_range_x.ContainsKey(full_name))
				{
					m_bone_range_x[full_name] = new JAngleRange(0, 360);
					m_bone_range_y[full_name] = new JAngleRange(0, 360);
					m_bone_range_z[full_name] = new JAngleRange(0, 360);
				}

				if(last_time > 0)
				{
					DoStaticsOneKeyFrame(time - last_time, last_rot, rot, full_name);
				}
				last_time = time;
				last_rot = rot;
				return true;
			});
	}

	void DoStaticsOneKeyFrame(float time_dura, Quaternion rot_l, Quaternion rot_r, string full_name)
	{
		Vector3 euler_l = rot_l.eulerAngles;
		Vector3 euler_r = rot_r.eulerAngles;
		debug_range_min.x = Mathf.Min(euler_l.x, debug_range_min.x);
		debug_range_min.y = Mathf.Min(euler_l.y, debug_range_min.y);
		debug_range_min.z = Mathf.Min(euler_l.z, debug_range_min.z);
		debug_range_max.x = Mathf.Max(euler_l.x, debug_range_max.x);
		debug_range_max.y = Mathf.Max(euler_l.y, debug_range_max.y);
		debug_range_max.z = Mathf.Max(euler_l.z, debug_range_max.z);
		if (Mathf.Abs(euler_r.x - euler_l.x) < 180)
		{
			m_bone_range_x[full_name].AddRange(Mathf.Min(euler_l.x, euler_r.x), Mathf.Max(euler_r.x, euler_r.x));
		}
		else
		{
			m_bone_range_x[full_name].AddRange(Mathf.Min(euler_r.x, euler_r.x), Mathf.Max(euler_l.x, euler_r.x));
		}
		if (Mathf.Abs(euler_r.y - euler_l.y) < 180)
		{
			m_bone_range_y[full_name].AddRange(Mathf.Min(euler_l.y, euler_r.y), Mathf.Max(euler_r.y, euler_r.y));
		}
		else
		{
			m_bone_range_y[full_name].AddRange(Mathf.Min(euler_r.y, euler_r.y), Mathf.Max(euler_l.y, euler_r.y));
		}
		if (Mathf.Abs(euler_r.z - euler_l.z) < 180)
		{
			m_bone_range_z[full_name].AddRange(Mathf.Min(euler_l.z, euler_r.z), Mathf.Max(euler_r.z, euler_r.z));
		}
		else
		{
			m_bone_range_z[full_name].AddRange(Mathf.Min(euler_r.z, euler_r.z), Mathf.Max(euler_l.z, euler_r.z));
		}
	}

	void ShowBoneRange()
	{
		foreach(KeyValuePair<string, JAngleRange> x_range in m_bone_range_x)
		{
			string bone_name = x_range.Key;
			if (m_trans_cache.ContainsKey(bone_name))
			{
				Transform bone_obj = m_trans_cache[bone_name];
				if (!m_bone_rangeObj.ContainsKey(bone_name))
				{
					Transform range_obj = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
					range_obj.parent = bone_obj;
					range_obj.localPosition = Vector3.zero;
					range_obj.localRotation = Quaternion.identity;
					m_bone_rangeObj[bone_name] = range_obj;

				}
				m_bone_rangeObj[bone_name].localScale = new Vector3(m_bone_range_x[bone_name].range, m_bone_range_y[bone_name].range, m_bone_range_z[bone_name].range) * m_scale;
			}
		}
	}
}
