using Hypertonic.GridPlacement;
using MRStainCleaning.MRUKFloor;
using UnityEngine;

namespace MRStainCleaning.Grid
{
    [DisallowMultipleComponent]
    public sealed class FloorGridBinder : MonoBehaviour
    {
        [SerializeField]
        private MRUKFloorProvider floorProvider;

        [SerializeField]
        private GridManager gridManager;

        [SerializeField]
        private GridSettings gridSettingsTemplate;

        [SerializeField]
        private bool createGridManagerIfMissing = true;

        [SerializeField]
        private bool displayGridAfterSetup = true;

        [SerializeField]
        private float floorNormalOffset = 0.001f;

        public bool IsReady { get; private set; }
        public GridManager GridManager => gridManager;
        public GridSettings RuntimeGridSettings { get; private set; }
        public FloorPlaneData FloorData { get; private set; }

        private void Awake()
        {
            if (gridManager == null)
            {
                gridManager = GetComponent<GridManager>();
            }
        }

        private void Start()
        {
            if (floorProvider == null)
            {
                floorProvider = FindObjectOfType<MRUKFloorProvider>();
            }

            if (gridSettingsTemplate == null && gridManager != null)
            {
                gridSettingsTemplate = gridManager.GridSettings;
            }

            if (floorProvider == null)
            {
                Debug.LogWarning("[Grid] Floor grid binder could not find an MRUKFloorProvider.");
                return;
            }

            floorProvider.FloorDataReady += HandleFloorDataReady;

            if (floorProvider.HasFloorData)
            {
                HandleFloorDataReady(floorProvider.CurrentFloorData);
            }
        }

        private void OnDestroy()
        {
            if (floorProvider != null)
            {
                floorProvider.FloorDataReady -= HandleFloorDataReady;
            }
        }

        public bool TryGetCellIndex(Vector3 worldPosition, out Vector2Int cellIndex)
        {
            cellIndex = default;

            if (!IsReady || gridManager == null || RuntimeGridSettings == null)
            {
                return false;
            }

            cellIndex = PlacementGrid.GetCellIndexFromWorldPosition(
                worldPosition,
                RuntimeGridSettings,
                gridManager.RuntimeGridRotation,
                gridManager.RuntimeGridPosition);

            return IsCellInGrid(cellIndex);
        }

        public bool TryGetCellCenterWorld(Vector2Int cellIndex, out Vector3 cellCenterWorld)
        {
            cellCenterWorld = default;

            if (!IsReady || gridManager == null || RuntimeGridSettings == null || !IsCellInGrid(cellIndex))
            {
                return false;
            }

            cellCenterWorld = GridUtilities.GetWorldPositionFromCellIndex(
                gridManager,
                cellIndex,
                RuntimeGridSettings,
                gridManager.RuntimeGridPosition);

            return true;
        }

        public bool IsCellInGrid(Vector2Int cellIndex)
        {
            return RuntimeGridSettings != null
                && cellIndex.x >= 0
                && cellIndex.y >= 0
                && cellIndex.x < RuntimeGridSettings.AmountOfCellsX
                && cellIndex.y < RuntimeGridSettings.AmountOfCellsY;
        }

        private void HandleFloorDataReady(FloorPlaneData floorData)
        {
            if (gridSettingsTemplate == null)
            {
                Debug.LogWarning("[Grid] Cannot generate floor grid because no GridSettings template is assigned.");
                return;
            }

            EnsureGridManager();

            if (gridManager == null)
            {
                Debug.LogWarning("[Grid] Cannot generate floor grid because no GridManager is available.");
                return;
            }

            FloorData = floorData;
            RuntimeGridSettings = CreateRuntimeGridSettings(gridSettingsTemplate, floorData);

            if (gridManager.IsSetup)
            {
                Debug.LogWarning("[Grid] GridManager was already set up before floor data arrived. The grid will be aligned to the floor, but its cell counts keep the existing setup.");
            }
            else
            {
                gridManager.Setup(RuntimeGridSettings);
            }

            Vector3 floorOffsetDirection = Vector3.Dot(floorData.NormalWorld, Vector3.up) >= 0f
                ? floorData.NormalWorld.normalized
                : -floorData.NormalWorld.normalized;

            Vector3 gridPosition = floorData.CenterWorld + floorOffsetDirection * floorNormalOffset;
            float yaw = CalculateYawFromFloorRotation(floorData.RotationWorld);

            gridManager.MoveGridTo(gridPosition);
            gridManager.RotateGridTo(yaw);

            if (displayGridAfterSetup)
            {
                gridManager.DisplayGrid();
            }

            IsReady = true;
            Debug.Log(
                $"[Grid] Floor grid generated. Position={gridPosition}, RotationY={yaw:F1}, " +
                $"Cells={RuntimeGridSettings.AmountOfCellsX}x{RuntimeGridSettings.AmountOfCellsY}, CellSize={RuntimeGridSettings.CellSize:F3}.");
        }

        private void EnsureGridManager()
        {
            if (gridManager != null || !createGridManagerIfMissing)
            {
                return;
            }

            GameObject gridObject = new("Floor Grid Runtime");
            gridObject.transform.SetParent(transform, false);
            gridManager = gridObject.AddComponent<GridManager>();
        }

        private static GridSettings CreateRuntimeGridSettings(GridSettings template, FloorPlaneData floorData)
        {
            GridSettings runtimeSettings = Instantiate(template);
            runtimeSettings.name = $"{template.name} Runtime";
            runtimeSettings.Key = $"{template.Key}_RuntimeFloor";

            float desiredCellSize = template.DesiredCellSize > 0f ? template.DesiredCellSize : template.CellSize;
            desiredCellSize = Mathf.Max(0.01f, desiredCellSize);

            int cellsX = Mathf.Max(1, Mathf.CeilToInt(floorData.Width / desiredCellSize));
            float cellSize = floorData.Width / cellsX;
            int cellsY = Mathf.Max(1, Mathf.CeilToInt(floorData.Height / cellSize));

            runtimeSettings.Width = floorData.Width;
            runtimeSettings.Height = cellsY * cellSize;
            runtimeSettings.AmountOfCellsX = cellsX;
            runtimeSettings.AmountOfCellsY = cellsY;
            runtimeSettings.GridSizeRatio = new Vector2Int(cellsX, cellsY);
            runtimeSettings.DesiredCellSize = desiredCellSize;
            runtimeSettings.GridPosition = floorData.CenterWorld;
            runtimeSettings.GridRotation = CalculateYawFromFloorRotation(floorData.RotationWorld);
            runtimeSettings.InitialPlacementCellIndex = new Vector2Int(cellsX / 2, cellsY / 2);

            return runtimeSettings;
        }

        private static float CalculateYawFromFloorRotation(Quaternion floorRotation)
        {
            Vector3 gridRight = floorRotation * Vector3.right;
            gridRight.y = 0f;

            if (gridRight.sqrMagnitude < 0.0001f)
            {
                gridRight = floorRotation * Vector3.up;
                gridRight.y = 0f;
            }

            if (gridRight.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            gridRight.Normalize();
            return Mathf.Atan2(-gridRight.z, gridRight.x) * Mathf.Rad2Deg;
        }
    }
}
