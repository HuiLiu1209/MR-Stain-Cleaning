using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hypertonic.GridPlacement;
using Hypertonic.GridPlacement.GridInput;
using Hypertonic.GridPlacement.Models;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MRStainCleaning.Grid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridManager))]
    public sealed class GridTestPrefabPlacementStarter : MonoBehaviour
    {
        [SerializeField]
        private GameObject prefabToPlace;

        [SerializeField]
        private bool enterPlacementOnStart = true;

        [SerializeField]
        private bool confirmOnPrimaryRelease = true;

        [SerializeField]
        private bool confirmOnEnter = true;

        [SerializeField]
        private bool startPlacementOnSpace = true;

        private GridInputDefinition hoverInputDefinition;
        private GridManager gridManager;
        private Camera gridCamera;
        private MethodInfo updateGridObjectPlacementMethod;
        private GameObject activePlacementObject;

        private void Awake()
        {
            gridManager = GetComponent<GridManager>();
            hoverInputDefinition = ScriptableObject.CreateInstance<GridEditorHoverInputDefinition>();
            updateGridObjectPlacementMethod = typeof(GridManager).GetMethod(
                "UpdateGridObjectPlacement",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private IEnumerator Start()
        {
            while (!gridManager.IsSetup)
            {
                yield return null;
            }

            UseHoverInputForCurrentPlatform();
            CacheGridCamera();

            if (!enterPlacementOnStart)
            {
                yield break;
            }

            StartPlacement();
        }

        private void Update()
        {
            if (startPlacementOnSpace && !gridManager.IsPlacingGridObject && WasSpacePressedThisFrame())
            {
                StartPlacement();
                return;
            }

            UpdatePlacementFromMouse();

            if (confirmOnEnter && gridManager.IsPlacingGridObject && WasEnterPressedThisFrame())
            {
                ConfirmPlacement();
                return;
            }

            if (!confirmOnPrimaryRelease || !gridManager.IsPlacingGridObject)
            {
                return;
            }

            if (WasPrimaryReleasedThisFrame())
            {
                ConfirmPlacement();
            }
        }

        public void StartPlacement()
        {
            if (prefabToPlace == null)
            {
                Debug.LogWarning("[Grid] Cannot start test prefab placement because no prefab is assigned.");
                return;
            }

            if (gridManager.IsPlacingGridObject)
            {
                return;
            }

            activePlacementObject = Instantiate(prefabToPlace, gridManager.RuntimeGridPosition, Quaternion.identity);

            if (!activePlacementObject.TryGetComponent(out GridObjectInfo _))
            {
                activePlacementObject.AddComponent<GridObjectInfo>();
            }

            gridManager.EnterPlacementMode(activePlacementObject);
            Debug.Log("[Grid] Started test prefab placement.");
        }

        public void ConfirmPlacement()
        {
            if (!gridManager.ConfirmPlacement())
            {
                Debug.LogWarning("[Grid] Test prefab placement is not valid yet.");
                return;
            }

            Debug.Log("[Grid] Confirmed test prefab placement.");
            activePlacementObject = null;
        }

        private void UpdatePlacementFromMouse()
        {
            if (!gridManager.IsPlacingGridObject || updateGridObjectPlacementMethod == null)
            {
                return;
            }

            if (gridCamera == null)
            {
                CacheGridCamera();
            }

            if (gridCamera == null || !TryGetMouseScreenPosition(out Vector3 mouseScreenPosition))
            {
                return;
            }

            Ray ray = gridCamera.ScreenPointToRay(mouseScreenPosition);
            Plane gridPlane = new(Vector3.up, gridManager.RuntimeGridPosition);

            if (!gridPlane.Raycast(ray, out float distance))
            {
                return;
            }

            Vector3 worldPosition = ray.GetPoint(distance);
            updateGridObjectPlacementMethod.Invoke(gridManager, new object[] { worldPosition });
        }

        private void CacheGridCamera()
        {
            string cameraName = gridManager.GridSettings != null
                ? gridManager.GridSettings.GridCanvasEventCameraName
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(cameraName))
            {
                GameObject cameraObject = GameObject.Find(cameraName);

                if (cameraObject != null && cameraObject.TryGetComponent(out Camera namedCamera))
                {
                    gridCamera = namedCamera;
                    return;
                }
            }

            gridCamera = Camera.main;
        }

        private void UseHoverInputForCurrentPlatform()
        {
            List<PlatformGridInputsDefinitionMapping> mappings = new(gridManager.GridSettings.PlatformGridInputsDefinitionMappings);
            PlatformGridInputsDefinitionMapping runtimeMapping = mappings.Find(mapping => mapping.RuntimePlatform == Application.platform);

            if (runtimeMapping == null)
            {
                mappings.Add(new PlatformGridInputsDefinitionMapping(Application.platform, hoverInputDefinition));
            }
            else
            {
                runtimeMapping.GridInputDefinition = hoverInputDefinition;
            }

            gridManager.UpdatePlatformGridInputsDefinitionMappings(mappings);
            Debug.Log("[Grid] Editor hover input enabled. Move with mouse, confirm with Enter.");
        }

        private static bool WasPrimaryReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(0);
#else
            return false;
#endif
        }

        private static bool TryGetMouseScreenPosition(out Vector3 mouseScreenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouseScreenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            mouseScreenPosition = Input.mousePosition;
            return true;
#else
            mouseScreenPosition = default;
            return false;
#endif
        }

        private static bool WasEnterPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }

        private static bool WasSpacePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        private sealed class GridEditorHoverInputDefinition : GridInputDefinition
        {
            public override Vector3? InputPosition()
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    return Mouse.current.position.ReadValue();
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return null;
#endif
            }

            public override bool ShouldInteract()
            {
                return true;
            }
        }
    }
}
