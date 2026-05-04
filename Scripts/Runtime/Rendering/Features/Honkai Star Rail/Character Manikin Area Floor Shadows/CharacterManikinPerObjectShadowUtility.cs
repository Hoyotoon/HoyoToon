using UnityEngine;

namespace HoyoToon.Runtime.Rendering.HSR
{
    internal static class CharacterManikinPerObjectShadowUtility
    {
        static readonly Matrix4x4 k_FlipZMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
        static readonly Vector3[] k_BoundsCorners = new Vector3[8];
        static readonly Vector3[] k_FrustumCorners = new Vector3[8];
        static readonly Vector3[] k_FrustumCornerBuffer = new Vector3[4];
        static readonly int[] k_FrustumTriangleIndices =
        {
            0, 3, 1,
            1, 3, 2,
            2, 3, 7,
            2, 7, 6,
            0, 5, 4,
            0, 1, 5,
            1, 2, 5,
            2, 6, 5,
            0, 7, 3,
            0, 4, 7,
            4, 7, 5,
            5, 7, 6,
        };

        static readonly Vector3[] k_ClipInput = new Vector3[16];
        static readonly Vector3[] k_ClipOutput = new Vector3[16];

        static void BuildBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            k_BoundsCorners[0] = new Vector3(min.x, min.y, min.z);
            k_BoundsCorners[1] = new Vector3(max.x, min.y, min.z);
            k_BoundsCorners[2] = new Vector3(min.x, max.y, min.z);
            k_BoundsCorners[3] = new Vector3(min.x, min.y, max.z);
            k_BoundsCorners[4] = new Vector3(max.x, max.y, min.z);
            k_BoundsCorners[5] = new Vector3(max.x, min.y, max.z);
            k_BoundsCorners[6] = new Vector3(min.x, max.y, max.z);
            k_BoundsCorners[7] = new Vector3(max.x, max.y, max.z);
        }

