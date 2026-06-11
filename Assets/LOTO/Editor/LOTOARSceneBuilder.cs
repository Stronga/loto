using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class LOTOARSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/loto.unity";
    private const string ArScenePath = "Assets/Scenes/LOTO_AR.unity";
    private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
    private const string PassthroughPrefabPath = "Packages/com.meta.xr.sdk.core/Editor/BuildingBlocks/BlockData/Passthrough/Prefabs/PassthroughUnderlay.prefab";

    [MenuItem("LOTO/Create AR Scene")]
    public static void CreateARScene()
    {
        EnsureARSceneSeed();
        Scene scene = EditorSceneManager.OpenScene(ArScenePath, OpenSceneMode.Single);

        CleanupDeprecatedARObjects();

        GameObject cameraRig = EnsurePrefabInstance("OVRCameraRig", CameraRigPrefabPath);
        GameObject passthroughObject = EnsurePrefabInstance("PassthroughUnderlay", PassthroughPrefabPath);

        ConfigureCameraRig(cameraRig);
        ConfigurePassthrough(passthroughObject);
        GameObject placementRoot = ConfigureMRPlacementRoot();
        ConfigureMRPlacementController(placementRoot, cameraRig);
        EnsureEventSystemObject();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ArScenePath);
        AddSceneToBuildSettings(ArScenePath);

        Selection.activeObject = placementRoot != null ? placementRoot : cameraRig;
        Debug.Log($"Created Meta MR LOTO scene at {ArScenePath}. The generator content is placed once at runtime under LOTO_MR_PlacementRoot.");
    }

    private static void CleanupDeprecatedARObjects()
    {
        DestroySceneObject("OVRHandPrefabBuildingBlock");
    }

    private static void DestroySceneObject(string objectName)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        if (sceneObject != null)
        {
            Object.DestroyImmediate(sceneObject);
        }
    }

    private static void EnsureARSceneSeed()
    {
        if (File.Exists(ArScenePath))
        {
            return;
        }

        if (!File.Exists(SourceScenePath))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ArScenePath);
            return;
        }

        File.Copy(SourceScenePath, ArScenePath);
        AssetDatabase.ImportAsset(ArScenePath);
    }

    private static GameObject EnsurePrefabInstance(string expectedName, string prefabPath)
    {
        GameObject existing = GameObject.Find(expectedName);
        if (existing != null)
        {
            return existing;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Meta prefab was not found at {prefabPath}. Add it manually from Meta Building Blocks if needed.");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            Debug.LogWarning($"Could not instantiate Meta prefab at {prefabPath}.");
            return null;
        }

        instance.name = expectedName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return instance;
    }

    private static void ConfigureCameraRig(GameObject cameraRig)
    {
        if (cameraRig == null)
        {
            return;
        }

        cameraRig.SetActive(true);

        OVRManager ovrManager = GetOrAdd<OVRManager>(cameraRig);
        ovrManager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
        ovrManager.isInsightPassthroughEnabled = true;
        EditorUtility.SetDirty(ovrManager);

        Camera centerEyeCamera = FindCenterEyeCamera(cameraRig);
        if (centerEyeCamera == null)
        {
            Debug.LogWarning("OVRCameraRig was created, but no center eye camera was found for LOTO ray input.");
            return;
        }

        DisableDesktopCameras(centerEyeCamera);

        centerEyeCamera.gameObject.tag = "MainCamera";
        centerEyeCamera.clearFlags = CameraClearFlags.SolidColor;
        centerEyeCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        centerEyeCamera.nearClipPlane = 0.05f;
        centerEyeCamera.farClipPlane = 100f;

        LOTORaycastInput raycastInput = GetOrAdd<LOTORaycastInput>(centerEyeCamera.gameObject);
        raycastInput.targetCamera = centerEyeCamera;
        raycastInput.maxDistance = 100f;
        raycastInput.interactionMask = ~0;
        raycastInput.blockWhenPointerOverUi = false;

        GetOrAdd<PhysicsRaycaster>(centerEyeCamera.gameObject);

        LOTOXRControllerRayInput controllerInput = GetOrAdd<LOTOXRControllerRayInput>(cameraRig);
        controllerInput.raycastInput = raycastInput;
        controllerInput.rightRayOrigin = FindChild(cameraRig.transform, "RightControllerAnchor") ?? FindChild(cameraRig.transform, "RightHandAnchor");
        controllerInput.leftRayOrigin = FindChild(cameraRig.transform, "LeftControllerAnchor") ?? FindChild(cameraRig.transform, "LeftHandAnchor");
        controllerInput.useRightController = true;
        controllerInput.useLeftController = true;
        controllerInput.debugLogs = true;
        controllerInput.drawDebugRays = true;

        EditorUtility.SetDirty(raycastInput);
        EditorUtility.SetDirty(controllerInput);
        EditorUtility.SetDirty(centerEyeCamera);
        EditorUtility.SetDirty(cameraRig);
    }

    private static Camera FindCenterEyeCamera(GameObject cameraRig)
    {
        Transform centerEye = FindChild(cameraRig.transform, "CenterEyeAnchor");
        Camera centerEyeCamera = centerEye != null ? centerEye.GetComponent<Camera>() : null;
        if (centerEyeCamera != null)
        {
            return centerEyeCamera;
        }

        return cameraRig.GetComponentInChildren<Camera>(true);
    }

    private static void DisableDesktopCameras(Camera arCamera)
    {
        foreach (Camera camera in FindSceneCameras())
        {
            if (camera == null || camera == arCamera)
            {
                continue;
            }

            if (camera.GetComponentInParent<OVRCameraRig>() != null)
            {
                continue;
            }

            camera.gameObject.tag = "Untagged";
            camera.gameObject.SetActive(false);
            EditorUtility.SetDirty(camera.gameObject);
        }
    }

    private static Camera[] FindSceneCameras()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<Camera>(true);
