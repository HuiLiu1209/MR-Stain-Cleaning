using System;
using System.Collections.Generic;
using System.Globalization;
using Meta.XR.BuildingBlocks.AIBlocks;
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
            return objectDetectionVisualizer.TryProject(
                box.position.x,
                box.position.y,
                box.scale.x,
                box.scale.y,
                out worldPosition,
                out _,
                out _);
#else
            return false;
#endif
        }

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