        static void ComputeViewSpaceExtents(Matrix4x4 viewMatrix, out Vector3 shadowMin, out Vector3 shadowMax)
        {
            shadowMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            shadowMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < k_BoundsCorners.Length; ++i)
            {
                Vector3 v = viewMatrix.MultiplyPoint(k_BoundsCorners[i]);
                shadowMin = Vector3.Min(shadowMin, v);
                shadowMax = Vector3.Max(shadowMax, v);
            }
        }

        static void SetFrustumEightCorners(Camera camera)
        {
            Transform transform = camera.transform;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;

            if (camera.orthographic)
            {
                float top = camera.orthographicSize;
                float right = top * camera.aspect;

                k_FrustumCorners[0] = transform.TransformPoint(new Vector3(-right, -top, near));
                k_FrustumCorners[1] = transform.TransformPoint(new Vector3(-right, top, near));
                k_FrustumCorners[2] = transform.TransformPoint(new Vector3(right, top, near));
                k_FrustumCorners[3] = transform.TransformPoint(new Vector3(right, -top, near));
                k_FrustumCorners[4] = transform.TransformPoint(new Vector3(-right, -top, far));
                k_FrustumCorners[5] = transform.TransformPoint(new Vector3(-right, top, far));
                k_FrustumCorners[6] = transform.TransformPoint(new Vector3(right, top, far));
                k_FrustumCorners[7] = transform.TransformPoint(new Vector3(right, -top, far));
            }
            else
            {
                Rect viewport = new Rect(0f, 0f, 1f, 1f);

                camera.CalculateFrustumCorners(viewport, near, Camera.MonoOrStereoscopicEye.Mono, k_FrustumCornerBuffer);
                for (int i = 0; i < 4; ++i)
                    k_FrustumCorners[i] = transform.TransformPoint(k_FrustumCornerBuffer[i]);

                camera.CalculateFrustumCorners(viewport, far, Camera.MonoOrStereoscopicEye.Mono, k_FrustumCornerBuffer);
                for (int i = 0; i < 4; ++i)
                    k_FrustumCorners[i + 4] = transform.TransformPoint(k_FrustumCornerBuffer[i]);
            }
        }

        static bool IsInsideEdge(Vector3 p, int componentIndex, float edgeValue, bool isMinEdge)
        {
            float delta = p[componentIndex] - edgeValue;
            return isMinEdge ? delta > 0f : delta < 0f;
        }

        static Vector3 IntersectEdge(Vector3 a, Vector3 b, int componentIndex, float edgeValue)
        {
            Vector3 delta = b - a;
            float denom = delta[componentIndex];
            if (Mathf.Abs(denom) <= 1e-6f)
                return a;

            float t = (edgeValue - a[componentIndex]) / denom;
            return a + delta * t;
        }

        static int ClipPolygonAgainstEdge(
            Vector3[] input,
            int inputCount,
            Vector3[] output,
            int componentIndex,
            float edgeValue,
            bool isMinEdge)
        {
            if (inputCount <= 0)
                return 0;

            int outputCount = 0;
            Vector3 prev = input[inputCount - 1];
            bool prevInside = IsInsideEdge(prev, componentIndex, edgeValue, isMinEdge);

            for (int i = 0; i < inputCount; ++i)
            {
                Vector3 curr = input[i];
                bool currInside = IsInsideEdge(curr, componentIndex, edgeValue, isMinEdge);

                if (currInside)
                {
                    if (!prevInside)
                        output[outputCount++] = IntersectEdge(prev, curr, componentIndex, edgeValue);

                    output[outputCount++] = curr;
                }
                else if (prevInside)
                {
                    output[outputCount++] = IntersectEdge(prev, curr, componentIndex, edgeValue);
                }

                prev = curr;
                prevInside = currInside;
            }

            return outputCount;
        }

        static bool AdjustViewSpaceShadowAabb(Camera camera, Matrix4x4 viewMatrix, ref Vector3 shadowMin, ref Vector3 shadowMax)
        {
            SetFrustumEightCorners(camera);

            Vector3 clipMin = new Vector3(shadowMin.x, shadowMin.y, shadowMin.z);
            Vector3 clipMax = new Vector3(shadowMax.x, shadowMax.y, shadowMax.z);

            bool hasVisibleXY = false;
            float minVisibleZ = float.PositiveInfinity;
            float maxVisibleZ = float.NegativeInfinity;

            for (int tri = 0; tri < k_FrustumTriangleIndices.Length; tri += 3)
            {
                int count = 3;
                k_ClipInput[0] = viewMatrix.MultiplyPoint(k_FrustumCorners[k_FrustumTriangleIndices[tri + 0]]);
                k_ClipInput[1] = viewMatrix.MultiplyPoint(k_FrustumCorners[k_FrustumTriangleIndices[tri + 1]]);
                k_ClipInput[2] = viewMatrix.MultiplyPoint(k_FrustumCorners[k_FrustumTriangleIndices[tri + 2]]);

                count = ClipPolygonAgainstEdge(k_ClipInput, count, k_ClipOutput, 0, clipMin.x, isMinEdge: true);
                if (count <= 0)
                    continue;

                count = ClipPolygonAgainstEdge(k_ClipOutput, count, k_ClipInput, 0, clipMax.x, isMinEdge: false);
                if (count <= 0)
                    continue;

                count = ClipPolygonAgainstEdge(k_ClipInput, count, k_ClipOutput, 1, clipMin.y, isMinEdge: true);
                if (count <= 0)
                    continue;

                count = ClipPolygonAgainstEdge(k_ClipOutput, count, k_ClipInput, 1, clipMax.y, isMinEdge: false);
                if (count <= 0)
                    continue;

                hasVisibleXY = true;
                for (int i = 0; i < count; ++i)
                {
                    float z = k_ClipInput[i].z;
                    if (z < minVisibleZ)
                        minVisibleZ = z;
                    if (z > maxVisibleZ)
                        maxVisibleZ = z;
                }
            }

            if (hasVisibleXY && minVisibleZ < shadowMax.z && maxVisibleZ > shadowMin.z)
            {
                shadowMin.z = Mathf.Max(shadowMin.z, minVisibleZ);
                return true;
            }

            return false;
        }

        public static bool TryBuildSelfShadowMatricesFromDirection(
            Camera camera,
            Transform casterTransform,
            Bounds bounds,
            Vector3 projectionAnchor,
            Vector3 baseDirection,
            float lightFollowAmount,
            float nearPlane,
            float farPlane,
            float minOrthographicSize,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix,
            out float priority,
            out Vector4 lightDirection)
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            priority = 0f;
            lightDirection = Vector4.zero;

            if (camera == null)
                return false;

            BuildBoundsCorners(bounds);

            Vector3 lightForward = baseDirection;
            if (lightForward.sqrMagnitude <= 1e-6f)
                lightForward = Vector3.down;
            else
                lightForward.Normalize();

            Vector3 forward = lightForward;

            Vector3 casterUp = casterTransform != null ? casterTransform.up : Vector3.up;
            float upDot = Vector3.Dot(forward, casterUp);
            float clampedUpDot = Mathf.Clamp(upDot, -0.866f, 0f);
            forward += (clampedUpDot - upDot) * casterUp;

            if (forward.sqrMagnitude <= 1e-6f)
                return false;

            forward.Normalize();
            lightDirection = new Vector4(-forward.x, -forward.y, -forward.z, 0f);

            float userNear = Mathf.Max(0.0001f, nearPlane);
            float userFar = Mathf.Max(userNear + 0.01f, farPlane);

            Vector3 stableUp = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, stableUp)) > 0.98f)
                stableUp = Vector3.forward;

            Quaternion lightRotation = Quaternion.LookRotation(forward, stableUp);
            Vector3 lightPosition = projectionAnchor;

            Matrix4x4 lightLocalToWorld = Matrix4x4.TRS(lightPosition, lightRotation, Vector3.one);
            viewMatrix = k_FlipZMatrix * lightLocalToWorld.inverse;

            ComputeViewSpaceExtents(viewMatrix, out Vector3 shadowMin, out Vector3 shadowMax);

            float requiredPushBack = shadowMax.z + userNear + 0.001f;
            if (requiredPushBack > 0f)
            {
                lightPosition -= forward * requiredPushBack;
                lightLocalToWorld = Matrix4x4.TRS(lightPosition, lightRotation, Vector3.one);
                viewMatrix = k_FlipZMatrix * lightLocalToWorld.inverse;

                ComputeViewSpaceExtents(viewMatrix, out shadowMin, out shadowMax);
            }

            float halfWidth = Mathf.Max(Mathf.Max(Mathf.Abs(shadowMin.x), Mathf.Abs(shadowMax.x)), minOrthographicSize);
            float halfHeight = Mathf.Max(Mathf.Max(Mathf.Abs(shadowMin.y), Mathf.Abs(shadowMax.y)), minOrthographicSize);

            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            float xyPadding = Mathf.Max(0.04f, radius * 0.12f);
            halfWidth += xyPadding;
            halfHeight += xyPadding;

            float depthPadding = Mathf.Max(0.08f, radius * 0.12f);
            float zNear = userNear;
            float zFar = Mathf.Max(userFar, -shadowMin.z + depthPadding);

            projectionMatrix = Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, zNear, zFar);

            priority = 0f;
            return true;
        }
    }
}
