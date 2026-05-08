using System.Collections;
using System.Collections.Generic;
using Hypertonic.GridPlacement.Enums;
using Hypertonic.GridPlacement.Models;
using MRStainCleaning.Detection;
using MRStainCleaning.Grid;
using UnityEngine;

namespace MRStainCleaning.StainSpawning
{
    [DisallowMultipleComponent]
    public sealed class StainPrefabSpawner : MonoBehaviour
    {
        [SerializeField]
        private ObjectDetectionWorldAdapter detectionAdapter;

        [SerializeField]
        private FloorGridBinder floorGridBinder;

        [SerializeField]
        private GameObject stainPrefab;

        [SerializeField]
        private ObjectAlignment objectAlignment = ObjectAlignment.CENTER;

        [SerializeField]
        private bool alignPrefabYawToGrid = true;

        private readonly HashSet<Vector2Int> spawnedCells = new();

        private void Start()
        {
            if (detectionAdapter == null)
            {
                detectionAdapter = FindFirstObjectByType<ObjectDetectionWorldAdapter>();
            }

            if (floorGridBinder == null)
            {
                floorGridBinder = FindFirstObjectByType<FloorGridBinder>();
            }

            if (detectionAdapter == null)
            {
                Debug.LogWarning("[StainSpawn] StainPrefabSpawner could not find an ObjectDetectionWorldAdapter.");
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
            if (stainPrefab == null)
            {
                Debug.LogWarning("[StainSpawn] Cannot spawn stain prefab because no prefab is assigned.");
                return;
            }

            if (floorGridBinder == null || !floorGridBinder.IsReady || floorGridBinder.GridManager == null)
            {
                Debug.LogWarning("[StainSpawn] Detection received before the floor grid was ready.");
                return;
            }

            if (!floorGridBinder.TryGetCellIndex(detectionResult.WorldPosition, out Vector2Int cellIndex))
            {
                Debug.LogWarning($"[StainSpawn] Detection world position is outside the grid. World={detectionResult.WorldPosition}.");
                return;
            }

            if (!spawnedCells.Add(cellIndex))
            {
                Debug.Log($"[StainSpawn] Stain prefab already exists for cell {cellIndex}.");
                return;
            }

            StartCoroutine(SpawnAtCell(cellIndex));
        }

        private IEnumerator SpawnAtCell(Vector2Int cellIndex)
        {
            if (!floorGridBinder.TryGetCellCenterWorld(cellIndex, out Vector3 cellCenterWorld))
            {
                spawnedCells.Remove(cellIndex);
                Debug.LogWarning($"[StainSpawn] Could not resolve cell center for {cellIndex}.");
                yield break;
            }

            Quaternion rotation = alignPrefabYawToGrid
                ? Quaternion.Euler(0f, floorGridBinder.GridManager.RuntimeGridRotation, 0f)
                : stainPrefab.transform.rotation;

            GameObject stainInstance = Instantiate(stainPrefab, cellCenterWorld, rotation);

            if (!stainInstance.TryGetComponent(out GridObjectInfo _))
            {
                stainInstance.AddComponent<GridObjectInfo>();
            }

            bool placed = false;
            yield return floorGridBinder.GridManager.AddObjectToGridByCell(
                stainInstance,
                cellIndex,
                objectAlignment,
                success => placed = success);

            if (!placed)
            {
                spawnedCells.Remove(cellIndex);
                Destroy(stainInstance);
                Debug.LogWarning($"[StainSpawn] Failed to place stain prefab at cell {cellIndex}.");
                yield break;
            }

            Debug.Log($"[StainSpawn] Spawned stain prefab. Cell={cellIndex}, World={cellCenterWorld}.");
        }
    }
}
