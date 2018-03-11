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
			//DoSplitOne(renderers[i], m_focus_bones[0]);
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

	Dictionary<int, int> GetBoneIdx2ParentIdx(SkinnedMeshRenderer renderer, string focus_name, out int focus_idx, out int focus_parentIdx)
	{
		Dictionary<string, int> bone_name_2_idx = new Dictionary<string, int>();
		Dictionary<int, string> bone_idx_2_parentName = new Dictionary<int, string>();
		for (int i = 0; i != renderer.bones.Length; ++i)
		{
			bone_name_2_idx[renderer.bones[i].name] = i;
			bone_idx_2_parentName[i] = renderer.bones[i].parent.name;
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

	Dictionary<int, bool> GetBoneIsChildMap(SkinnedMeshRenderer renderer, string focus_name)
	{
		Dictionary<int, bool> res = new Dictionary<int, bool>();
		for (int i = 0; i != renderer.bones.Length; ++i)
		{
			Transform cur_trans = renderer.bones[i];
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
	Dictionary<int, List<int>> GetTriangleEdge2Triangle(List<int> triangles, Mesh mesh)
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
	Dictionary<int, List<int>> GetTriangle2TriangleList(List<int> triangles, Mesh mesh, Dictionary<int, List<int>> edge_2_triangle)
	{
		Dictionary<int, List<int>> triangle_2_neighbours = new Dictionary<int, List<int>>();
		foreach (int triangle_idx in triangles)
		{
			int vi1 = mesh.triangles[triangle_idx];
			int vi2 = mesh.triangles[triangle_idx];
			int vi3 = mesh.triangles[triangle_idx];
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

	#region 将三角形列表根据其“跨越父子骨骼的程度”排序
	[System.Obsolete("Should not be useful. Sort func needs to be optimized by the way", true)]
	void SortTriangleListByParam(SkinnedMeshRenderer renderer, string focus_bone, List<int> cross_triangles)
	{
		Dictionary<int, bool> bone_is_child = GetBoneIsChildMap(renderer, focus_bone);
		cross_triangles.Sort(delegate (int lt, int rt)
			{
				float lparam = CalTriangleCrossParam(lt, renderer.sharedMesh, bone_is_child);
				float rparam = CalTriangleCrossParam(rt, renderer.sharedMesh, bone_is_child);
				return lparam > rparam ? -1 : (lparam < rparam ? 1 : 0);
			});
	}
	#endregion

	void DoSplitOne(SkinnedMeshRenderer renderer, string focus_bone)
	{
		Dictionary<int, bool> bone_idx_is_child = GetBoneIsChildMap(renderer, focus_bone);

		// 遍历所有三角形，与该骨骼和其父骨骼都有关的三角形
		// 如果这些点不足以获得一个闭合曲线，其实应当再去孩子和祖父中寻找。TODO
		List<int> cross_triangles = new List<int>();
		List<int> child_triangles = new List<int>();
		List<int> parent_triangles = new List<int>();
		Mesh mesh = renderer.sharedMesh;
		for (int i = 0; i != mesh.triangles.Length; i = i + 3)
		{
			bool has_childIdx = false;
			bool has_parentIdx = false;
			for (int j = 0; j < 3; ++j)
			{
				int vi = mesh.triangles[i + j];
				BoneWeight vw = mesh.boneWeights[vi];
				if (bone_idx_is_child.ContainsKey(vw.boneIndex0) || bone_idx_is_child.ContainsKey(vw.boneIndex1))
				{
					has_childIdx = true;
				}

				if (!bone_idx_is_child.ContainsKey(vw.boneIndex0) || !bone_idx_is_child.ContainsKey(vw.boneIndex1))
				{
					has_parentIdx = true;
				}
			}
			if(has_childIdx && has_parentIdx)
			{
				cross_triangles.Add(i);
			}
			else if(has_childIdx)
			{
				child_triangles.Add(i);
			}
			else if(has_parentIdx)
			{
				parent_triangles.Add(i);
			}
		}

		if(cross_triangles.Count != 0)
		{
			List<int> child_side_vertex, parent_side_vertex;
			FindSplitVertexOnTwoSides(mesh, cross_triangles, child_triangles, parent_triangles, bone_idx_is_child, out child_side_vertex, out parent_side_vertex);

			List<int> cross_child_triangles;
			List<int> cross_parent_triangles;
			SplitChildAndParent(mesh, cross_triangles, child_side_vertex, parent_side_vertex, bone_idx_is_child, out cross_child_triangles, out cross_parent_triangles);
		}

		// 将child和parent分别存储为mesh
		// 
	}
	
	// 找到位于两个轮廓上的顶点
	void FindSplitVertexOnTwoSides(Mesh mesh, List<int> cross_triangles, List<int> child_triangles, List<int> parent_triangles, Dictionary<int, bool> bone_idx_is_child, out List<int> child_side_vertex, out List<int> parent_side_vertex)
	{
		child_side_vertex = new List<int>();
		parent_side_vertex = new List<int>();
		foreach (int triangle_idx in cross_triangles)
		{
			for(int vi = 0; vi < 3; ++vi)
			{
				int vindex = mesh.triangles[triangle_idx + vi];
				BoneWeight vweight = mesh.boneWeights[vindex];
				if(vweight.boneIndex1 == 0) // 只查找那些仅含有1个骨骼作为其绑定的顶点
				{
					bool has_index = false;
					if (bone_idx_is_child.ContainsKey(vweight.boneIndex0))
					{
						foreach (int child_triangle_idx in child_triangles)
						{
							for (int childvi = 0; childvi < 3; ++childvi)
							{
								if (mesh.triangles[child_triangle_idx + childvi] == vindex)
								{
									has_index = true;
									break;
								}
							}
						}
						if(has_index)
						{
							child_side_vertex.Add(vindex);
						}
					}
					else
					{
						foreach (int child_triangle_idx in parent_triangles)
						{
							for (int childvi = 0; childvi < 3; ++childvi)
							{
								if (mesh.triangles[child_triangle_idx + childvi] == vindex)
								{
									has_index = true;
									break;
								}
							}
						}
						if (has_index)
						{
							parent_side_vertex.Add(vindex);
						}
					}
				}
			}
		}
	}

	// 找到三角形带的分割线
	void SplitChildAndParent(Mesh mesh, List<int> cross_triangle, List<int> child_side_vertex, List<int> parent_side_vertex, Dictionary<int, bool> bone_idx_is_child, out List<int> cross_child_triangles, out List<int> cross_parent_triangles)
	{
		cross_child_triangles = new List<int>();
		cross_parent_triangles = new List<int>();

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
		// 在孩子这边的所有顶点中寻找最近的那个点
		int child_vert = child_side_vertex[0];
		float min_child_dist = CalVertDist(mesh, path_vert, child_vert);
		for(int vidx_iter = 1; vidx_iter != child_side_vertex.Count; ++vidx_iter)
		{
			int vidx = child_side_vertex[vidx_iter];
			float new_dist = CalVertDist(mesh, path_vert, vidx);
			if(new_dist < min_child_dist)
			{
				child_vert = vidx;
				min_child_dist = new_dist;
			}
		}
		// 在父亲这边的所有顶点中寻找最近的那个点
		int parent_vert = parent_side_vertex[0];
		float min_parent_dist = CalVertDist(mesh, path_vert, parent_vert);
		for (int vidx_iter = 1; vidx_iter != parent_side_vertex.Count; ++vidx_iter)
		{
			int vidx = parent_side_vertex[vidx_iter];
			float new_dist = CalVertDist(mesh, path_vert, vidx);
			if (new_dist < min_parent_dist)
			{
				parent_vert = vidx;
				min_parent_dist = new_dist;
			}
		}

		/* 用三角形边来切开三角形带
		// 缓存顶点到所有邻居的映射
		Dictionary<int, List<int>> vert2neigh = CalVertToNeighbour(mesh, cross_triangle);
		// 按照方向优先寻路，以求尽可能经过更少量的点
		List<int> child_parent_path = new List<int>();
		bool res = CalVertPathByMinDist(mesh, child_vert, parent_vert, vert2neigh, ref child_parent_path);
		if(!res)
		{
			Debug.LogError("Cannot find path!");
			return;
		}
		*/

		// 用三角形带来切开三角形带
		// 建立边=>三角形的索引
		Dictionary<int, List<int>> edge_2_triangle = GetTriangleEdge2Triangle(cross_triangle, mesh);
		// 是否是边缘处的三角形
		Dictionary<int, bool> triangle_is_onedge = GetTriangleIsOnEdge(edge_2_triangle);
		// 建立三角形=>三角形的索引
		Dictionary<int, List<int>> triangle_2_neigh = GetTriangle2TriangleList(cross_triangle, mesh, edge_2_triangle);

		// 以三角形寻路，从此边缘的某三角形开始，寻路到第一次经过的彼边缘
		int bgn_triangle = FindOneTriangleByVertex(child_vert);
		List<int> triangle_path; // 路径经过的三角形
		List<int> edge_id_path_inside; // 路径经过的内部edge
		List<int> vertex_id_inside; // 经过的所有顶点
		FindTrianglePathToOtherSide(bgn_triangle, child_side_vertex, parent_side_vertex, out triangle_path, out edge_id_path_inside, out vertex_id_inside);

		int middle_edge_id = edge_id_path_inside[edge_id_path_inside.Count / 2];
		int middle_edge_v1, middle_edge_v2;
		EdgeIdToVertexIdx(middle_edge_id, out middle_edge_v1, out middle_edge_v2);
		int split_bgn_triangle = FindTriangleWithSplit(middle_edge_v1, triangle_path);
		int split_end_triangle = FindTriangleWithSplit(middle_edge_v2, triangle_path);

		// 剔除用来剪断三角形带的邻边们
		List<int> cal_cut_triangle_2_edges = ScissorPathTriangle(vertex_id_inside);

		// 从断口两端开始寻路
		List<int> cut_edge_id_path_inside, cut_vertex_id_inside;
		FindTrianglePath(cal_cut_triangle_2_edges, split_bgn_triangle, split_end_triangle, out triangle_path, out cut_edge_id_path_inside, out cut_vertex_id_inside);

		// 生成面片！
		Mesh surface_mesh = GeneratePathSurface(cut_vertex_id_inside);

		// 剔除用来剪三角形带为两个的三角形的邻边们
		List<int> cal_genmesh_triangle_2_edges = ScissorPathTriangle(cut_vertex_id_inside);
		List<int> all_child_side_triangles = GetAllNeighTriangles(child_vert);
		List<int> all_parent_side_triangles = GetAllNeighTriangles(parent_vert);

		// 生成孩子这边的mesh
		// 生成父亲这边的mesh
		// 合并各自的mesh
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
	Dictionary<int, List<int>> CalPathTriangles(List<int> path, Mesh mesh, List<int> cross_triangles)
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
}
