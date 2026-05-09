using System;
using System.Collections.Generic;
using System.Globalization;
using Meta.XR.BuildingBlocks.AIBlocks;
using MRStainCleaning.MRUKFloor;
using UnityEngine;

namespace MRStainCleaning.Detection
{
    [DisallowMultipleComponent]
    public sealed class ObjectDetectionWorldAdapter : MonoBehaviour
    {
        [SerializeField]
        private ObjectDetectionAgent objectDetectionAgent;

        [SerializeField]
        private ObjectDetectionVisualizer objectDetectionVisualizer;

        [SerializeField]
        private MRUKFloorProvider floorProvider;

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
            bool hasDepthPosition = objectDetectionVisualizer.TryProject(
                box.position.x,
                box.position.y,
                box.scale.x,
                box.scale.y,
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

#if MRUK_INSTALLED
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
