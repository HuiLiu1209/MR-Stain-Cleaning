using UnityEngine;

namespace MRStainCleaning.Detection
{
    public readonly struct DetectionResult
    {
        public DetectionResult(string label, float confidence, Vector3 worldPosition)
        {
            Label = label;
            Confidence = confidence;
            WorldPosition = worldPosition;
        }

        public string Label { get; }
        public float Confidence { get; }
        public Vector3 WorldPosition { get; }
    }
}
