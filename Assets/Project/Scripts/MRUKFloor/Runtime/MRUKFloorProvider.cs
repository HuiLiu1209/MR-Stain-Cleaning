using System;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MRStainCleaning.MRUKFloor
{
    [DisallowMultipleComponent]
    public sealed class MRUKFloorProvider : MonoBehaviour
    {
        [SerializeField]
        private bool loadSceneFromDeviceIfNeeded;

        [SerializeField]
        private bool requestSceneCaptureIfNoDataFound = true;

        public event Action<FloorPlaneData> FloorDataReady;

        public bool HasFloorData { get; private set; }
        public FloorPlaneData CurrentFloorData { get; private set; }

        private bool isLoadingScene;

        private void Start()
        {
            if (MRUK.Instance == null)
            {
                Debug.LogWarning("[MRUKFloor] MRUK instance was not found. Add the MR Utility Kit building block to the scene.");
                return;
            }

            MRUK.Instance.RegisterSceneLoadedCallback(HandleSceneLoaded);

            if (!MRUK.Instance.IsInitialized && loadSceneFromDeviceIfNeeded)
            {
                LoadSceneFromDevice();
            }
        }

        private void OnDestroy()
        {
            if (MRUK.Instance != null)
            {
                MRUK.Instance.SceneLoadedEvent.RemoveListener(HandleSceneLoaded);
            }
        }

        public bool TryGetFloorData(out FloorPlaneData floorData)
        {
            floorData = CurrentFloorData;
            return HasFloorData;
        }

        private async void LoadSceneFromDevice()
        {
            if (isLoadingScene || MRUK.Instance == null)
            {
                return;
            }

            isLoadingScene = true;
            MRUK.LoadDeviceResult result = await MRUK.Instance.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound);
            isLoadingScene = false;

            if (result != MRUK.LoadDeviceResult.Success)
            {
                Debug.LogWarning($"[MRUKFloor] MRUK scene load finished with result: {result}.");
            }
        }

        private void HandleSceneLoaded()
        {
            if (!TryCreateFloorData(out FloorPlaneData floorData))
            {
                HasFloorData = false;
                Debug.LogWarning("[MRUKFloor] No floor plane data was found in the loaded MRUK scene.");
                return;
            }

            CurrentFloorData = floorData;
            HasFloorData = true;
            FloorDataReady?.Invoke(CurrentFloorData);

            Debug.Log(
                $"[MRUKFloor] Floor found. Center={floorData.CenterWorld}, RotationY={floorData.RotationWorld.eulerAngles.y:F1}, " +
                $"Size={floorData.Width:F2}x{floorData.Height:F2}, BoundaryPoints={floorData.BoundaryLocal2D.Count}.");
        }

        private static bool TryCreateFloorData(out FloorPlaneData floorData)
        {
            floorData = default;

            MRUKRoom room = MRUK.Instance.GetCurrentRoom();
            if (room == null && MRUK.Instance.Rooms.Count > 0)
            {
                room = MRUK.Instance.Rooms[0];
            }

            if (room == null)
            {
                return false;
            }

            MRUKAnchor floorAnchor = FindLargestFloorAnchor(room);
            if (floorAnchor == null || !floorAnchor.PlaneRect.HasValue)
            {
                return false;
            }

            Rect planeRect = floorAnchor.PlaneRect.Value;
            Vector3 centerLocal = new(planeRect.center.x, planeRect.center.y, 0f);
            Vector3 centerWorld = floorAnchor.transform.TransformPoint(centerLocal);
            List<Vector2> boundary = floorAnchor.PlaneBoundary2D != null
                ? new List<Vector2>(floorAnchor.PlaneBoundary2D)
                : new List<Vector2>();

            floorData = new FloorPlaneData(
                room,
                floorAnchor,
                centerWorld,
                floorAnchor.transform.rotation,
                floorAnchor.transform.forward,
                planeRect.width,
                planeRect.height,
                boundary);

            return true;
        }

        private static MRUKAnchor FindLargestFloorAnchor(MRUKRoom room)
        {
            MRUKAnchor largestAnchor = null;
            float largestArea = 0f;

            foreach (MRUKAnchor anchor in room.FloorAnchors)
            {
                if (anchor == null || !anchor.PlaneRect.HasValue)
                {
                    continue;
                }

                Rect planeRect = anchor.PlaneRect.Value;
                float area = planeRect.width * planeRect.height;

                if (area > largestArea)
                {
                    largestArea = area;
                    largestAnchor = anchor;
                }
            }

            return largestAnchor;
        }
    }
}
