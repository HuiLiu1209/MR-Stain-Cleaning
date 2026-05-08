using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MRStainCleaning.MRUKFloor
{
    [DisallowMultipleComponent]
    public sealed class MRUKFloorProvider : MonoBehaviour
    {
        [SerializeField]
        private bool loadSceneFromDeviceIfNeeded = true;

        [SerializeField]
        private bool requestSceneCaptureIfNoDataFound = true;

        [SerializeField]
        private bool createMRUKIfMissing = true;

        public event Action<FloorPlaneData> FloorDataReady;

        public bool HasFloorData { get; private set; }
        public FloorPlaneData CurrentFloorData { get; private set; }

        private bool isLoadingScene;
        private bool registeredSceneLoadedCallback;

        private IEnumerator Start()
        {
            yield return null;

            if (MRUK.Instance == null)
            {
                EnsureMRUKInstance();
            }

            if (MRUK.Instance == null)
            {
                Debug.LogWarning("[MRUKFloor] MRUK instance was not found and could not be created.");
                yield break;
            }

            RegisterSceneLoadedCallback();

            if (HasFloorData || TryPublishLoadedFloorData())
            {
                yield break;
            }

            if (loadSceneFromDeviceIfNeeded)
            {
                LoadSceneFromDevice();
            }
            else
            {
                Debug.LogWarning("[MRUKFloor] No loaded floor data was found and device scene loading is disabled.");
            }
        }

        private void OnDestroy()
        {
            if (registeredSceneLoadedCallback && MRUK.Instance != null)
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
            try
            {
                MRUK.LoadDeviceResult result = await MRUK.Instance.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound);

                if (result != MRUK.LoadDeviceResult.Success)
                {
                    Debug.LogWarning($"[MRUKFloor] MRUK scene load finished with result: {result}.");
                }

                if (!HasFloorData)
                {
                    TryPublishLoadedFloorData();
                }
            }
            finally
            {
                isLoadingScene = false;
            }
        }

        private void HandleSceneLoaded()
        {
            if (!TryPublishLoadedFloorData())
            {
                HasFloorData = false;
                Debug.LogWarning("[MRUKFloor] No floor plane data was found in the loaded MRUK scene.");
            }
        }

        private void EnsureMRUKInstance()
        {
            if (!createMRUKIfMissing || MRUK.Instance != null)
            {
                return;
            }

            GameObject mrukObject = new("MRUK Runtime");
            MRUK mruk = mrukObject.AddComponent<MRUK>();
            mruk.SceneSettings = new MRUK.MRUKSettings
            {
                DataSource = MRUK.SceneDataSource.Device,
                RoomIndex = -1,
                RoomPrefabs = Array.Empty<GameObject>(),
                SceneJsons = Array.Empty<TextAsset>(),
                LoadSceneOnStartup = false,
                EnableHighFidelityScene = false,
                SeatWidth = 0.6f
            };

            Debug.Log("[MRUKFloor] Created runtime MRUK instance.");
        }

        private void RegisterSceneLoadedCallback()
        {
            if (registeredSceneLoadedCallback || MRUK.Instance == null)
            {
                return;
            }

            MRUK.Instance.RegisterSceneLoadedCallback(HandleSceneLoaded);
            registeredSceneLoadedCallback = true;
        }

        private bool TryPublishLoadedFloorData()
        {
            if (!TryCreateFloorData(out FloorPlaneData floorData))
            {
                return false;
            }

            CurrentFloorData = floorData;
            HasFloorData = true;
            FloorDataReady?.Invoke(CurrentFloorData);

            Debug.Log(
                $"[MRUKFloor] Floor found. Center={floorData.CenterWorld}, RotationY={floorData.RotationWorld.eulerAngles.y:F1}, " +
                $"Size={floorData.Width:F2}x{floorData.Height:F2}, BoundaryPoints={floorData.BoundaryLocal2D.Count}.");

            return true;
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
