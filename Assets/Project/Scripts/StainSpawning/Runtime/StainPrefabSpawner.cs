using System;
using System.Collections;
using System.Collections.Generic;
using Hypertonic.GridPlacement.Enums;
using Hypertonic.GridPlacement.CustomSizing;
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

        [SerializeField]
        private bool scalePrefabToCell = true;

        [SerializeField]
        [Min(0.01f)]
        private float cellFootprintScale = 0.9f;

        [SerializeField]
        private bool alignPrefabBottomToGrid = true;

        [SerializeField]
        private bool forcePlacedPrefabBottomToGridPlane = true;

        private readonly HashSet<Vector2Int> spawnedCells = new();
        private readonly HashSet<Vector2Int> reservedCells = new();
        private readonly Queue<SpawnRequest> spawnQueue = new();
        private bool isProcessingSpawnQueue;

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

            if (reservedCells.Contains(cellIndex))
            {
                Debug.Log($"[StainSpawn] Stain prefab already exists for cell {cellIndex}.");
                return;
            }

            reservedCells.Add(cellIndex);
            spawnQueue.Enqueue(new SpawnRequest(cellIndex, detectionResult.WorldPosition));

            if (!isProcessingSpawnQueue)
            {
                StartCoroutine(ProcessSpawnQueue());
            }
        }

        private IEnumerator ProcessSpawnQueue()
        {
            isProcessingSpawnQueue = true;

            while (spawnQueue.Count > 0)
            {
                SpawnRequest request = spawnQueue.Dequeue();
                bool placed = false;

                yield return SpawnAtCell(
                    request.CellIndex,
                    request.DetectionWorldPosition,
                    success => placed = success);

                if (placed)
                {
                    spawnedCells.Add(request.CellIndex);
                }
                else
                {
                    reservedCells.Remove(request.CellIndex);
                }
            }

            isProcessingSpawnQueue = false;
        }

        private IEnumerator SpawnAtCell(Vector2Int cellIndex, Vector3 detectionWorldPosition, Action<bool> placementCompleted)
        {
            Vector3 initialPosition = floorGridBinder.GridManager.RuntimeGridPosition;
            Quaternion rotation = GetInitialPrefabRotation();

            GameObject stainInstance = Instantiate(stainPrefab, initialPosition, rotation);

            if (scalePrefabToCell)
            {
                ScalePrefabToCell(stainInstance);
            }

            if (alignPrefabBottomToGrid)
            {
                ApplyGridHeightFromPrefabBottom(stainInstance);
            }

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
                Destroy(stainInstance);
                Debug.LogWarning($"[StainSpawn] Failed to place stain prefab at cell {cellIndex}.");
                placementCompleted?.Invoke(false);
                yield break;
            }

            if (forcePlacedPrefabBottomToGridPlane)
            {
                ForcePrefabBottomToGridPlane(stainInstance, cellIndex);
            }

            Vector3 cellCenterWorld = floorGridBinder.TryGetCellCenterWorld(cellIndex, out Vector3 resolvedCellCenter)
                ? resolvedCellCenter
                : Vector3.zero;

            Debug.Log(
                $"[StainSpawn] Spawned stain prefab via GridManager.AddObjectToGridByCell. " +
                $"Cell={cellIndex}, DetectionWorld={detectionWorldPosition}, CellCenter={cellCenterWorld}, World={stainInstance.transform.position}.");

            placementCompleted?.Invoke(true);
        }

        private Quaternion GetInitialPrefabRotation()
        {
            if (!alignPrefabYawToGrid)
            {
                return stainPrefab.transform.rotation;
            }

            if (floorGridBinder.RuntimeGridSettings != null && floorGridBinder.RuntimeGridSettings.ParentToGrid)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(0f, floorGridBinder.GridManager.RuntimeGridRotation, 0f);
        }

        private void ScalePrefabToCell(GameObject stainInstance)
        {
            if (!floorGridBinder.TryGetCellSize(out float cellSize))
            {
                Debug.LogWarning("[StainSpawn] Cannot scale stain prefab because the grid cell size is unavailable.");
                return;
            }

            float targetFootprint = cellSize * cellFootprintScale;
            if (targetFootprint <= 0f)
            {
                Debug.LogWarning($"[StainSpawn] Cannot scale stain prefab because target footprint is invalid. Target={targetFootprint:F3}.");
                return;
            }

            Quaternion originalRotation = stainInstance.transform.rotation;
            stainInstance.transform.rotation = Quaternion.identity;
            bool hasBounds = TryGetObjectBounds(stainInstance, out Bounds bounds);
            stainInstance.transform.rotation = originalRotation;

            if (!hasBounds)
            {
                float fallbackScale = targetFootprint;
                stainInstance.transform.localScale *= fallbackScale;
                Debug.LogWarning($"[StainSpawn] Stain prefab has no renderer or collider bounds. Applied fallback uniform scale={fallbackScale:F3}.");
                return;
            }

            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint <= 0f)
            {
                Debug.LogWarning($"[StainSpawn] Cannot scale stain prefab because bounds footprint is invalid. Bounds={bounds.size}.");
                return;
            }

            float uniformMultiplier = targetFootprint / footprint;
            stainInstance.transform.localScale *= uniformMultiplier;
        }

        private void ApplyGridHeightFromPrefabBottom(GameObject stainInstance)
        {
            if (!TryGetUnrotatedObjectBounds(stainInstance, out Bounds bounds))
            {
                Debug.LogWarning("[StainSpawn] Cannot align stain prefab bottom to grid because it has no renderer or collider bounds.");
                return;
            }

            float bottomLocalY = bounds.min.y - stainInstance.transform.position.y;
            GridHeightPositioner gridHeightPositioner = stainInstance.GetComponent<GridHeightPositioner>();
            if (gridHeightPositioner == null)
            {
                gridHeightPositioner = stainInstance.AddComponent<GridHeightPositioner>();
            }

            gridHeightPositioner.GridHeight = bottomLocalY;
        }

        private void ForcePrefabBottomToGridPlane(GameObject stainInstance, Vector2Int cellIndex)
        {
            if (!floorGridBinder.TryGetCellCenterWorld(cellIndex, out Vector3 cellCenterWorld))
            {
                Debug.LogWarning($"[StainSpawn] Cannot force stain prefab onto grid plane because cell {cellIndex} could not be resolved.");
                return;
            }

            if (!TryGetObjectBounds(stainInstance, out Bounds bounds))
            {
                Vector3 position = stainInstance.transform.position;
                position.y = cellCenterWorld.y;
                stainInstance.transform.position = position;
                Debug.LogWarning("[StainSpawn] Stain prefab has no renderer or collider bounds. Forced pivot Y to grid plane instead.");
                return;
            }

            float yDelta = cellCenterWorld.y - bounds.min.y;
            if (Mathf.Abs(yDelta) > 0.0001f)
            {
                stainInstance.transform.position += Vector3.up * yDelta;
            }

            if (TryGetObjectBounds(stainInstance, out Bounds correctedBounds))
            {
                Debug.Log(
                    $"[StainSpawn] Forced stain prefab bottom to grid plane. " +
                    $"Cell={cellIndex}, GridY={cellCenterWorld.y:F3}, BottomY={correctedBounds.min.y:F3}, DeltaY={yDelta:F3}.");
            }
        }

        private static bool TryGetUnrotatedObjectBounds(GameObject stainInstance, out Bounds bounds)
        {
            Quaternion originalRotation = stainInstance.transform.rotation;
            stainInstance.transform.rotation = Quaternion.identity;
            bool hasBounds = TryGetObjectBounds(stainInstance, out bounds);
            stainInstance.transform.rotation = originalRotation;
            return hasBounds;
        }

        private static bool TryGetObjectBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                if (!collider.enabled)
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private readonly struct SpawnRequest
        {
            public SpawnRequest(Vector2Int cellIndex, Vector3 detectionWorldPosition)
            {
                CellIndex = cellIndex;
                DetectionWorldPosition = detectionWorldPosition;
            }

            public Vector2Int CellIndex { get; }
            public Vector3 DetectionWorldPosition { get; }
        }
    }
}
