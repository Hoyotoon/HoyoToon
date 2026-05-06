using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Scene.HSR
{
	public class Rotatable : MonoBehaviour
	{
		private static readonly List<Rotatable> s_ActiveRotatables = new List<Rotatable>();
		private static bool s_BeforeRenderRegistered;

		[SerializeField] private Camera targetCamera;
		private float lastSyncedYaw = float.NaN;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState()
		{
			UnregisterBeforeRender();
			s_ActiveRotatables.Clear();
		}

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetActiveCamera();
            }
        }

		private void OnEnable()
		{
			lastSyncedYaw = float.NaN;
			Register(this);
		}

		private void OnDisable()
		{
			Unregister(this);
		}

		private void LateUpdate()
		{
			SyncToCamera(ResolveCamera(null, true));
		}

		private Camera ResolveCamera(Camera renderCamera, bool cacheFallback)
		{
			Camera resolvedCamera = targetCamera != null ? targetCamera : renderCamera;
			if (resolvedCamera == null)
				resolvedCamera = GetActiveCamera();

			if (cacheFallback && targetCamera == null && resolvedCamera != null)
				targetCamera = resolvedCamera;

			return resolvedCamera;
		}

		private void SyncToCamera(Camera camera)
		{
			if (camera == null || camera.transform == null)
				return;

			SyncToYaw(camera.transform.rotation.eulerAngles.y);
		}

		private void SyncToYaw(float cameraYaw)
		{
			if (Mathf.Approximately(lastSyncedYaw, cameraYaw))
				return;

			lastSyncedYaw = cameraYaw;
			Vector3 currentEuler = transform.rotation.eulerAngles;
			transform.rotation = Quaternion.Euler(currentEuler.x, cameraYaw, currentEuler.z);
		}

		private static Camera GetActiveCamera()
		{
			if (Camera.main != null)
			{
				return Camera.main;
			}

			return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
		}

		private static void Register(Rotatable rotatable)
		{
			if (rotatable == null)
				return;

			if (!s_ActiveRotatables.Contains(rotatable))
				s_ActiveRotatables.Add(rotatable);

			RegisterBeforeRender();
		}

		private static void Unregister(Rotatable rotatable)
		{
			if (rotatable == null)
				return;

			s_ActiveRotatables.Remove(rotatable);
			if (s_ActiveRotatables.Count == 0)
				UnregisterBeforeRender();
		}

		private static void RegisterBeforeRender()
		{
			if (s_BeforeRenderRegistered)
				return;

			s_BeforeRenderRegistered = true;
			Application.onBeforeRender -= SyncActiveRotatablesBeforeRender;
			Application.onBeforeRender += SyncActiveRotatablesBeforeRender;
		}

		private static void UnregisterBeforeRender()
		{
			if (!s_BeforeRenderRegistered)
				return;

			Application.onBeforeRender -= SyncActiveRotatablesBeforeRender;
			s_BeforeRenderRegistered = false;
		}

		private static void SyncActiveRotatablesBeforeRender()
		{
			Camera fallbackCamera = GetActiveCamera();
			float fallbackCameraYaw = 0f;
			bool hasFallbackCameraYaw = fallbackCamera != null && fallbackCamera.transform != null;
			if (hasFallbackCameraYaw)
				fallbackCameraYaw = fallbackCamera.transform.rotation.eulerAngles.y;

			for (int i = s_ActiveRotatables.Count - 1; i >= 0; --i)
			{
				Rotatable rotatable = s_ActiveRotatables[i];
				if (rotatable == null)
				{
					s_ActiveRotatables.RemoveAt(i);
					continue;
				}

				if (!rotatable.isActiveAndEnabled)
					continue;

				if (rotatable.targetCamera != null)
				{
					rotatable.SyncToCamera(rotatable.targetCamera);
				}
				else if (hasFallbackCameraYaw)
				{
					rotatable.SyncToYaw(fallbackCameraYaw);
				}
			}

			if (s_ActiveRotatables.Count == 0)
				UnregisterBeforeRender();
		}
	}
}
