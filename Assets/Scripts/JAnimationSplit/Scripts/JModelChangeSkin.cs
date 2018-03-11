using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JModelChangeSkin : MonoBehaviour {
	public GameObject obj;
	
	public void CopySkinnedMesh() {
		if(obj == null)
		{
			return;
		}
		
		var old_body = transform.Find("body").GetComponent<SkinnedMeshRenderer>();
		var new_body = obj.GetComponentInChildren<SkinnedMeshRenderer>();

		old_body.rootBone = transform.Find("Bip01");
		old_body.bones = CopyBones(new_body.bones, transform);
		old_body.sharedMesh = new_body.sharedMesh;
		old_body.sharedMaterials = new_body.sharedMaterials;
	}

	Transform[] CopyBones(Transform[] src_bones, Transform dst_bone_root)
	{
		Transform[] res = new Transform[src_bones.Length];
		for(int i = 0; i != src_bones.Length; ++i)
		{
			string name = src_bones[i].name;
			res[i] = JAnimationUtility.TraverseFind(dst_bone_root, name);
		}
		return res;
	}
}
