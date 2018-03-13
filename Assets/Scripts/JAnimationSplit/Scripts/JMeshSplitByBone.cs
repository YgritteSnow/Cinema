using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JMeshSplitByBone : MonoBehaviour
{
	public GameObject obj;

	public string[] m_focus_bones = new string[0]; // 只关注这些骨骼

	public Dictionary<string, Mesh> m_bone_split; // 分块后的mesh

	public void DoSplit()
	{
		if (m_focus_bones.Length == 0)
		{
			Debug.Log("Focus bone list is empty!");
			return;
		}

		SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
		for (int i = 0; i != renderers.Length; ++i)
		{
			DoSplitOne(renderers[i], m_focus_bones[0]);
		}
	}
	
	void DoSplitOne(SkinnedMeshRenderer child_renderer, string focus_bone)
	{
		Dictionary<int, bool> bone_idx_is_child = IsBoneInRenderer(child_renderer, focus_bone);
		int focus_idx = GetBoneIdx(child_renderer, focus_bone);
		int parent_idx = GetBoneIdx(child_renderer, child_renderer.bones[focus_idx].parent.name);
		Mesh mesh = child_renderer.sharedMesh;

		// 遍历所有 submesh
		List<Mesh> child_mesh_vec = new List<Mesh>();
		List<CombineInstance> child_mesh_comb_vec = new List<CombineInstance>();
		List<Mesh> parent_mesh_vec = new List<Mesh>();
		List<CombineInstance> parent_mesh_comb_vec = new List<CombineInstance>();
		for(int i = 0; i != mesh.subMeshCount; ++i)
		{
			int[] triangles = mesh.GetTriangles(i);
			BoneWeight[] boneWeights = mesh.boneWeights;

			// 遍历所有三角形，根据绑定关系，区分“完全是孩子的三角形”、“完全是父亲的三角形”、“介于两者之间的三角形”
			int[] child_triangles, parent_triangles;
			Dictionary<int, BoneWeight> vertex_child_bone_reset;
			Dictionary<int, BoneWeight> vertex_parent_bone_reset;
			CalVertexOnSideState(mesh, triangles
				, focus_idx, parent_idx, bone_idx_is_child, out child_triangles, out parent_triangles, out vertex_child_bone_reset, out vertex_parent_bone_reset);

			// 将child和parent分别存储为mesh
			Matrix4x4 local_mat = child_renderer.transform.localToWorldMatrix;
			if (child_triangles.Length > 0)
			{
				Mesh new_mesh = JMeshUtility.SplitTriangleToMesh(child_triangles, mesh, triangles, vertex_child_bone_reset);
				child_mesh_vec.Add(new_mesh);
				CombineInstance comb = new CombineInstance();
				comb.mesh = new_mesh;
				comb.transform = Matrix4x4.identity;
				child_mesh_comb_vec.Add(comb);
			}
			if (parent_triangles.Length > 0)
			{
				Mesh new_mesh = JMeshUtility.SplitTriangleToMesh(parent_triangles, mesh, triangles, vertex_parent_bone_reset);
				parent_mesh_vec.Add(new_mesh);
				CombineInstance comb = new CombineInstance();
				comb.mesh = new_mesh;
				comb.transform = Matrix4x4.identity;
				parent_mesh_comb_vec.Add(comb);
			}
		}

		Mesh child_comb = JMeshUtility.CombineSkinnedMeshesOfSameBone(child_mesh_comb_vec.ToArray(), "child");
		Mesh parent_comb = JMeshUtility.CombineSkinnedMeshesOfSameBone(parent_mesh_comb_vec.ToArray(), "parent");

		GameObject new_child = GameObject.Instantiate(child_renderer.gameObject);
		new_child.transform.parent = child_renderer.transform.parent;
		new_child.GetComponent<SkinnedMeshRenderer>().sharedMesh = child_comb;
		GameObject new_parent = GameObject.Instantiate(child_renderer.gameObject);
		new_parent.transform.parent = child_renderer.transform.parent;
		new_parent.GetComponent<SkinnedMeshRenderer>().sharedMesh = parent_comb;
		// 隐藏原模型
		child_renderer.gameObject.SetActive(false);
	}

	Dictionary<int, bool> IsBoneInRenderer(SkinnedMeshRenderer child_renderer, string focus_name)
	{
		Dictionary<int, bool> res = new Dictionary<int, bool>();
		for (int i = 0; i != child_renderer.bones.Length; ++i)
		{
			Transform cur_trans = child_renderer.bones[i];
			bool is_child = false;
			while (cur_trans != null)
			{
				if (cur_trans.name == focus_name)
				{
					is_child = true;
					break;
				}
				cur_trans = cur_trans.parent;
			}
			res[i] = is_child;
		}
		return res;
	}

	int GetBoneIdx(SkinnedMeshRenderer child_renderer, string bone_name)
	{
		for (int i = 0; i != child_renderer.bones.Length; ++i)
		{
			Transform cur_trans = child_renderer.bones[i];
			if(cur_trans.name == bone_name)
			{
				return i;
			}
		}
		return -1;
	}

	void CalVertexOnSideState(Mesh mesh, int[] triangles
		, int child_bone_idx, int parent_bone_idx, Dictionary<int, bool> bone_idx_is_child, out int[] child_triangles, out int[] parent_triangles
		, out Dictionary<int, BoneWeight> vector_child_bone_reset, out Dictionary<int, BoneWeight> vertex_parent_bone_reset)
	{
		bone_idx_is_child[0] = false;

		int child_triangle_count = 0;
		int parent_triangle_count = 0;
		bool[] triangle_is_child = new bool[triangles.Length / 3];

		vector_child_bone_reset = new Dictionary<int, BoneWeight>();
		vertex_parent_bone_reset = new Dictionary<int, BoneWeight>();

		BoneWeight all_child = new BoneWeight();
		all_child.boneIndex0 = child_bone_idx;
		all_child.weight0 = 1;
		BoneWeight all_parent = new BoneWeight();
		all_parent.boneIndex0 = parent_bone_idx;
		all_parent.weight0 = 1;
		for (int triangle_idx = 0; triangle_idx < triangles.Length; triangle_idx = triangle_idx + 3)
		{
			float child_weight = 0;
			float parent_weight = 0;
			for (int j = 0; j < 3; ++j)
			{
				int vi = triangles[triangle_idx + j];
				BoneWeight vw = mesh.boneWeights[vi];

				if(bone_idx_is_child[vw.boneIndex0])
				{
					child_weight += vw.weight0;
				}
				else
				{
					parent_weight += vw.weight0;
				}

				if (bone_idx_is_child[vw.boneIndex1])
				{
					child_weight += vw.weight1;
				}
				else
				{
					parent_weight += vw.weight1;
				}
			}

			if(child_weight > parent_weight)
			{
				++ child_triangle_count;
			}
			else
			{
				++parent_triangle_count;
			}
			triangle_is_child[triangle_idx / 3] = (child_weight > parent_weight);

			// 如果一个顶点同时涉及两个三角形，那么重设这个顶点的 boneWeight 为更靠近的那一边
			if(child_weight > 0 && parent_weight > 0)
			{
				for(int j = 0; j != 3; ++j)
				{
					if (child_weight > parent_weight)
					{
						vector_child_bone_reset[triangles[triangle_idx + j]] = all_child;
					}
					else
					{
						vertex_parent_bone_reset[triangles[triangle_idx + j]] = all_parent;
					}
				}
			}
		}

		// 统计左右两侧的三角形
		child_triangles = new int[child_triangle_count];
		int child_triangle_iter = 0;
		parent_triangles = new int[parent_triangle_count];
		int parent_triangle_iter = 0;
		for (int triangle_idx = 0; triangle_idx != triangle_is_child.Length; ++triangle_idx)
		{
			if (triangle_is_child[triangle_idx])
			{
				child_triangles[child_triangle_iter] = triangle_idx * 3;
				++child_triangle_iter;
			}
			else
			{
				parent_triangles[parent_triangle_iter] = triangle_idx * 3;
				++parent_triangle_iter;
			}
		}
	}
}
