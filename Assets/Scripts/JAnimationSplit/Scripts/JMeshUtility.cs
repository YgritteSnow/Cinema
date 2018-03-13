using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JMeshUtility
{
	#region 把一条vertex的路径连成一个面
	public static Mesh GenerateSurfaceMeshByVertexList(List<int> vertex_path, Vector3[] origin_vecs)
	{
		List<int> triangles = new List<int>();
		int lo_idx = 0;
		int hi_idx = vertex_path.Count-1;
		while(++lo_idx < --hi_idx)
		{
			int lo_idx_pre = lo_idx - 1;
			int hi_idx_pre = hi_idx + 1;
			triangles.Add(lo_idx_pre);
			triangles.Add(hi_idx_pre);
			triangles.Add(hi_idx);
			triangles.Add(hi_idx);
			triangles.Add(lo_idx);
			triangles.Add(lo_idx_pre);
		}
		if(lo_idx == hi_idx)
		{
			int lo_idx_pre = lo_idx - 1;
			int hi_idx_pre = hi_idx + 1;
			triangles.Add(lo_idx_pre);
			triangles.Add(hi_idx_pre);
			triangles.Add(lo_idx);
		}
		int[] triangles_arr = new int[triangles.Count];
		triangles.CopyTo(triangles_arr);
		Vector3[] vector_arr = new Vector3[vertex_path.Count];
		for(int vidx_idx = 0; vidx_idx != vertex_path.Count; ++vidx_idx)
		{
			vector_arr[vidx_idx] = origin_vecs[vertex_path[vidx_idx]];
		}

		Mesh res = new Mesh();
		res.triangles = triangles_arr;
		res.vertices = vector_arr;
		return res;
	}
	#endregion

	#region 遍历图
	public delegate bool TriverseMapFunc<NodeType>(NodeType node);
	public static void TraverseMap<NodeType>(NodeType bgn_node, Dictionary<NodeType, List<NodeType>> neighbours, TriverseMapFunc<NodeType> traverse_func)
	{
		Dictionary<NodeType, bool> checked_node = new Dictionary<NodeType, bool>();

		List<NodeType> to_check_node = new List<NodeType>();
		to_check_node.Add(bgn_node);
		while(to_check_node.Count != 0)
		{
			NodeType cur_to_check = to_check_node[to_check_node.Count - 1];
			to_check_node.RemoveAt(to_check_node.Count - 1);
			checked_node[cur_to_check] = true;

			traverse_func(cur_to_check);

			foreach(NodeType neigh_node in neighbours[cur_to_check])
			{
				if(!checked_node.ContainsKey(neigh_node))
				{
					to_check_node.Add(neigh_node);
				}
			}

		}
	}
	#endregion

	#region 从一个Mesh中分离一部分三角形新建mesh
	public static Mesh SplitTriangleToMesh(int[] triangles, Mesh origin_mesh)
	{
		// 整理出所有感兴趣的vertex
		int[] old_vertex_interested = new int[origin_mesh.vertices.Length];
		int real_vertex_count = 0;
		for(int i = 0; i != old_vertex_interested.Length; ++i)
		{
			old_vertex_interested[i] = -1;
		}
		foreach(int triangle in triangles)
		{
			for(int vidx_idx = 0; vidx_idx != 3; ++vidx_idx)
			{
				if (old_vertex_interested[origin_mesh.triangles[triangle + vidx_idx]] < 0)
				{
					old_vertex_interested[origin_mesh.triangles[triangle + vidx_idx]] = 0;
					++ real_vertex_count;
				}
			}
		}
		int new_vertex_iter = 0;
		Vector3[] new_vertex_arr = new Vector3[real_vertex_count];
		Vector2[] new_uv_arr = new Vector2[real_vertex_count];
		Vector3[] new_normal_arr = new Vector3[real_vertex_count];
		Vector4[] new_tangent_arr = new Vector4[real_vertex_count];
		bool has_new_boneweight = origin_mesh.boneWeights.Length > 0;
		bool has_new_uv2 = origin_mesh.uv2.Length > 0;
		bool has_new_uv3 = origin_mesh.uv3.Length > 0;
		bool has_new_uv4 = origin_mesh.uv4.Length > 0;
		BoneWeight[] new_boneweight = has_new_boneweight ? new BoneWeight[real_vertex_count] : new BoneWeight[0];
		Vector2[] new_uv2_arr = has_new_uv2 ? new Vector2[real_vertex_count] : new Vector2[0];
		Vector2[] new_uv3_arr = has_new_uv3 ? new Vector2[real_vertex_count] : new Vector2[0];
		Vector2[] new_uv4_arr = has_new_uv4 ? new Vector2[real_vertex_count] : new Vector2[0];
		for (int i = 0; i != old_vertex_interested.Length; ++i)
		{
			if (old_vertex_interested[i] >= 0)
			{
				new_vertex_arr[new_vertex_iter] = origin_mesh.vertices[i];
				new_uv_arr[new_vertex_iter] = origin_mesh.uv[i];
				new_normal_arr[new_vertex_iter] = origin_mesh.normals[i];
				new_tangent_arr[new_vertex_iter] = origin_mesh.tangents[i];
				if (has_new_boneweight) { new_boneweight[new_vertex_iter] = origin_mesh.boneWeights[i]; }
				if (has_new_uv2) { new_uv2_arr[new_vertex_iter] = origin_mesh.uv2[i]; }
				if (has_new_uv3) { new_uv3_arr[new_vertex_iter] = origin_mesh.uv3[i]; }
				if (has_new_uv4) { new_uv4_arr[new_vertex_iter] = origin_mesh.uv4[i]; }
				old_vertex_interested[i] = new_vertex_iter;

				++ new_vertex_iter;
			}
		}

		// 整理所有的triangle
		int[] new_triangle_arr = new int[triangles.Length * 3];
		for(int tri_idx = 0; tri_idx != triangles.Length; ++tri_idx)
		{
			int triangle = triangles[tri_idx];
			new_triangle_arr[tri_idx * 3 + 0] = old_vertex_interested[origin_mesh.triangles[triangle + 0]];
			new_triangle_arr[tri_idx * 3 + 1] = old_vertex_interested[origin_mesh.triangles[triangle + 1]];
			new_triangle_arr[tri_idx * 3 + 2] = old_vertex_interested[origin_mesh.triangles[triangle + 2]];
		}

		Mesh result = new Mesh();
		result.bindposes = origin_mesh.bindposes;
		result.vertices = new_vertex_arr;
		result.boneWeights = new_boneweight;
		result.triangles = new_triangle_arr;
		result.subMeshCount = origin_mesh.subMeshCount;
		result.uv = new_uv_arr;
		result.uv2 = new_uv2_arr;
		result.uv3 = new_uv3_arr;
		result.uv4 = new_uv4_arr;
		result.normals = new_normal_arr;
		result.tangents = new_tangent_arr;
		return result;
	}
	#endregion
}
