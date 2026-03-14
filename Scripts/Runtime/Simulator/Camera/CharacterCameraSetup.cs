using System.Collections.Generic;
using UnityEngine;
using HoyoToon.Simulator.Utilities;

namespace HoyoToon.Simulator.Camera
{
    public class CharacterCameraSetup : MonoBehaviour
    {
        [Header("Bone References")]
        [SerializeField] private Transform characterCenter;
        [SerializeField] private Transform characterHead;

        [Header("Auto-Find Settings")]
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private bool useRootForCenter = false;
        [SerializeField] private string headBoneName = "Head";
        [SerializeField] private List<string> headBoneAlternatives = new List<string>
            { "head", "Head_Bone", "Bip01_Head", "Head_M" };
        [SerializeField] private string centerBoneName = "Hips";
        [SerializeField] private List<string> centerBoneAlternatives = new List<string>
            { "hips", "Hips_Bone", "Bip01_Pelvis", "Root", "Root_M" };

        public Transform CharacterCenter => characterCenter;
        public Transform CharacterHead => characterHead;
        public bool HasValidReferences => characterCenter != null && characterHead != null;

        private void Awake()
        {
            if (autoFindReferences)
            {
                FindBoneReferences();
            }
        }

        public void FindBoneReferences()
        {
            if (characterCenter == null)
            {
                if (useRootForCenter)
                {
                    characterCenter = transform;
                }
                else
                {
                    characterCenter = BoneUtility.FindBone(transform, centerBoneName, centerBoneAlternatives);
                    if (characterCenter == null)
                    {
                        characterCenter = transform;
                    }
                }
            }

            if (characterHead == null)
            {
                characterHead = BoneUtility.FindBone(transform, headBoneName, headBoneAlternatives);
                if (characterHead == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(CharacterCameraSetup)}] Could not find head bone on '{gameObject.name}', using root.",
                        this);
                    characterHead = transform;
                }
            }
        }

        public void ConnectToCamera()
        {
            var controller = FindAnyObjectByType<DynamicCameraTargetController>();
            if (controller == null || characterCenter == null || characterHead == null)
            {
                return;
            }

            controller.SetCharacterReferences(characterCenter, characterHead);
            controller.StartTransition();
        }
        public void SetBoneReferences(Transform center, Transform head)
        {
            characterCenter = center;
            characterHead = head;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (characterCenter != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(characterCenter.position, 0.2f);
            }

            if (characterHead != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(characterHead.position, 0.15f);
            }
        }
#endif
    }
}
