using System;
using System.Collections.Generic;
using System.Globalization;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using MRStainCleaning.MRUKFloor;
using UnityEngine;

namespace MRStainCleaning.Detection
{
    [DisallowMultipleComponent]
    public sealed class ObjectDetectionWorldAdapter : MonoBehaviour
    {
        private enum DetectionBoxCoordinateMode
        {
            CenterSize,
            MinMax
        }

        [SerializeField]
        private ObjectDetectionAgent objectDetectionAgent;

        [SerializeField]
        private ObjectDetectionVisualizer objectDetectionVisualizer;

        [SerializeField]
        private MRUKFloorProvider floorProvider;

#if MRUK_INSTALLED
        [SerializeField]
        private PassthroughCameraAccess passthroughCamera;

        [SerializeField]
        private bool projectDetectionsByFloorRay = true;
#endif

        [SerializeField]
        private DetectionBoxCoordinateMode boxCoordinateMode = DetectionBoxCoordinateMode.CenterSize;

        [SerializeField]
        private bool logProjectionDetails;

#if MRUK_INSTALLED
        [SerializeField]
        private bool projectDetectionsToFloorPlane = true;
#endif

        public event Action<DetectionResult> DetectionReceived;

        private void Awake()
        {
            if (objectDetectionAgent == null)
            {
                objectDetectionAgent = GetComponent<ObjectDetectionAgent>();
            }

            if (objectDetectionVisualizer == null)
            {
                objectDetectionVisualizer = GetComponent<ObjectDetectionVisualizer>();
            }

            if (floorProvider == null)
            {
                floorProvider = FindFirstObjectByType<MRUKFloorProvider>();
            }

#if MRUK_INSTALLED
            if (passthroughCamera == null)
            {
                passthroughCamera = FindAnyObjectByType<PassthroughCameraAccess>();
            }
#endif
        }

        private void OnEnable()
        {
            if (objectDetectionAgent == null)
            {
                Debug.LogWarning("[Detection] ObjectDetectionWorldAdapter needs an ObjectDetectionAgent reference.");
                return;
            }

            objectDetectionAgent.OnDetectionResponseReceived.AddListener(HandleDetectionResponseReceived);
        }

        private void OnDisable()
        {
            if (objectDetectionAgent != null)
            {
                objectDetectionAgent.OnDetectionResponseReceived.RemoveListener(HandleDetectionResponseReceived);
            }
        }

        private void HandleDetectionResponseReceived(List<BoxData> boxes)
        {
            if (boxes == null || boxes.Count == 0)
            {
                return;
            }

            foreach (BoxData box in boxes)
            {
                if (!TryProjectToWorld(box, out Vector3 worldPosition))
                {
                    Debug.LogWarning($"[Detection] Could not project detection '{box.label}' to a world position.");
                    continue;
                }

                ParseLabelAndConfidence(box.label, out string label, out float confidence);
                DetectionResult result = new(label, confidence, worldPosition);
                DetectionReceived?.Invoke(result);

                Debug.Log($"[Detection] Stain detected. Label={result.Label}, Confidence={result.Confidence:F2}, World={result.WorldPosition}.");
            }
        }

        private bool TryProjectToWorld(BoxData box, out Vector3 worldPosition)
        {
            worldPosition = default;

            if (objectDetectionVisualizer == null)
            {
                return false;
            }

#if MRUK_INSTALLED
            GetProjectionBounds(box, out float xmin, out float ymin, out float xmax, out float ymax);

            if (logProjectionDetails)
            {
                Debug.Log($"[Detection] Projecting detection box. Mode={boxCoordinateMode}, RawPosition={box.position}, RawScale={box.scale}, MinMax=({xmin:F1}, {ymin:F1}, {xmax:F1}, {ymax:F1}).");
            }

            if (projectDetectionsByFloorRay
                && TryProjectBoxCenterToFloorPlane(xmin, ymin, xmax, ymax, out worldPosition))
            {
                return true;
            }

            bool hasDepthPosition = objectDetectionVisualizer.TryProject(
                xmin,
                ymin,
                xmax,
                ymax,
                out worldPosition,
                out _,
                out _);

            if (!hasDepthPosition)
            {
                return false;
            }

            if (projectDetectionsToFloorPlane
                && TryProjectDepthPositionToFloorPlane(worldPosition, out Vector3 floorPosition))
            {
                Debug.Log($"[Detection] Snapped detection depth position to floor plane. DepthWorld={worldPosition}, FloorWorld={floorPosition}.");
                worldPosition = floorPosition;
            }

            return true;
#else
            Debug.LogWarning("[Detection] Cannot project detection to world because MRUK_INSTALLED is not defined.");
            return false;
#endif
        }

