using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JMeshSplitByBone : MonoBehaviour {
	public GameObject obj;

	public string[] m_focus_bones; // 只关注这些骨骼

	public Dictionary<string, float> m_bone_volume; // 骨骼的质量
	public Dictionary<string, Mesh> m_bone_split; // 分块后的mesh

	public void DoSplit()
	{
		if (m_focus_bones.Length == 0)
		{
			Debug.Log("Focus bone list is empty!");
			return;
		}

		m_bone_volume = new Dictionary<string, float>();
		foreach(string bone in m_focus_bones)
		{
			m_bone_volume[bone] = 0f;
		}

		SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
		for (int i = 0; i != renderers.Length; ++i)
		{
			DoSplitOne(renderers[i], m_focus_bones[0]);
		}
	}

	[System.Obsolete("not useful now.")]
	Dictionary<string, string> CacheFocusedBoneAndParent()
	{
		var m_focus_bones_parent = new Dictionary<string, string>();
		foreach(string bone in m_focus_bones)
		{
			m_focus_bones_parent[bone] = JAnimationUtility.TraverseFind(transform, bone).parent.name;
		}
		return m_focus_bones_parent;
	}

	Dictionary<int, int> GetBoneIdx2ParentIdx(SkinnedMeshRenderer child_renderer, string focus_name, out int focus_idx, out int focus_parentIdx)
	{
		Dictionary<string, int> bone_name_2_idx = new Dictionary<string, int>();
		Dictionary<int, string> bone_idx_2_parentName = new Dictionary<int, string>();
		for (int i = 0; i != child_renderer.bones.Length; ++i)
		{
			bone_name_2_idx[child_renderer.bones[i].name] = i;
			bone_idx_2_parentName[i] = child_renderer.bones[i].parent.name;
		}
		Dictionary<int, int> bone_idx_2_parentIdx = new Dictionary<int, int>();
		foreach (KeyValuePair<int, string> bone in bone_idx_2_parentName)
		{
			bone_idx_2_parentIdx[bone.Key] = bone_name_2_idx[bone.Value];
		}
		focus_idx = bone_name_2_idx[focus_name];
		focus_parentIdx = bone_idx_2_parentIdx[focus_idx];
		return bone_idx_2_parentIdx;
	}

	Dictionary<int, bool> GetBoneIsChildMap(SkinnedMeshRenderer child_renderer, string focus_name)
	{
		Dictionary<int, bool> res = new Dictionary<int, bool>();
		for (int i = 0; i != child_renderer.bones.Length; ++i)
		{
			Transform cur_trans = child_renderer.bones[i];
			bool is_child = false;
			while(cur_trans != null)
			{
				if(cur_trans.name == focus_name)
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

	int VertexIdxToEdgeId(int vi1, int vi2)
	{
		return vi1 < vi2 ? vi1 * 10000 + vi2 : vi2 * 10000 + vi1;
	}
	void EdgeIdToVertexIdx(int edge_id, out int vi1, out int vi2)
	{
		vi1 = edge_id % 10000;
		vi2 = edge_id / 10000;
	}
	float CalVertexCrossParam(int vertex, Mesh mesh, Dictionary<int, bool> child_bones)
	{
		float lw = 0;
		float rw = 0;
		BoneWeight bw = mesh.boneWeights[vertex];
		if (bw.boneIndex0 != 0)
		{
			if (child_bones.ContainsKey(bw.boneIndex0))
			{
				lw += bw.weight0;
			}
			else
			{
				rw += bw.weight0;
			}
		}
		if (bw.boneIndex1 != 0)
		{
			if (child_bones.ContainsKey(bw.boneIndex1))
			{
				lw += bw.weight1;
			}
			else
			{
				rw += bw.weight1;
			}
		}
		return lw * lw + rw * rw;
	}
	float CalTriangleCrossParam(int triangle, Mesh mesh, Dictionary<int, bool> child_bones)
	{
		int v1 = mesh.triangles[triangle];
		int v2 = mesh.triangles[triangle+1];
		int v3 = mesh.triangles[triangle+2];
		float lw = 0;
		float rw = 0;
		for(int i = 0; i != 3; ++i)
		{
			int v = mesh.triangles[triangle + i];
			BoneWeight bw = mesh.boneWeights[v];
			if(bw.boneIndex0 != 0)
			{
				if (child_bones.ContainsKey(bw.boneIndex0))
				{
					lw += bw.weight0;
				}
				else
				{
					rw += bw.weight0;
				}
			}
		}
		return lw * lw + rw * rw;
	}

	#region 统计三角形边 => 三角形列表
	Dictionary<int, List<int>> GetTriangleEdge2Triangle(int[] triangles, Mesh mesh)
	{
		Dictionary<int, List<int>> edge_2_triangle = new Dictionary<int, List<int>>();
		foreach (int triangle_idx in triangles)
		{
			int vi1 = mesh.triangles[triangle_idx+0];
			int vi2 = mesh.triangles[triangle_idx+1];
			int vi3 = mesh.triangles[triangle_idx+2];
			int edge_id = VertexIdxToEdgeId(vi1, vi2);
			if (!edge_2_triangle.ContainsKey(edge_id))
			{
				edge_2_triangle[edge_id] = new List<int>();
			}
			edge_2_triangle[edge_id].Add(triangle_idx);

			edge_id = VertexIdxToEdgeId(vi1, vi3);
			if (!edge_2_triangle.ContainsKey(edge_id))
			{
				edge_2_triangle[edge_id] = new List<int>();
			}
			edge_2_triangle[edge_id].Add(triangle_idx);

			edge_id = VertexIdxToEdgeId(vi3, vi2);
			if (!edge_2_triangle.ContainsKey(edge_id))
			{
				edge_2_triangle[edge_id] = new List<int>();
			}
			edge_2_triangle[edge_id].Add(triangle_idx);
		}
		return edge_2_triangle;
	}
	#endregion

	#region 计算三角形 => 相邻三角形列表
	Dictionary<int, List<int>> GetTriangle2TriangleList(int[] triangles, Mesh mesh, Dictionary<int, List<int>> edge_2_triangle)
	{
		Dictionary<int, List<int>> triangle_2_neighbours = new Dictionary<int, List<int>>();
		foreach (int triangle_idx in triangles)
		{
			int vi1 = mesh.triangles[triangle_idx+0];
			int vi2 = mesh.triangles[triangle_idx+1];
			int vi3 = mesh.triangles[triangle_idx+2];
			triangle_2_neighbours[triangle_idx] = new List<int>();

			int edge_id = VertexIdxToEdgeId(vi1, vi2);
			foreach (int check_triangle in edge_2_triangle[edge_id])
			{
				if (check_triangle != triangle_idx)
				{
					triangle_2_neighbours[triangle_idx].Add(check_triangle);
				}
			}

			edge_id = VertexIdxToEdgeId(vi1, vi3);
			foreach (int check_triangle in edge_2_triangle[edge_id])
			{
				if (check_triangle != triangle_idx)
				{
					triangle_2_neighbours[triangle_idx].Add(check_triangle);
				}
			}

			edge_id = VertexIdxToEdgeId(vi3, vi2);
			foreach (int check_triangle in edge_2_triangle[edge_id])
			{
				if (check_triangle != triangle_idx)
				{
					triangle_2_neighbours[triangle_idx].Add(check_triangle);
				}
			}
		}
		return triangle_2_neighbours;
	}
	#endregion

	#region 顶点 => 邻居顶点列表
	Dictionary<int, List<int>> GetVertex2VertexList(int[] cross_triangles, Mesh mesh, Dictionary<int, List<int>> edge_2_triangle)
	{
		Dictionary<int, List<int>> res = new Dictionary<int,List<int>>();
		foreach(int triangle in cross_triangles)
		{
			TraverseTriangleEdge(triangle, mesh, delegate(int v1, int v2, int v3)
			{
				if(!res.ContainsKey(v1))
				{
					res[v1] = new List<int>();	
				}
				bool already_has_v2 = false;
				bool already_has_v3 = false;
				foreach(int nei_vert in res[v1])
				{
					already_has_v2 = already_has_v2 || nei_vert == v2;
					already_has_v3 = already_has_v3 || nei_vert == v3;
				}
				if(!already_has_v2)
				{
					res[v1].Add(v2);
				}
				if (!already_has_v3)
				{
					res[v1].Add(v3);
				}
				return true;
			});
		}
		return res;
	}
	#endregion

	#region 将三角形列表根据其“跨越父子骨骼的程度”排序
	[System.Obsolete("Should not be useful. Sort func needs to be optimized by the way", true)]
	void SortTriangleListByParam(SkinnedMeshRenderer child_renderer, string focus_bone, List<int> cross_triangles)
	{
		Dictionary<int, bool> bone_is_child = GetBoneIsChildMap(child_renderer, focus_bone);
		cross_triangles.Sort(delegate (int lt, int rt)
			{
				float lparam = CalTriangleCrossParam(lt, child_renderer.sharedMesh, bone_is_child);
				float rparam = CalTriangleCrossParam(rt, child_renderer.sharedMesh, bone_is_child);
				return lparam > rparam ? -1 : (lparam < rparam ? 1 : 0);
			});
	}
	#endregion

	void DoSplitOne(SkinnedMeshRenderer child_renderer, string focus_bone)
	{
		Dictionary<int, bool> bone_idx_is_child = GetBoneIsChildMap(child_renderer, focus_bone);

		Mesh mesh = child_renderer.sharedMesh;

		// 遍历所有三角形，根据绑定关系，区分“完全是孩子的三角形”、“完全是父亲的三角形”、“介于两者之间的三角形”
		int[] cross_triangles, child_triangles, parent_triangles;
		int[] vertex_side_state;
		CalVertexOnSideState(mesh, bone_idx_is_child, out vertex_side_state, out cross_triangles, out child_triangles, out parent_triangles);

		if(cross_triangles.Length != 0)
		{
			Dictionary<int, bool> child_side_vertex, parent_side_vertex; // 在cross_triangle中，同时位于child_side/parent_side的点
			FindSplitVertexOnTwoSides(mesh, vertex_side_state, out child_side_vertex, out parent_side_vertex);

			List<int> cross_child_triangles;
			List<int> cross_parent_triangles;
			SplitChildAndParent(mesh, cross_triangles, child_side_vertex, parent_side_vertex, bone_idx_is_child, out cross_child_triangles, out cross_parent_triangles);

			int[] final_child_triangles = new int[child_triangles.Length + cross_child_triangles.Count];
			child_triangles.CopyTo(final_child_triangles, 0);
			cross_child_triangles.CopyTo(final_child_triangles, child_triangles.Length);

			int[] final_parent_triangles = new int[parent_triangles.Length + cross_parent_triangles.Count];
			parent_triangles.CopyTo(final_parent_triangles, 0);
			cross_parent_triangles.CopyTo(final_parent_triangles, parent_triangles.Length);
		}

		// 将child和parent分别存储为mesh
		if(child_triangles.Length > 0)
		{
			Mesh new_child_mesh = JMeshUtility.SplitTriangleToMesh(child_triangles, mesh);
			GameObject new_child = GameObject.Instantiate(child_renderer.gameObject);
			new_child.GetComponent<SkinnedMeshRenderer>().sharedMesh = new_child_mesh;
		}
		if(parent_triangles.Length > 0)
		{
			Mesh new_parent_mesh = JMeshUtility.SplitTriangleToMesh(parent_triangles, mesh);
			GameObject new_parent = GameObject.Instantiate(child_renderer.gameObject);
			new_parent.GetComponent<SkinnedMeshRenderer>().sharedMesh = new_parent_mesh;
		}

		// 隐藏原模型
		child_renderer.gameObject.SetActive(false);
	}

	void CalVertexOnSideState(Mesh mesh, Dictionary<int, bool> bone_idx_is_child, out int[] vertice_state, out int[] cross_triangles, out int[] child_triangles, out int[] parent_triangles)
	{
		int child_triangle_count = 0;
		int parent_triangle_count = 0;
		int cross_triangle_count = 0;
		vertice_state = new int[mesh.vertices.Length];
		int[] triangle_side_state = new int[mesh.triangles.Length / 3];
		for (int triangle_idx = 0; triangle_idx < mesh.triangles.Length; triangle_idx = triangle_idx + 3)
		{
			bool has_childIdx = false;
			bool has_parentIdx = false;
			for (int j = 0; j < 3; ++j)
			{
				int vi = mesh.triangles[triangle_idx + j];
				BoneWeight vw = mesh.boneWeights[vi];
				bool is_bone0_child = vw.boneIndex0 != 0 && bone_idx_is_child[vw.boneIndex0];
				bool is_bone1_child = vw.boneIndex1 != 0 && bone_idx_is_child[vw.boneIndex1];
				bool is_bone0_parent = vw.boneIndex0 != 0 && !is_bone0_child;
				bool is_bone1_parent = vw.boneIndex1 != 0 && !is_bone1_child;

				// 统计顶点所属部位
				if(is_bone0_child || is_bone1_child)
				{
					has_childIdx = true;
					vertice_state[vi] |= 0x1;
				}
				if (is_bone0_parent || is_bone1_parent)
				{
					has_parentIdx = true;
					vertice_state[vi] |= 0x2;
				}
			}

			// 统计三角形所属部位
			if (has_childIdx && has_parentIdx)
			{
				triangle_side_state[triangle_idx/3] |= 0x4; // 0x4 代表cross
				++cross_triangle_count;
			}
			else if (has_childIdx)
			{
				triangle_side_state[triangle_idx/3] |= 0x1;
				++child_triangle_count;
			}
			else if (has_parentIdx)
			{
				triangle_side_state[triangle_idx/3] |= 0x2;
				++parent_triangle_count;
			}
		}

		// 统计三角形们
		cross_triangles = new int[cross_triangle_count];
		int cross_triangle_iter = 0;
		child_triangles = new int[child_triangle_count];
		int child_triangle_iter = 0;
		parent_triangles = new int[parent_triangle_count];
		int parent_triangle_iter = 0;
		for(int triangle_idx = 0; triangle_idx != triangle_side_state.Length; ++triangle_idx)
		{
			if(triangle_side_state[triangle_idx] == 0x4)
			{
				cross_triangles[cross_triangle_iter++] = triangle_idx * 3;
			}
			else if(triangle_side_state[triangle_idx] == 0x1)
			{
				child_triangles[child_triangle_iter++] = triangle_idx * 3;
			}
			else if (triangle_side_state[triangle_idx] == 0x2)
			{
				parent_triangles[parent_triangle_iter++] = triangle_idx * 3;
			}
		}
		return;
	}
	
	// 找到位于两个轮廓上的顶点
	void FindSplitVertexOnTwoSides(Mesh mesh, int[] vertex_side_state
		, out Dictionary<int, bool> child_side_vertex, out Dictionary<int, bool> parent_side_vertex)
	{
		child_side_vertex = new Dictionary<int, bool>();
		parent_side_vertex = new Dictionary<int, bool>();
		for(int vidx = 0; vidx != vertex_side_state.Length; ++vidx)
		{
			if((vertex_side_state[vidx] | 0x1) == 0x1)
			{
				child_side_vertex[vidx] = true;
			}
			else if ((vertex_side_state[vidx] | 0x2) == 0x2)
			{
				parent_side_vertex[vidx] = true;
			}
		}
	}

	// 找到三角形带的分割线
	void SplitChildAndParent(Mesh mesh, int[] cross_triangle, Dictionary<int, bool> child_side_vertex, Dictionary<int, bool> parent_side_vertex, Dictionary<int, bool> bone_idx_is_child, out List<int> cross_child_triangles, out List<int> cross_parent_triangles)
	{
		// 建立边=>三角形的索引
		Dictionary<int, List<int>> edge_2_triangle = GetTriangleEdge2Triangle(cross_triangle, mesh);
		// 建立顶点=>邻接顶点的索引
		Dictionary<int, List<int>> vert_2_neigh = GetVertex2VertexList(cross_triangle, mesh, edge_2_triangle);

		Dictionary<int, float> all_vert_param = new Dictionary<int, float>();
		foreach(int triangle in cross_triangle)
		{
			for(int vi = 0; vi != 3; ++vi)
			{
				int vert_idx = mesh.triangles[triangle + vi];
				all_vert_param[vert_idx] = CalVertexCrossParam(vert_idx, mesh, bone_idx_is_child);
			}
		}

		List<int> all_vert = new List<int>();
		foreach(int vidx in all_vert_param.Keys)
		{
			all_vert.Add(vidx);
		}
		all_vert.Sort(delegate (int lv, int rv)
		{
			float lp = all_vert_param[lv];
			float rp = all_vert_param[rv];
			if (lp < rp)
			{
				return -1;
			}
			else if (lp == rp)
			{
				return 0;
			}
			else
			{
				return 1;
			}
		});

		// 选取权重最高的那个点为第1个点
		int path_vert = all_vert[0];
		int path_vert_neigh = vert_2_neigh[path_vert][0];
		int path_vert_other_nei = -1;
		int edge_id = VertexIdxToEdgeId(path_vert, path_vert_neigh);
		int path_start_triangle = edge_2_triangle[edge_id][0];

		int need_search_child = -1;
		int need_search_parent = -1;
		TraverseTriangleEdge(path_start_triangle, mesh, delegate(int v1, int v2, int v3)
		{
			if(v1 != path_vert)
			{
				if(child_side_vertex.ContainsKey(v1))
				{
					need_search_child = v1;
				}
				else if(parent_side_vertex.ContainsKey(v1))
				{
					need_search_parent = v1;
				}

				if (v1 != path_vert_neigh)
				{
					path_vert_other_nei = v1;
				}
			}
			return true;
		});

		List<int> child_edge_path = new List<int>();
		List<int> parent_edge_path = new List<int>();
		int child_end_triangle = -1;
		int parent_end_triangle = -1;
		int split_vert_start = -1;
		int split_vert_stop = -1;
		if (need_search_child < 0 && need_search_parent < 0)
		{
			child_edge_path = FindTrianglePathToOtherSide(path_start_triangle, VertexIdxToEdgeId(path_vert, path_vert_other_nei)
				, mesh, parent_side_vertex, child_side_vertex, edge_2_triangle, vert_2_neigh);
			parent_edge_path = FindTrianglePathToOtherSide(path_start_triangle, VertexIdxToEdgeId(path_vert_neigh, path_vert)
				, mesh, child_side_vertex, parent_side_vertex, edge_2_triangle, vert_2_neigh);

			child_end_triangle = edge_2_triangle[child_edge_path[child_edge_path.Count - 1]][0];
			parent_end_triangle = edge_2_triangle[parent_edge_path[parent_edge_path.Count - 1]][0];

			split_vert_start = path_vert;
			split_vert_stop = path_vert_other_nei;
		}
		else if(need_search_child < 0)
		{
			child_edge_path = FindTrianglePathToOtherSide(path_start_triangle, VertexIdxToEdgeId(path_vert, need_search_parent)
				, mesh, parent_side_vertex, child_side_vertex, edge_2_triangle, vert_2_neigh);
			parent_edge_path = FindTrianglePathToOtherSide(path_start_triangle, VertexIdxToEdgeId(path_vert_neigh, path_vert)
				, mesh, child_side_vertex, parent_side_vertex, edge_2_triangle, vert_2_neigh);

			child_end_triangle = edge_2_triangle[child_edge_path[child_edge_path.Count - 1]][0];
			parent_end_triangle = path_start_triangle;

			split_vert_start = path_vert;
			split_vert_stop = need_search_parent;
		}
		else if (need_search_parent < 0)
		{
			child_edge_path = FindTrianglePathToOtherSide(path_start_triangle, VertexIdxToEdgeId(path_vert, path_vert_other_nei)
				, mesh, parent_side_vertex, child_side_vertex, edge_2_triangle, vert_2_neigh);
			parent_edge_path = FindTrianglePathToOtherSide(path_start_triangle, VertexIdxToEdgeId(path_vert, need_search_child)
				, mesh, child_side_vertex, parent_side_vertex, edge_2_triangle, vert_2_neigh);

			child_end_triangle = path_start_triangle;
			parent_end_triangle = edge_2_triangle[parent_edge_path[parent_edge_path.Count - 1]][0];

			split_vert_start = path_vert;
			split_vert_stop = need_search_child;
		}

		// ！注意此步以后的vert_2_neigh已经剪掉一部分了，不是原有的数据了！
		SplitVertNeigh(child_edge_path, vert_2_neigh, mesh);
		SplitVertNeigh(parent_edge_path, vert_2_neigh, mesh);
		List<int> vertex_split_path = VertexNavigation(split_vert_start, split_vert_stop, mesh, vert_2_neigh);

		#if UNITY_EDITOR
		for(int i = 0; i != vertex_split_path.Count; ++i)
		{
			Vector3 v1 = mesh.vertices[vertex_split_path[i]];
			Vector3 v2 = mesh.vertices[vertex_split_path[(i+1)%vertex_split_path.Count]];
			Debug.DrawLine(v1, v2, Color.green, 10);
		}
		#endif

		// 生成面片！
		Mesh surface_mesh = JMeshUtility.GenerateSurfaceMeshByVertexList(vertex_split_path, mesh.vertices);

		// 剔除用来剪三角形带为两个的三角形的邻边们
		// ！注意此步以后的 triangle_2_neigh 已经剪掉了一部分，不是原有的数据了！
		SplitTriangleNeigh(edge_2_triangle, vertex_split_path);
		// 剔除后，建立三角形=>邻接三角形的索引
		Dictionary<int, List<int>> triangle_2_neigh = GetTriangle2TriangleList(cross_triangle, mesh, edge_2_triangle);

		cross_child_triangles = GetAllNeighTriangles(child_end_triangle, triangle_2_neigh);
		cross_parent_triangles = GetAllNeighTriangles(parent_end_triangle, triangle_2_neigh);
	}

	List<int> GetAllNeighTriangles(int triangle, Dictionary<int, List<int>> triangle_2_neigh)
	{
		List<int> res = new List<int>();
		JMeshUtility.TraverseMap<int>(triangle, triangle_2_neigh, delegate(int node)
		{
			res.Add(node);
			return true;
		});
		return res;
	}

	// 剔除 边=>三角形 中的一部分边
	void SplitTriangleNeigh(Dictionary<int, List<int>> edge_2_triangle, List<int> vertex_path)
	{
		for(int i = 0; i != vertex_path.Count; ++i)
		{
			int vbgn = vertex_path[i];
			int vend = vertex_path[(i+1) % vertex_path.Count];
			int edge_id = VertexIdxToEdgeId(vbgn, vend);
			edge_2_triangle[edge_id] = new List<int>();
		}
	}

	int FindTriangleOtherVertex(int triangle, int edge_id, Mesh mesh)
	{
		int result_vert = -1;
		TraverseTriangleEdge(triangle, mesh, delegate(int ver1, int ver2, int ver_other)
		{
			if (edge_id == VertexIdxToEdgeId(ver1, ver2))
			{
				result_vert = ver_other;
				return false;
			}
			return true;
		});
		return result_vert;
	}

	// 从三角形带的这一侧的一个三角形开始，找到某侧的一个点为止，中途不能经过任何边缘上的点
	List<int> FindTrianglePathToOtherSide(int bgn_triangle, int bgn_edge, Mesh mesh, Dictionary<int, bool> this_side_vers, Dictionary<int, bool> target_side_vers, Dictionary<int, List<int>> edge_2_triangle, Dictionary<int, List<int>> vert_2_neigh)
	{
		List<int> result_triangle_path = new List<int>();
		List<int> result_edge_id_path_inside = new List<int>();

		List<int> pathed_vers = new List<int>();
		int bgn_edge_v1, bgn_edge_v2;
		EdgeIdToVertexIdx(bgn_edge, out bgn_edge_v1, out bgn_edge_v2);
		pathed_vers.Add(bgn_edge_v1);
		pathed_vers.Add(bgn_edge_v2);

		int last_edge = bgn_edge; // 上一次的边
		int last_triangle = bgn_triangle; // 上次的三角形
		int last_vertex = FindTriangleOtherVertex(bgn_triangle, last_edge, mesh); // 下次的顶点
		result_triangle_path.Add(last_triangle);// 把第一个三角形加入路径

		result_edge_id_path_inside.Add(last_edge); // 把上一条边放入路径
		pathed_vers.Add(last_vertex);

		// 依次寻找剩下的三角形
		if(!target_side_vers.ContainsKey(last_vertex))
		{
			FindTrianglePathInner(last_triangle, last_edge, last_vertex, edge_2_triangle, pathed_vers, mesh, result_triangle_path, result_edge_id_path_inside, delegate(int vert, int triangle, int edge)
			{
				if(target_side_vers.ContainsKey(vert))
				{
					return 1;
				}
				else if(this_side_vers.ContainsKey(vert))
				{
					return -1;
				}
				else
				{
					return 0;
				}
			});

			last_vertex = pathed_vers[pathed_vers.Count - 1];
			last_triangle = result_triangle_path[result_triangle_path.Count - 1];
		}

#if UNITY_EDITOR
		for (int i = 0; i != result_edge_id_path_inside.Count; ++i)
		{
			int result_edge_v1, result_edge_v2;
			EdgeIdToVertexIdx(result_edge_id_path_inside[i], out result_edge_v1, out result_edge_v2);
			Vector3 v1 = mesh.vertices[result_edge_v1];
			Vector3 v2 = mesh.vertices[result_edge_v2];
			Debug.DrawLine(v1, v2, Color.red, 10);
		}
#endif

		// 尾部可能不足以把三角形带切开，应当寻找到紧贴边缘为止
		List<int> added_edges;
		FindTriangleToEdgeAroundOneVertex(last_vertex, last_triangle, mesh, edge_2_triangle, vert_2_neigh, out added_edges);
		foreach(int edge_id in added_edges)
		{
			result_edge_id_path_inside.Add(edge_id);
		}

		return result_edge_id_path_inside;
	}

	void SplitVertNeigh(List<int> edge_id_path_inside, Dictionary<int, List<int>> vert_2_neigh, Mesh mesh)
	{
		if(edge_id_path_inside.Count == 0)
		{
			return;
		}

		// 将edgeid所代表的所有的vertex到vertex的联系断开
		foreach (int edge_id in edge_id_path_inside)
		{
			int need_split_v1, need_split_v2;
			EdgeIdToVertexIdx(edge_id, out need_split_v1, out need_split_v2);
			List<int> v1_neigh = vert_2_neigh[need_split_v1];
			v1_neigh.Remove(need_split_v2);
			vert_2_neigh[need_split_v1]  = v1_neigh;
			List<int> v2_neigh = vert_2_neigh[need_split_v2];
			v2_neigh.Remove(need_split_v1);
			vert_2_neigh[need_split_v2] = v2_neigh;
		}
	}

	// 使用A*寻路，对所有顶点寻路
	List<int> VertexNavigation(int split_start_vert, int split_stop_vert, Mesh mesh, Dictionary<int, List<int>> vert_2_neigh)
	{
		return JAnimationUtility.AStarNavigation<int>(split_start_vert, split_stop_vert, vert_2_neigh, delegate(int v1, int v2)
		{
			return (mesh.vertices[v1] - mesh.vertices[v2]).magnitude;
		},
		delegate(int v1, int v2)
		{
			return v1 == v2;
		}
		);
	}

	delegate int FindTrianglePathFunc(int vert, int triangle, int edge);
	int FindTrianglePathInner(int last_triangle, int last_edge_id, int last_vertex, Dictionary<int, List<int>> edge_2_triangle
		, List<int> checked_points, Mesh mesh, List<int> triangle_path, List<int> edge_path, FindTrianglePathFunc check_func)
	{
		int check_res = -1;
		int checked_vert = -1;
		int checked_triangle = -1;
		int checked_edge = -1;
		TraverseTriangleEdge(last_triangle, mesh, delegate(int v1, int v2, int vother)
		{
			int edge_id = VertexIdxToEdgeId(v1, v2);
			if (edge_id == last_edge_id)
			{
				return true; // 继续查找
			}

			int nei_triangle = -1;
			int nei_vert = FindTriangleOtherVertexAndTriangleByEdgeId(last_triangle, edge_id, edge_2_triangle, mesh, out nei_triangle);

			if (nei_triangle > 0 && !checked_points.Exists((int v) => v == nei_vert))
			{
				int cur_check_res = check_func(nei_vert, nei_triangle, edge_id);
				if (cur_check_res == -1)
				{
					return true; // 不合法，无视这个节点，继续查找后边的节点
				}
				else if (cur_check_res == 0)
				{
					check_res = 0;
					checked_vert = nei_vert;
					checked_triangle = nei_triangle;
					checked_edge = edge_id;

					checked_points.Add(checked_vert);
					edge_path.Add(checked_edge);
					triangle_path.Add(checked_triangle);
					check_res = FindTrianglePathInner(checked_triangle, checked_edge, checked_vert, edge_2_triangle, checked_points, mesh, triangle_path, edge_path, check_func);
					if(check_res == -1)
					{
						checked_points.RemoveAt(checked_points.Count - 1);
						edge_path.Remove(edge_path.Count - 1);
						triangle_path.Remove(triangle_path.Count - 1);
						return true; // 合法，可以由此继续查找。保存下当前的情况，仍然继续此循环，看能不能找到可以终止查找的点
					}
					else
					{
						return false;
					}
				}
				else
				{
					check_res = 1; // 查找完毕，可以终止查找
					checked_vert = nei_vert;
					checked_triangle = nei_triangle;
					checked_edge = edge_id;

					checked_points.Add(checked_vert);
					edge_path.Add(checked_edge);
					triangle_path.Add(checked_triangle);
					return false;
				}
			}
			return true;
		});

		return check_res;
	}

	// 找到一个triangle上和edge_id不同的那个点
	int FindTriangleOtherVertexByEdgeId(int triangle, int edge_id, Mesh mesh)
	{
		int res_vert = -1;
		TraverseTriangleEdge(triangle, mesh, delegate(int v1, int v2, int vother)
		{
			if(edge_id == VertexIdxToEdgeId(v1, v2))
			{
				res_vert = vother;
				return false;
			}
			return true;
		});
		return res_vert;
	}

	// 找到edge_id上非此triangle的那个triangle和那个点
	int FindTriangleOtherVertexAndTriangleByEdgeId(int last_triangle, int edge_id, Dictionary<int, List<int>> edge_2_triangles, Mesh mesh, out int triangle_id)
	{
		foreach(int triangle in edge_2_triangles[edge_id])
		{
			if(triangle != last_triangle)
			{
				triangle_id = triangle;
				return FindTriangleOtherVertexByEdgeId(triangle, edge_id, mesh);
			}
		}
		triangle_id = -1;
		return -1;
	}

	delegate bool TraverseTriangleEdgeFunc(int v1, int v2, int vother);
	bool TraverseTriangleEdge(int triangle, Mesh mesh, TraverseTriangleEdgeFunc traverseFunc)
	{
		for(int vidx_idx = 0; vidx_idx != 3; ++vidx_idx)
		{
			int ver1 = mesh.triangles[triangle + vidx_idx];
			int ver2 = mesh.triangles[triangle + (vidx_idx + 1) % 3];
			int vother = mesh.triangles[triangle + (vidx_idx + 2) % 3];
			if(!traverseFunc(ver1, ver2, vother))
			{
				return false; // 提前退出
			}
		}
		return true; // 正常遍历完毕退出
	}

	int FindAnyTriangleByVertex(int vert, List<int> triangle_collect, Mesh mesh)
	{
		foreach(int triangle in triangle_collect)
		{
			if(!TraverseTriangleEdge(triangle, mesh, delegate(int v1, int v2, int v3)
			{
				if(v1 == vert)
				{
					return false;
				}
				return true;
			}))
			{
				return triangle;
			}
		}
		return -1;
	}

	// 在给定的三角形中，找到一个包含指定vertex的三角形
	[System.Obsolete("Useless now.", true)]
	bool FindEdgeTriangleByVertex(int vert, Mesh mesh, Dictionary<int, List<int>> vert_2_neigh, Dictionary<int, List<int>> edge_2_triangle
		, out int triangle, out int edge_id)
	{
		foreach(int neigh_vert in vert_2_neigh[vert])
		{
			edge_id = VertexIdxToEdgeId(vert, neigh_vert);
			if(edge_2_triangle[edge_id].Count == 1)
			{
				triangle = edge_2_triangle[edge_id][0];
				return true;
			}
		}
		triangle = -1;
		edge_id = -1;
		return false;
	}

	float CalVertDist(Mesh mesh, int vidx1, int vidx2)
	{
		return (mesh.vertices[vidx1] - mesh.vertices[vidx2]).sqrMagnitude;
	}

	// 统计顶点 => 顶点的邻居列表
	Dictionary<int, List<int>> CalVertToNeighbour(Mesh mesh, List<int> focus_triangles)
	{
		Dictionary<int, List<int>> vert2neighbour = new Dictionary<int, List<int>>();
		foreach(int triangle_idx in focus_triangles)
		{
			for(int vidx_idx = 0; vidx_idx != 3; ++vidx_idx)
			{
				int vidx = mesh.triangles[triangle_idx + vidx_idx];
				if(!vert2neighbour.ContainsKey(vidx))
				{
					vert2neighbour[vidx] = new List<int>();
				}
				for(int vneigh_idx = 0; vneigh_idx != 3; ++vneigh_idx)
				{
					if(vneigh_idx != vidx_idx)
					{
						int vneigh = mesh.triangles[triangle_idx + vneigh_idx];
						bool is_repeat = false;
						foreach(int already_neigh in vert2neighbour[vidx])
						{
							if(already_neigh == vneigh)
							{
								is_repeat = true;
								break;
							}
						}
						if(!is_repeat)
						{
							vert2neighbour[vidx].Add(vneigh);
						}
					}
				}
			}
		}
		return vert2neighbour;
	}

	// 按照给定的优先级来寻路
	delegate int VertSortCompareFunc(int lh_vert_idx, int rh_vert_idx);
	bool CalVertPathByFunc(Mesh mesh, int bgn_vert, int end_vert, Dictionary<int, List<int>> vert2neigh, VertSortCompareFunc sort_func,ref Dictionary<int, bool> checked_verts, ref List<int> path)
	{
		List<int> neighs = vert2neigh[bgn_vert];
		if (neighs.Count == 0)
		{
			return false;
		}

		List<int> copy_neighs = new List<int>();
		foreach (int vert in neighs)
		{
			copy_neighs.Add(vert);
		}
		copy_neighs.Sort(delegate (int v1, int v2)
		{
			return sort_func(v1, v2);
		});

		foreach (int nei in copy_neighs)
		{
			if (checked_verts.ContainsKey(nei))
			{ }
			else if (nei == end_vert)
			{
				return true;
			}
			else
			{
				path.Add(nei);
				checked_verts[nei] = true;
				if (CalVertPathByFunc(mesh, nei, end_vert, vert2neigh, sort_func, ref checked_verts, ref path))
				{
					return true;
				}
				else
				{
					path.RemoveAt(path.Count - 1);
				}
			}
		}
		return false;
	}

	// 从vidx1到vidx2的距离最近的路径
	bool CalVertPathByMinDist(Mesh mesh, int bgn_vert, int end_vert, Dictionary<int, List<int>> vert2neigh, ref List<int> path)
	{
		Vector3 end_pos = mesh.vertices[end_vert];
		Vector3 bgn_pos = mesh.vertices[bgn_vert];
		Vector3 need_dir = end_pos - bgn_pos;

		Dictionary<int, bool> checked_verts = new Dictionary<int, bool>();
		return CalVertPathByFunc(mesh, bgn_vert, end_vert, vert2neigh, delegate (int lh, int rh)
		{
			float dir1 = Vector3.Dot(mesh.vertices[lh] - bgn_pos, need_dir);
			float dir2 = Vector3.Dot(mesh.vertices[rh] - bgn_pos, need_dir);
			return dir1 > dir2 ? 1 : (dir1 < dir2 ? -1 : 0);
		}, ref checked_verts, ref path);
	}

	// 按照目标点的权重来寻路
	bool CalVertPathByWeight(Mesh mesh, int bgn_vert, int end_vert, Dictionary<int, List<int>> vert2neigh, Dictionary<int, bool> bone_idx_is_child, ref List<int> path)
	{
		Dictionary<int, bool> checked_verts = new Dictionary<int, bool>();
		return CalVertPathByFunc(mesh, bgn_vert, end_vert, vert2neigh, delegate (int lh, int rh)
		{
			BoneWeight lw = mesh.boneWeights[lh];
			BoneWeight rw = mesh.boneWeights[rh];
			float lparam = CalVertexCrossParam(lh, mesh, bone_idx_is_child);
			float rparam = CalVertexCrossParam(rh, mesh, bone_idx_is_child);
			return lparam > rparam ? 1 : (lparam < rparam ? -1 : 0);
		}, ref checked_verts, ref path);
	}

	// 获得顶点=>三角形列表
	Dictionary<int, List<int>> CalPathTriangles(List<int> path, Mesh mesh, int[] cross_triangles)
	{
		Dictionary<int, List<int>> vidx2triangle = new Dictionary<int, List<int>>();
		foreach (int path_v in path)
		{
			vidx2triangle[path_v] = new List<int>();
		}

		foreach (int triangle in cross_triangles)
		{
			for (int vidx_idx = 0; vidx_idx != 3; ++vidx_idx)
			{
				int vidx = mesh.triangles[triangle + vidx_idx];
				if (vidx2triangle.ContainsKey(vidx))
				{
					// 检查是否已经有了
					bool already_has = false;
					foreach (int already_triangle in vidx2triangle[vidx])
					{
						if (already_triangle == triangle)
						{
							already_has = true;
							break;
						}
					}
					if (!already_has)
					{
						vidx2triangle[vidx].Add(triangle);
					}
				}
			}
		}
		return vidx2triangle;
	}
	

	bool FindEdgeAndTriangle(int vert, List<int> path, Mesh mesh, Dictionary<int, List<int>> edge_2_triangle, Dictionary<int, List<int>> vidx2triangle, List<int> checked_edge_vert, ref int edge_other_vert, ref int edge_triangle)
	{
		checked_edge_vert.Add(vert);
		
		foreach (int triangle in vidx2triangle[vert])
		{
			for (int vidx_idx = 0; vidx_idx != 3; ++vidx_idx)
			{
				int edge_vidx1 = mesh.triangles[triangle + vidx_idx];
				int edge_vidx2 = mesh.triangles[triangle + (vidx_idx + 1) % 3];
				int edge_id = VertexIdxToEdgeId(edge_vidx1, edge_vidx2);
				if (edge_vidx1 == vert || edge_vidx2 == vert)
				{
					if (edge_2_triangle[edge_id].Count == 1)
					{
						int potential_edge_other_vert = (edge_vidx1 == vert ? edge_vidx2 : edge_vidx1);

						// 检查是否已经检查过了，如果是，那么直接跳过
						bool has_checked = false;
						foreach(int checked_vert in checked_edge_vert)
						{
							if(checked_vert == potential_edge_other_vert)
							{
								has_checked = true;
								break;
							}
						}
						if (has_checked)
						{
							continue;
						}

						// 检查是否是路径中的点
						bool is_in_path = false;
						foreach (int path_vert in path)
						{
							if (path_vert == potential_edge_other_vert)
							{
								is_in_path = true;
								break;
							}
						}

						if (is_in_path)
						{
							if (FindEdgeAndTriangle(potential_edge_other_vert, path, mesh, edge_2_triangle, vidx2triangle, checked_edge_vert, ref edge_other_vert, ref edge_triangle))
							{
								return true;
							}
						}
						else
						{
							edge_other_vert = potential_edge_other_vert;
							edge_triangle = triangle;
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	// 因为三角形有可能有两个边都在轮廓上，所以不返回特定的edgeid
	[System.Obsolete("Useless now.")]
	Dictionary<int, bool> GetTriangleIsOnEdge(Dictionary<int, List<int>> edge_2_triangle)
	{
		Dictionary<int, bool> res = new Dictionary<int, bool>();
		foreach(KeyValuePair<int, List<int>> e2t in edge_2_triangle)
		{
			if(e2t.Value.Count == 1)
			{
				res[e2t.Value[0]] = true;
			}
		}
		return res;
	}
	
	// 对于一个有vert碰到边缘了三角形，计算将其寻路到有edge贴着边缘时的三角形列表和edge列表
	void FindTriangleToEdgeAroundOneVertex(int around_vertex, int last_triangle, Mesh mesh, Dictionary<int, List<int>> edge_2_triangles, Dictionary<int, List<int>> vert_2_neigh, out List<int> added_edges)
	{
		bool is_already_ok = false;
		int edge_id = -1;
		int next_vert = -1;
		TraverseTriangleEdge(last_triangle, mesh, delegate(int v1, int v2, int v3)
		{
			if(v1 == around_vertex)
			{
				if(edge_2_triangles[VertexIdxToEdgeId(v2, v1)].Count == 1)
				{
					edge_id = VertexIdxToEdgeId(v2, v1);
					next_vert = v2;
					is_already_ok = true;
				}
				else if (edge_2_triangles[VertexIdxToEdgeId(v3, v1)].Count == 1)
				{
					edge_id = VertexIdxToEdgeId(v3, v1);
					next_vert = v3;
					is_already_ok = true;
				}
				else
				{
					edge_id = VertexIdxToEdgeId(v2, v1);
					next_vert = v2;
					is_already_ok = false;
				}

				return false;
			}
			return true;
		});

		added_edges = new List<int>();
		added_edges.Add(edge_id);
		if(is_already_ok)
		{
			return;
		}

		int max_loop = vert_2_neigh[around_vertex].Count;
		while(--max_loop >= 0)
		{
			if(edge_2_triangles[edge_id].Count == 1)
			{
				break;
			}

			next_vert = FindTriangleOtherVertexAndTriangleByEdgeId(last_triangle, edge_id, edge_2_triangles, mesh, out last_triangle);
			edge_id = VertexIdxToEdgeId(around_vertex, next_vert);
			added_edges.Add(edge_id);
		}
	}
}
