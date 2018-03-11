using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultipleParam
{
	public MultipleParam(string c, float b, bool i) { clip_path = c; blend_weight = b; is_blend_normalise = i; clip = null; }
	public string clip_path;
	public float blend_weight = 1; // 当有重合部分时，对重合部分进行融合的权重
	public bool is_blend_normalise = false; // 重合部分融合时使用的权重之和，是否归一化
	public JAnimationClip clip;
}

public class JAnimationPoseSetter
{
	public JAnimationPoseSetter(GameObject r) 
	{
		trans_cache = JAnimationUtility.GetChildrenPathToTransform(r);
	}
	
	Dictionary<string, Transform> trans_cache;

	public void SetPose(JAnimationClip clip, float time)
	{
		clip.TraverseAndSample_rotation(time, delegate(Quaternion rot, string full_name)
			{
				if (trans_cache.ContainsKey(full_name))
				{
					trans_cache[full_name].localRotation = rot;
				}
				return true;
			});

		clip.TraverseAndSample_position(time, delegate(Vector3 pos, string full_name)
		{
			if (trans_cache.ContainsKey(full_name))
			{
				trans_cache[full_name].localPosition = pos;
			}
			return true;
		});
	}
}

public class JAnimationMultiplePlay : MonoBehaviour {
	private MultipleParam[] m_clips = new MultipleParam[0];
	public MultipleParam[] clips { get{return m_clips; } }
	public bool is_playing = true;
	float m_cycleTime = 1.0f;

	bool is_dirty = true;
	JAnimationPoseSetter m_pos_setter = null;

	void Awake()
	{
		if (GetComponent<Animation>() != null)
		{
			GetComponent<Animation>().enabled = false;
		}

		InitPoseSetter();
		is_dirty = true;
	}

	void Update()
	{
		if(is_dirty)
		{
			CalBlendAnims();
			is_dirty = false;
		}

		if(is_playing)
		{
			StopAt(Time.time % m_cycleTime);
		}
	}

	void CalBlendAnims()
	{
		for(int i = 0; i != m_clips.Length; ++i)
		{
			if (m_clips[i].clip_path != "")
			{
				m_clips[i].clip = JAnimationUtility.LoadAni(m_clips[i].clip_path);
				m_cycleTime = m_clips[i].clip.cycleTime;
			}
		}
	}

	void InitPoseSetter()
	{
		m_pos_setter = new JAnimationPoseSetter(this.gameObject);
	}

	public void ResetAnims(MultipleParam[] clips)
	{
		m_clips = clips;
		is_dirty = true;
	}

	public void ResetAnimCount(int count)
	{
		if(count == m_clips.Length)
		{
			return;
		}

		MultipleParam[] new_clips = new MultipleParam[count];
		int data_len = Mathf.Min(new_clips.Length, m_clips.Length);
		System.Array.Copy(m_clips, new_clips, data_len);
		for(int i = data_len; i < new_clips.Length; ++i)
		{
			new_clips[i] = new MultipleParam("", 0, true);
		}
		m_clips = new_clips;
		is_dirty = true;
	}

	public void ChangeMultipleParam(int index, MultipleParam clip)
	{
		m_clips[index] = clip;
		m_clips[index].clip = JAnimationUtility.LoadAni(clip.clip_path);
		m_cycleTime = m_clips[index].clip.cycleTime;
	}

	// 停止在某个位置
	public void StopAt(float time)
	{
		foreach(MultipleParam param in m_clips)
		{
			if(param.clip != null)
			{
				m_pos_setter.SetPose(param.clip, time);
			}
		}
	}

	public void PlayAt(float time)
	{}
}
