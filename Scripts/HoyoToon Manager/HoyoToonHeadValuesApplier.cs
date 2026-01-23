using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon.EditorTools.ManagerScene
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public sealed class HoyoToonHeadValuesApplier : MonoBehaviour
    {
        public GameObject HeadBone;
        public GameObject FaceMesh;

        public bool IsYUp;

        public Vector3 FaceWorldDir;
        public Vector3 HeadWorldDir;
        public Vector3 OffsetToCenter;

        public bool RunOnEnable = true;
        public bool RemoveAfterRun = true;

        private static readonly int IdCharacterHeadCenterWorldPosition = Shader.PropertyToID("_CharacterHeadCenterWorldPosition");
        private static readonly int IdCharacterFaceWorldDirection = Shader.PropertyToID("_CharacterFaceWorldDirection");
        private static readonly int IdCharacterHeadCenterXDirWS = Shader.PropertyToID("_CharacterHeadCenterXDirWS");
        private static readonly int IsYUpHash = Shader.PropertyToID("_IsYup");

        private bool _hasRun;

    #if UNITY_EDITOR
        private bool _editorRunScheduled;
    #endif

        private void OnEnable()
        {
            if (RunOnEnable)
            {
                RunOnce();
            }
        }

        public void RunOnce()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (_editorRunScheduled)
                {
                    return;
                }

                _editorRunScheduled = true;
                EditorApplication.delayCall += () =>
                {
                    _editorRunScheduled = false;
                    if (this != null)
                    {
                        RunOnceInternal();
                    }
                };
                return;
            }
#endif

            RunOnceInternal();
        }

        private void RunOnceInternal()
        {
            if (_hasRun)
            {
                return;
            }

            _hasRun = true;

            if (HeadBone == null)
            {
                FindHeadBoneAutomatically();
            }

            if (HeadBone == null)
            {
                CleanupAfterRun();
                return;
            }

            CalculateCenterFromWeights();
            GetDirections();
            SendToMaterials();

            CleanupAfterRun();
        }

        public void SendToMaterials()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            Vector3 computedCenter = HeadBone.transform.position + HeadBone.transform.TransformDirection(OffsetToCenter);

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                if (materials == null)
                {
                    continue;
                }

                foreach (var mat in materials)
                {
                    if (mat == null)
                    {
                        continue;
                    }

                    mat.SetVector(IdCharacterHeadCenterWorldPosition, computedCenter);
                    mat.SetVector(IdCharacterFaceWorldDirection, FaceWorldDir);
                    mat.SetVector(IdCharacterHeadCenterXDirWS, HeadWorldDir);
                    mat.SetFloat(IsYUpHash, IsYUp ? 1f : 0f);

#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(mat);
                    }
#endif
                }
            }
        }

        public void GetDirections()
        {
            Transform rootTransform = transform;
            Vector3 characterForward = rootTransform.forward;

            Vector3 headX = HeadBone.transform.right;
            Vector3 headY = HeadBone.transform.up;
            Vector3 headZ = HeadBone.transform.forward;

            float dotX = Vector3.Dot(headX, characterForward);
            float dotY = Vector3.Dot(headY, characterForward);
            float dotZ = Vector3.Dot(headZ, characterForward);

            Vector3 trueForward;
            if (Mathf.Abs(dotX) > Mathf.Abs(dotY) && Mathf.Abs(dotX) > Mathf.Abs(dotZ))
            {
                trueForward = headX * Mathf.Sign(dotX);
            }
            else if (Mathf.Abs(dotY) > Mathf.Abs(dotZ))
            {
                trueForward = headY * Mathf.Sign(dotY);
            }
            else
            {
                trueForward = headZ * Mathf.Sign(dotZ);
            }

            Vector3 trueRight = Vector3.Cross(Vector3.up, trueForward).normalized;
            FaceWorldDir = trueForward;
            HeadWorldDir = trueRight;

            Vector3 worldUp = Vector3.up;
            Vector3 localRight = HeadBone.transform.right;
            Vector3 localUp = HeadBone.transform.up;
            Vector3 localForward = HeadBone.transform.forward;

            float xAlign = Mathf.Abs(Vector3.Dot(localRight, worldUp));
            float yAlign = Mathf.Abs(Vector3.Dot(localUp, worldUp));
            float zAlign = Mathf.Abs(Vector3.Dot(localForward, worldUp));

            bool isYUp = (yAlign >= xAlign && yAlign >= zAlign);
            IsYUp = isYUp;
        }

        public void FindHeadBoneAutomatically()
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr == null)
                {
                    continue;
                }

                string name = smr.name.ToLowerInvariant();
                string goName = smr.gameObject.name.ToLowerInvariant();
                string meshName = smr.sharedMesh != null ? smr.sharedMesh.name.ToLowerInvariant() : string.Empty;
                bool match = name.Contains("head") || name.Contains("face")
                             || goName.Contains("head") || goName.Contains("face")
                             || meshName.Contains("head") || meshName.Contains("face");
                if (match)
                {
                    if (smr.rootBone != null)
                    {
                        HeadBone = smr.rootBone.gameObject;
                        FaceMesh = smr.gameObject;
                        return;
                    }

                    if (smr.bones != null)
                    {
                        for (int i = 0; i < smr.bones.Length; i++)
                        {
                            if (smr.bones[i] != null)
                            {
                                HeadBone = smr.bones[i].gameObject;
                                FaceMesh = smr.gameObject;
                                return;
                            }
                        }
                    }
                }
            }

            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t == null)
                {
                    continue;
                }

                string tName = t.name.ToLowerInvariant();
                if (tName.Contains("head") || tName.Contains("face"))
                {
                    HeadBone = t.gameObject;
                    if (FaceMesh == null)
                    {
                        FaceMesh = t.gameObject;
                    }
                    return;
                }
            }
        }

        public void CalculateCenterFromWeights()
        {
            if (HeadBone == null)
            {
                return;
            }

            Vector3 totalLocalPos = Vector3.zero;
            int vertexCount = 0;

            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var smr in smrs)
            {
                if (smr == null)
                {
                    continue;
                }

                Mesh mesh = smr.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                int headBoneIndex = -1;
                Transform[] bones = smr.bones;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == HeadBone.transform)
                    {
                        headBoneIndex = i;
                        break;
                    }
                }

                if (headBoneIndex == -1)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;

                for (int i = 0; i < vertices.Length; i++)
                {
                    BoneWeight bw = weights[i];
                    if ((bw.boneIndex0 == headBoneIndex && bw.weight0 > 0) ||
                        (bw.boneIndex1 == headBoneIndex && bw.weight1 > 0) ||
                        (bw.boneIndex2 == headBoneIndex && bw.weight2 > 0) ||
                        (bw.boneIndex3 == headBoneIndex && bw.weight3 > 0))
                    {
                        Vector3 worldPt = smr.transform.TransformPoint(vertices[i]);
                        totalLocalPos += HeadBone.transform.InverseTransformPoint(worldPt);
                        vertexCount++;
                    }
                }
            }

            if (vertexCount > 0)
            {
                OffsetToCenter = totalLocalPos / vertexCount;
            }
        }

        private void CleanupAfterRun()
        {
            if (!RemoveAfterRun)
            {
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        Undo.DestroyObjectImmediate(this);
                    }
                };
                return;
            }

            Destroy(this);
#else
            Destroy(this);
#endif   
        }
    }
}
