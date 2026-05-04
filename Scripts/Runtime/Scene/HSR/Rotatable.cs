using UnityEngine;

namespace HoyoToon.Runtime.Scene.HSR
{
	public class Rotatable : MonoBehaviour
	{
		[SerializeField] private Camera targetCamera;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetActiveCamera();
            }
        }

		private void LateUpdate()
		{
			Camera activeCamera = targetCamera;
			Vector3 currentEuler = transform.rotation.eulerAngles;
			float cameraYaw = activeCamera.transform.rotation.eulerAngles.y;
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
	}
}