#endif
    }

    private static void ConfigurePassthrough(GameObject passthroughObject)
    {
        OVRPassthroughLayer passthroughLayer = passthroughObject != null
            ? passthroughObject.GetComponentInChildren<OVRPassthroughLayer>(true)
            : null;

        if (passthroughLayer == null)
        {
#if UNITY_2023_1_OR_NEWER
            passthroughLayer = Object.FindFirstObjectByType<OVRPassthroughLayer>(FindObjectsInactive.Include);
#else
            passthroughLayer = Object.FindObjectOfType<OVRPassthroughLayer>(true);
#endif
        }

        if (passthroughLayer == null)
        {
            if (passthroughObject == null)
            {
                passthroughObject = new GameObject("PassthroughUnderlay");
            }

            passthroughLayer = passthroughObject.AddComponent<OVRPassthroughLayer>();
        }

        passthroughObject = passthroughLayer.gameObject;
        passthroughObject.SetActive(true);
        passthroughLayer.enabled = true;
        passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
        passthroughLayer.textureOpacity = 1f;

        EditorUtility.SetDirty(passthroughLayer);
        EditorUtility.SetDirty(passthroughObject);
    }

    private static GameObject ConfigureMRPlacementRoot()
    {
        GameObject placementRoot = GameObject.Find("LOTO_MR_PlacementRoot");
        GameObject oldWorldRoot = GameObject.Find("LOTO_AR_WorldRoot");

        if (placementRoot == null && oldWorldRoot != null)
        {
            placementRoot = oldWorldRoot;
            placementRoot.name = "LOTO_MR_PlacementRoot";
        }
        else if (placementRoot != null && oldWorldRoot != null && oldWorldRoot != placementRoot)
        {
            MoveChildren(oldWorldRoot.transform, placementRoot.transform);
            Object.DestroyImmediate(oldWorldRoot);
        }

        if (placementRoot == null)
        {
            placementRoot = new GameObject("LOTO_MR_PlacementRoot");
            placementRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        string[] placedContentRoots =
        {
            "Generator_Model",
            "InteractionTargets",
            "Props",
            "UI"
        };

        foreach (string rootName in placedContentRoots)
        {
            GameObject root = GameObject.Find(rootName);
            if (root != null && root != placementRoot)
            {
                root.transform.SetParent(placementRoot.transform, true);
                EditorUtility.SetDirty(root);
            }
        }

        GameObject lotoManager = GameObject.Find("LOTO_Manager");
        if (lotoManager != null && lotoManager.transform.parent == placementRoot.transform)
        {
            lotoManager.transform.SetParent(null, true);
            EditorUtility.SetDirty(lotoManager);
        }

        EditorUtility.SetDirty(placementRoot);
        return placementRoot;
    }

    private static void MoveChildren(Transform source, Transform destination)
    {
        while (source.childCount > 0)
        {
            Transform child = source.GetChild(0);
            child.SetParent(destination, true);
            EditorUtility.SetDirty(child);
        }
    }

    private static void ConfigureMRPlacementController(GameObject placementRoot, GameObject cameraRig)
    {
        if (placementRoot == null)
        {
            return;
        }

        GameObject placementManager = GameObject.Find("MR_Placement_Manager");
        if (placementManager == null)
        {
            placementManager = new GameObject("MR_Placement_Manager");
            placementManager.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        Camera centerEyeCamera = cameraRig != null ? FindCenterEyeCamera(cameraRig) : Camera.main;
        LOTOMRPlacementController placementController = GetOrAdd<LOTOMRPlacementController>(placementManager);
        placementController.placementRoot = placementRoot.transform;
        placementController.headCamera = centerEyeCamera != null ? centerEyeCamera.transform : null;
        placementController.forwardDistance = 1.8f;
        placementController.rightOffset = 0.5f;
        placementController.floorY = 0f;
        placementController.faceUser = true;
        placementController.yawOffsetDegrees = 0f;
        placementController.rootScale = 1f;

        EditorUtility.SetDirty(placementController);
        EditorUtility.SetDirty(placementManager);
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = FindChild(parent.GetChild(i), childName);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void EnsureEventSystemObject()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void AddSceneToBuildSettings(string path)
    {
        EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene existingScene in existingScenes)
        {
            if (existingScene.path == path)
            {
                existingScene.enabled = true;
                EditorBuildSettings.scenes = existingScenes;
                return;
            }
        }

        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
        existingScenes.CopyTo(scenes, 0);
        scenes[existingScenes.Length] = new EditorBuildSettingsScene(path, true);
        EditorBuildSettings.scenes = scenes;
    }
}
