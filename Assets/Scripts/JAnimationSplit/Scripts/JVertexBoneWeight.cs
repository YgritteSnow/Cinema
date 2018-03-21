using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JVertexBoneWeight : MonoBehaviour
{
	public Transform focus_bone = null; 

	Mesh origin_mesh;
	SkinnedMeshRenderer skinned_renderer;

	void Awake()
	{
		skinned_renderer = GetComponent<SkinnedMeshRenderer>();
		origin_mesh = skinned_renderer.sharedMesh;
		InitBoneCache();
	}

	void InitBoneCache()
	{ }

	int GetBoneIdx(string bone_name)
	{
		skinned_renderer = GetComponent<SkinnedMeshRenderer>();
		origin_mesh = skinned_renderer.sharedMesh;

		for(int i = 0; i != skinned_renderer.bones.Length; ++i)
		{
			if (skinned_renderer.bones[i].name == bone_name)
			{
				return i;
			}
		}
		return -1;
	}

	public void ShowNode(string node_name)
	{
		int focus_bone_idx = GetBoneIdx(node_name);
		if(focus_bone_idx < 0)
		{
			return;
		}

		Mesh new_mesh = GameObject.Instantiate(origin_mesh);

		// 把要高亮的那些 uv 表示成 0，不高亮的uv表示成原来的，以此区分
		Vector2[] uvs = new Vector2[new_mesh.uv.Length];
		for(int i = 0; i != new_mesh.vertices.Length; ++i)
		{
			BoneWeight bw = new_mesh.boneWeights[i];
			bool is_interested = bw.boneIndex0 == focus_bone_idx || bw.boneIndex1 == focus_bone_idx;
			uvs[i] = is_interested ? Vector2.zero : new Vector2(0.5f, 0.5f);
		}

		new_mesh.vertices = origin_mesh.vertices;
		new_mesh.triangles = origin_mesh.triangles;
		new_mesh.uv = uvs;

		skinned_renderer.sharedMesh = new_mesh;
		return;
	}
}