        private void GetProjectionBounds(BoxData box, out float xmin, out float ymin, out float xmax, out float ymax)
        {
            if (boxCoordinateMode == DetectionBoxCoordinateMode.CenterSize)
            {
                float halfWidth = box.scale.x * 0.5f;
                float halfHeight = box.scale.y * 0.5f;

                xmin = box.position.x - halfWidth;
                ymin = box.position.y - halfHeight;
                xmax = box.position.x + halfWidth;
                ymax = box.position.y + halfHeight;
            }
            else
            {
                xmin = box.position.x;
                ymin = box.position.y;
                xmax = box.scale.x;
                ymax = box.scale.y;
            }

            if (xmax < xmin)
            {
                (xmin, xmax) = (xmax, xmin);
            }

            if (ymax < ymin)
            {
                (ymin, ymax) = (ymax, ymin);
            }
        }

#if MRUK_INSTALLED
        private bool TryProjectBoxCenterToFloorPlane(
            float xmin,
            float ymin,
            float xmax,
            float ymax,
            out Vector3 floorPosition)
        {
            floorPosition = default;

            if (passthroughCamera == null)
            {
                passthroughCamera = FindAnyObjectByType<PassthroughCameraAccess>();
            }

            if (passthroughCamera == null || !passthroughCamera.IsPlaying)
            {
                return false;
            }

            Texture cameraTexture = passthroughCamera.GetTexture();
            if (cameraTexture == null || cameraTexture.width <= 0 || cameraTexture.height <= 0)
            {
                return false;
            }

            if (floorProvider == null)
            {
                floorProvider = FindFirstObjectByType<MRUKFloorProvider>();
            }

            if (floorProvider == null || !floorProvider.TryGetFloorData(out FloorPlaneData floorData))
            {
                return false;
            }

            Vector3 floorNormal = floorData.NormalWorld.normalized;
            if (floorNormal.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float centerX = (xmin + xmax) * 0.5f;
            float centerY = (ymin + ymax) * 0.5f;
            float normalizedCenterX = Mathf.Clamp01(centerX / cameraTexture.width);
            float normalizedCenterY = Mathf.Clamp01(centerY / cameraTexture.height);

            Ray floorRay = passthroughCamera.ViewportPointToRay(
                new Vector2(normalizedCenterX, 1f - normalizedCenterY));

            float denominator = Vector3.Dot(floorNormal, floorRay.direction);
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                return false;
            }

            float distance = Vector3.Dot(floorData.CenterWorld - floorRay.origin, floorNormal) / denominator;
            if (distance < 0f)
            {
                return false;
            }

            floorPosition = floorRay.origin + floorRay.direction * distance;

            if (logProjectionDetails)
            {
                Debug.Log(
                    $"[Detection] Projected detection by floor ray. " +
                    $"Camera={passthroughCamera.gameObject.name}, Texture={cameraTexture.width}x{cameraTexture.height}, " +
                    $"Viewport=({normalizedCenterX:F3}, {1f - normalizedCenterY:F3}), RayOrigin={floorRay.origin}, FloorWorld={floorPosition}.");
            }

            return true;
        }

        private bool TryProjectDepthPositionToFloorPlane(Vector3 depthWorldPosition, out Vector3 floorPosition)
        {
            floorPosition = default;

            if (floorProvider == null)
            {
                floorProvider = FindFirstObjectByType<MRUKFloorProvider>();
            }

            if (floorProvider == null || !floorProvider.TryGetFloorData(out FloorPlaneData floorData))
            {
                return false;
            }

            Vector3 floorNormal = floorData.NormalWorld.normalized;
            if (floorNormal.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            if (Mathf.Abs(floorNormal.y) < 0.0001f)
            {
                return false;
            }

            float floorYAtDepthXZ = floorData.CenterWorld.y
                - ((floorNormal.x * (depthWorldPosition.x - floorData.CenterWorld.x))
                + (floorNormal.z * (depthWorldPosition.z - floorData.CenterWorld.z))) / floorNormal.y;

            floorPosition = new Vector3(depthWorldPosition.x, floorYAtDepthXZ, depthWorldPosition.z);
            return true;
        }
#endif

        private static void ParseLabelAndConfidence(string source, out string label, out float confidence)
        {
            label = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
            confidence = 0f;

            int lastSpaceIndex = label.LastIndexOf(' ');
            if (lastSpaceIndex <= 0 || lastSpaceIndex >= label.Length - 1)
            {
                return;
            }

            string confidenceText = label[(lastSpaceIndex + 1)..];
            if (!float.TryParse(confidenceText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedConfidence))
            {
                return;
            }

            label = label[..lastSpaceIndex];
            confidence = parsedConfidence;
        }
    }
}
