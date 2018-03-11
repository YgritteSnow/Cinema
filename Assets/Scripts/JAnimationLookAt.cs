using UnityEngine;

public class JAnimationLookAt : MonoBehaviour
{
	/// <summary>
	/// 旋转速度
	/// </summary>
	[HideInInspector]
	public float targetSpeed = 3;
	/// <summary>
	/// 总体的最大旋转角度
	/// </summary>
	[HideInInspector]
	public Vector2 angleLimit = new Vector2(30, 70);
	/// <summary>
	/// 眼睛所视的方向在target结点坐标系下的指向
	/// </summary>
	public Vector3 targetDir = new Vector3(-0.1f, 0.8f, 0);
	/// <summary>
	/// 要改变的目标结点（transform.name 版）
	/// </summary>
	public string targetNodeName = "Bip01 Head";
	/// <summary>
	/// 跟随旋转的结点和其权重的数据结构（transform.name 版）
	/// </summary>
	[System.Serializable]
	public struct NodeNameAndParam
	{
		public NodeNameAndParam(string n, float p) { node = n; param = p; }
		public string node;
		public float param;
	}
	/// <summary>
	/// 跟随旋转的结点和其权重的列表（transform.name 版）
	/// </summary>
	[HideInInspector]
	public NodeNameAndParam[] node_name_list = null;

	/// <summary>
	/// 要改变的目标结点（transform 版）
	/// </summary>
	Transform targetNode;
	/// <summary>
	/// 跟随旋转的结点和其权重的数据结构（transform版）
	/// </summary>
	struct NodeTransAndParam
	{
		public NodeTransAndParam(Transform n, float p) { node = n; param = p; }
		public Transform node;
		public float param;
	}
	/// <summary>
	/// 跟随旋转的结点和其权重的列表（transform 版）
	/// </summary>
	NodeTransAndParam[] node_trans_list;

	bool inited = false;
	Quaternion dstQuaternion = Quaternion.identity;
	Quaternion curQuaternion = Quaternion.identity;

	public Camera cameraNode;



	/// <summary>
	/// 初始化
	/// </summary>
	/// <param name="camera">摄像机</param>
	/// <param name="headBoneName">头部骨骼名称</param>
	/// <param name="spineBoneName">胸部骨骼名称（用来计算最大旋转角度）</param>
	public void Init(Camera camera)
	{
		inited = false;

		// 初始化相机
		//this.cameraNode = camera;
		if (!this.cameraNode)
		{
			Debug.LogError("Camera is null!");
			return;
		}

		// 如果没有定义的话，初始化目标结点的数据
		if (node_name_list == null)
		{
			node_name_list = new NodeNameAndParam[3];
			node_name_list[0] = new NodeNameAndParam("Bip01 Spine", 0.5f);
			node_name_list[1] = new NodeNameAndParam("Bip01 Spine1", 1);
			node_name_list[2] = new NodeNameAndParam("Bip01 Neck", 2);
		}

		// 初始化目标结点
		targetNode = null;
		if (!FindChildDeep(this.transform, targetNodeName, ref targetNode))
		{
			Debug.LogError("Target not found!");
			return;
		}

		// 初始化要旋转的结点的列表
		node_trans_list = new NodeTransAndParam[node_name_list.Length];
		Transform cur_trans = this.transform;
		float norm_param_total = 0;
		int count = 0;
		for (int idx = 0; idx < node_name_list.Length; ++idx)
		{
			Transform child = null;
			if (FindChildDeep(cur_trans, node_name_list[idx].node, ref child))
			{
				node_trans_list[idx] = new NodeTransAndParam(child, node_name_list[idx].param);
				norm_param_total += node_name_list[idx].param;
				++count;
			}
			else
			{
				node_trans_list[idx] = new NodeTransAndParam(null, 0);
				norm_param_total += 0;
			}
		}
		if (count == 0)
		{
			Debug.LogError("No node to transform!");
			return;
		}
		for (int idx = 0; idx < node_trans_list.Length; ++idx)
		{
			if (norm_param_total > 0.00001)
			{
				node_trans_list[idx].param /= norm_param_total;
			}
			else
			{
				node_trans_list[idx].param = 1.0f / count;
			}
		}

		inited = true;
		return;
	}

	bool FindChildDeep(Transform parent, string searchName, ref Transform result)
	{
		if (parent.name == searchName)
		{
			result = parent;
			return true;
		}
		for (int a = 0; a < parent.childCount; a++)
		{
			if (FindChildDeep(parent.GetChild(a), searchName, ref result))
			{
				return true;
			}
		}
		return false;
	}

	Quaternion CalculateRotate(Transform node, Camera cam)
	{
		if (node && cam)
		{
			Vector3 selfDir = node.TransformVector(targetDir);
			Vector3 cameraDir = cam.transform.position - node.position;
			return Quaternion.FromToRotation(selfDir, cameraDir);
		}
		return Quaternion.identity;
	}

	void LateUpdate()
	{
		if (!inited)
		{
			return;
		}

		dstQuaternion = CalculateRotate(targetNode, cameraNode);
		if ((Mathf.Abs(dstQuaternion.eulerAngles.x) > angleLimit.x && Mathf.Abs(dstQuaternion.eulerAngles.x - 360) > angleLimit.x)
		 || (Mathf.Abs(dstQuaternion.eulerAngles.y) > angleLimit.y && Mathf.Abs(dstQuaternion.eulerAngles.y - 360) > angleLimit.y))
		{
			dstQuaternion = Quaternion.identity;
		}

		curQuaternion = Quaternion.Slerp(curQuaternion, dstQuaternion, Time.deltaTime * targetSpeed);

		float ang;
		Vector3 axis;
		curQuaternion.ToAngleAxis(out ang, out axis);

		foreach (NodeTransAndParam v in node_trans_list)
		{
			v.node.rotation = Quaternion.AngleAxis(ang * v.param, axis) * v.node.rotation;
		}

#if UNITY_EDITOR
		if (debug)
		{
			Debug.DrawRay(targetNode.position, targetNode.transform.up, Color.red);
			Debug.DrawRay(targetNode.position, targetNode.transform.right, Color.green);
			Debug.DrawRay(targetNode.position, targetNode.transform.forward, Color.blue);
		}
#endif
	}

	private void Start()//For test in editor
	{
		Init(Camera.main);
	}
#if UNITY_EDITOR
	public bool debug = true;
	private void OnDrawGizmos()
	{
		if (cameraNode && debug)
		{
			Gizmos.matrix = cameraNode.transform.localToWorldMatrix;
			Gizmos.DrawFrustum(Vector3.zero, cameraNode.fieldOfView, 1, 0, cameraNode.aspect);
		}
	}
#endif

}
