using MRStainCleaning.Detection;
using MRStainCleaning.Grid;
using UnityEngine;

namespace MRStainCleaning.StainSpawning
{
    [DisallowMultipleComponent]
    public sealed class StainGridCellLogger : MonoBehaviour
    {
        [SerializeField]
        private ObjectDetectionWorldAdapter detectionAdapter;

        [SerializeField]
        private FloorGridBinder floorGridBinder;

        private void Start()
        {
            if (detectionAdapter == null)
            {
                detectionAdapter = FindObjectOfType<ObjectDetectionWorldAdapter>();
            }

            if (floorGridBinder == null)
            {
                floorGridBinder = FindObjectOfType<FloorGridBinder>();
            }

            if (detectionAdapter == null)
            {
                Debug.LogWarning("[StainSpawn] StainGridCellLogger could not find an ObjectDetectionWorldAdapter.");
                return;
            }

            detectionAdapter.DetectionReceived += HandleDetectionReceived;
        }

        private void OnDestroy()
        {
            if (detectionAdapter != null)
            {
                detectionAdapter.DetectionReceived -= HandleDetectionReceived;
            }
        }

        private void HandleDetectionReceived(DetectionResult detectionResult)
        {
            if (floorGridBinder == null || !floorGridBinder.IsReady)
            {
                Debug.LogWarning("[StainSpawn] Detection received before the floor grid was ready.");
                return;
            }

            if (!floorGridBinder.TryGetCellIndex(detectionResult.WorldPosition, out Vector2Int cellIndex))
            {
                Debug.LogWarning($"[StainSpawn] Detection world position is outside the grid. World={detectionResult.WorldPosition}.");
                return;
            }

            Debug.Log($"[StainSpawn] Detection world position -> cell index. World={detectionResult.WorldPosition}, Cell={cellIndex}.");
        }
    }
}
