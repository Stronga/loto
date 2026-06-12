using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class LOTOARSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/loto.unity";
    private const string ArScenePath = "Assets/Scenes/LOTO_AR.unity";
    private const string CameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
    private const string PassthroughPrefabPath = "Packages/com.meta.xr.sdk.core/Editor/BuildingBlocks/BlockData/Passthrough/Prefabs/PassthroughUnderlay.prefab";
    private const string EnvironmentRaycastPrefabPath = "Packages/com.meta.xr.mrutilitykit/Editor/BuildingBlocks/InstantContentPlacement/Prefabs/EnvironmentRaycastManager.prefab";
    private const string GeneratorLoopClipPath = "Assets/LOTO/audio/Generator loop Sound.wav";
    private const string GeneratorShutdownClipPath = "Assets/LOTO/audio/Generator, Shutting Down .wav";
    private const string IntroUxmlPath = "Assets/LOTO/UI/introUI.uxml";
    private const string ChecklistUxmlPath = "Assets/LOTO/UI/lotonewui.uxml";
    private const string SuccessUxmlPath = "Assets/LOTO/UI/sucessui.uxml";
    private const string ChecklistUssPath = "Assets/LOTO/UI/lotonewuiuss.uss";
    private const string HudPanelSettingsPath = "Assets/LOTO/UI/LOTO_HUD_PanelSettings.asset";

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
        ConfigureAudioController(placementRoot);
        ConfigureNewToolkitUi();
        CleanupMetaInteractionComponents();
        ConfigureFallbackControllerInput(cameraRig, true);
        EnsureEnvironmentRaycastManager();
        EnsureEventSystemObject();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ArScenePath);
        AddSceneToBuildSettings(ArScenePath);

        Selection.activeObject = placementRoot != null ? placementRoot : cameraRig;
        Debug.Log($"Created Meta MR LOTO scene at {ArScenePath}. The generator content is placed once at runtime under LOTO_MR_PlacementRoot.");
    }

    [MenuItem("LOTO/Configure AR UI Only")]
    public static void ConfigureARUiOnly()
    {
        ConfigureNewToolkitUi();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (!string.IsNullOrWhiteSpace(activeScene.path))
            {
                EditorSceneManager.SaveScene(activeScene);
            }
        }

        Debug.Log("Configured separate tag-along LOTO AR UI documents for the open scene.");
    }

    private static void CleanupDeprecatedARObjects()
    {
        DestroySceneObject("OVRHandPrefabBuildingBlock");
        DestroySceneObject("UnityXRComprehensiveInteractionRig");
        DestroySceneObject("Controllers");
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
            "Props"
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

        DetachUiObjectFromPlacementRoot("LOTO_UIDocument", placementRoot.transform);
        DetachUiObjectFromPlacementRoot("LOTO_UIRoot", placementRoot.transform);
        DetachUiObjectFromPlacementRoot("LOTO_Intro_UIDocument", placementRoot.transform);
        DetachUiObjectFromPlacementRoot("LOTO_Checklist_UIDocument", placementRoot.transform);
        DetachUiObjectFromPlacementRoot("LOTO_Success_UIDocument", placementRoot.transform);

        EditorUtility.SetDirty(placementRoot);
        return placementRoot;
    }

    private static void DetachUiObjectFromPlacementRoot(string objectName, Transform placementRoot)
    {
        GameObject uiObject = GameObject.Find(objectName);
        if (uiObject != null && uiObject.transform.IsChildOf(placementRoot))
        {
            uiObject.transform.SetParent(null, true);
            EditorUtility.SetDirty(uiObject);
        }
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
        placementController.forwardDistance = 3f;
        placementController.rightOffset = 0.5f;
        placementController.floorY = 0f;
        placementController.faceUser = true;
        placementController.modelYawCorrectionDegrees = 0f;
        placementController.rootScale = 1f;
        placementController.snapToFloor = true;
        placementController.floorRayStartHeight = 2f;
        placementController.floorRayDistance = 5f;
        placementController.floorRaycastMask = ~0;
        placementController.floorYOffset = 0f;
        placementController.usePhysicsFloorFallback = true;

        EditorUtility.SetDirty(placementController);
        EditorUtility.SetDirty(placementManager);
    }

    private static void ConfigureAudioController(GameObject placementRoot)
    {
        GameObject lotoManager = GameObject.Find("LOTO_Manager");
        if (lotoManager == null)
        {
            lotoManager = new GameObject("LOTO_Manager");
            lotoManager.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        LOTOAudioController audioController = GetOrAdd<LOTOAudioController>(lotoManager);
        GameObject generatorModel = GameObject.Find("Generator_Model");
        audioController.audioEmitter = generatorModel != null
            ? generatorModel.transform
            : placementRoot != null ? placementRoot.transform : lotoManager.transform;
        audioController.generatorLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GeneratorLoopClipPath);
        audioController.generatorShutdownClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GeneratorShutdownClipPath);
        audioController.playGeneratorLoopOnStart = true;
        audioController.defaultVolume = 1f;
        audioController.spatialBlend = 1f;
        audioController.spatialize = true;
        audioController.rolloffMode = AudioRolloffMode.Linear;
        audioController.minDistance = 0.5f;
        audioController.maxDistance = 12f;

#if UNITY_2023_1_OR_NEWER
        LOTOStateController stateController = Object.FindFirstObjectByType<LOTOStateController>(FindObjectsInactive.Include);
#else
        LOTOStateController stateController = Object.FindObjectOfType<LOTOStateController>(true);
#endif

        if (stateController != null)
        {
            stateController.audioController = audioController;
            EditorUtility.SetDirty(stateController);
        }

        EditorUtility.SetDirty(audioController);
        EditorUtility.SetDirty(lotoManager);
    }

    private static void ConfigureNewToolkitUi()
    {
#if UNITY_2023_1_OR_NEWER
        LOTOChecklistUI checklistUI = Object.FindFirstObjectByType<LOTOChecklistUI>(FindObjectsInactive.Include);
        LOTOWarningFeedback warningFeedback = Object.FindFirstObjectByType<LOTOWarningFeedback>(FindObjectsInactive.Include);
#else
        LOTOChecklistUI checklistUI = Object.FindObjectOfType<LOTOChecklistUI>(true);
        LOTOWarningFeedback warningFeedback = Object.FindObjectOfType<LOTOWarningFeedback>(true);
#endif

        if (checklistUI == null)
        {
            return;
        }

        VisualTreeAsset introVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(IntroUxmlPath);
        VisualTreeAsset checklistVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ChecklistUxmlPath);
        VisualTreeAsset successVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SuccessUxmlPath);
        StyleSheet checklistStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(ChecklistUssPath);
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelSettingsPath);
        Transform headCamera = ResolveHeadCameraTransform();

        GameObject uiRoot = GameObject.Find("LOTO_UIRoot");
        if (uiRoot == null)
        {
            uiRoot = new GameObject("LOTO_UIRoot");
            uiRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        GameObject placementRoot = GameObject.Find("LOTO_MR_PlacementRoot");
        if (placementRoot != null)
        {
            DetachUiObjectFromPlacementRoot("LOTO_UIDocument", placementRoot.transform);
            DetachUiObjectFromPlacementRoot("LOTO_UIRoot", placementRoot.transform);
            DetachUiObjectFromPlacementRoot("LOTO_Intro_UIDocument", placementRoot.transform);
            DetachUiObjectFromPlacementRoot("LOTO_Checklist_UIDocument", placementRoot.transform);
            DetachUiObjectFromPlacementRoot("LOTO_Success_UIDocument", placementRoot.transform);
        }

        UIDocument introDocument = GetOrCreateToolkitDocument(
            "LOTO_Intro_UIDocument",
            "LOTO_UIDocument",
            uiRoot.transform,
            panelSettings,
            introVisualTree,
            headCamera,
            new Vector3(0f, -0.06f, 1.55f),
            0f);

        UIDocument checklistDocument = GetOrCreateToolkitDocument(
            "LOTO_Checklist_UIDocument",
            null,
            uiRoot.transform,
            panelSettings,
            checklistVisualTree,
            headCamera,
            new Vector3(0.78f, -0.08f, 1.35f),
            0f);

        UIDocument successDocument = GetOrCreateToolkitDocument(
            "LOTO_Success_UIDocument",
            null,
            uiRoot.transform,
            panelSettings,
            successVisualTree,
            headCamera,
            new Vector3(0f, -0.06f, 1.6f),
            0f);

        checklistUI.uiDocument = introDocument != null ? introDocument : checklistDocument;
        checklistUI.introDocument = introDocument;
        checklistUI.checklistDocument = checklistDocument;
        checklistUI.successDocument = successDocument;
        checklistUI.styleSheet = checklistStyle;
        checklistUI.introVisualTree = introVisualTree;
        checklistUI.checklistVisualTree = checklistVisualTree;
        checklistUI.successVisualTree = successVisualTree;
        checklistUI.useSeparateToolkitDocuments = true;
        checklistUI.showIntroOnStart = introVisualTree != null;
        checklistUI.lockInputUntilStarted = introVisualTree != null;
        checklistUI.introPanelSizeMeters = new Vector2(0.95f, 0.75f);
        checklistUI.checklistPanelSizeMeters = new Vector2(0.82f, 0.9f);
        checklistUI.successPanelSizeMeters = new Vector2(1.25f, 0.65f);

        if (warningFeedback != null)
        {
            warningFeedback.uiDocument = checklistDocument != null ? checklistDocument : checklistUI.uiDocument;
            warningFeedback.warningPanel = null;
            warningFeedback.warningText = null;
            EditorUtility.SetDirty(warningFeedback);
        }

        SetInitialToolkitDocumentState(introDocument, introVisualTree != null);
        SetInitialToolkitDocumentState(checklistDocument, introVisualTree == null);
        SetInitialToolkitDocumentState(successDocument, false);

        EditorUtility.SetDirty(uiRoot);
        EditorUtility.SetDirty(checklistUI);
    }

    private static UIDocument GetOrCreateToolkitDocument(
        string objectName,
        string fallbackObjectName,
        Transform parent,
        PanelSettings panelSettings,
        VisualTreeAsset visualTreeAsset,
        Transform headCamera,
        Vector3 headSpaceOffset,
        float yawOffsetDegrees)
    {
        GameObject uiObject = GameObject.Find(objectName);
        if (uiObject == null && !string.IsNullOrWhiteSpace(fallbackObjectName))
        {
            uiObject = GameObject.Find(fallbackObjectName);
            if (uiObject != null)
            {
                uiObject.name = objectName;
            }
        }

        if (uiObject == null)
        {
            uiObject = new GameObject(objectName);
        }

        uiObject.transform.SetParent(parent, true);
        uiObject.transform.localScale = Vector3.one;
        UIDocument document = GetOrAdd<UIDocument>(uiObject);
        document.panelSettings = panelSettings;
        document.visualTreeAsset = visualTreeAsset;

        LOTOYawFollowUI tagAlong = GetOrAdd<LOTOYawFollowUI>(uiObject);
        tagAlong.headCamera = headCamera;
        tagAlong.headSpaceOffset = headSpaceOffset;
        tagAlong.yawOffsetDegrees = yawOffsetDegrees;
        tagAlong.positionFollowSpeed = 3.5f;
        tagAlong.yawFollowSpeed = 3.5f;
        tagAlong.yawDeadZoneDegrees = 4f;
        tagAlong.followPosition = true;
        tagAlong.followYaw = true;
        tagAlong.snapOnEnable = true;

        EditorUtility.SetDirty(document);
        EditorUtility.SetDirty(tagAlong);
        EditorUtility.SetDirty(uiObject);
        return document;
    }

    private static void SetInitialToolkitDocumentState(UIDocument document, bool enabled)
    {
        if (document == null)
        {
            return;
        }

        document.enabled = enabled;
        EditorUtility.SetDirty(document);
    }

    private static Transform ResolveHeadCameraTransform()
    {
        GameObject cameraRig = GameObject.Find("OVRCameraRig");
        Camera centerEyeCamera = cameraRig != null ? FindCenterEyeCamera(cameraRig) : Camera.main;
        return centerEyeCamera != null ? centerEyeCamera.transform : null;
    }

    private static void ConfigureFallbackControllerInput(GameObject cameraRig, bool enableFallback)
    {
        if (cameraRig == null)
        {
            return;
        }

        LOTOXRControllerRayInput controllerInput = cameraRig.GetComponent<LOTOXRControllerRayInput>();
        if (controllerInput == null)
        {
            return;
        }

        controllerInput.enableFallbackTriggerInput = enableFallback;
        controllerInput.enableVisibleRay = enableFallback;
        controllerInput.drawDebugRays = enableFallback;
        controllerInput.disableWhenMetaInteractionRigPresent = false;
        controllerInput.enableRayGrabSnapObjects = true;
        controllerInput.enableToolkitUiInteraction = true;
#if UNITY_2023_1_OR_NEWER
        controllerInput.checklistUI = Object.FindFirstObjectByType<LOTOChecklistUI>(FindObjectsInactive.Include);
#else
        controllerInput.checklistUI = Object.FindObjectOfType<LOTOChecklistUI>(true);
#endif
        controllerInput.enabled = enableFallback;
        EditorUtility.SetDirty(controllerInput);
    }

    private static void CleanupMetaInteractionComponents()
    {
        string[] targetNames =
        {
            "SwitchBoxClickTarget",
            "PowerHandleClickTarget",
            "Padlock",
            "WarningTag",
            "MainDoorClickTarget"
        };

        string[] componentTypeNames =
        {
            "Oculus.Interaction.RayInteractable",
            "Oculus.Interaction.Surfaces.ColliderSurface",
            "Oculus.Interaction.InteractableUnityEventWrapper",
            "Oculus.Interaction.Grabbable",
            "Oculus.Interaction.GrabInteractable",
            "Oculus.Interaction.DistanceGrabInteractable",
            "Oculus.Interaction.MoveTowardsTargetProvider",
            "Oculus.Interaction.GrabFreeTransformer",
            "LOTOMetaRaySelectBridge",
            "LOTOMetaGrabSnapBridge",
            "LOTOInteractionEventBridge"
        };

        foreach (string targetName in targetNames)
        {
            GameObject target = GameObject.Find(targetName);
            if (target != null)
            {
                RemoveComponentsByTypeName(target, componentTypeNames);
            }
        }
    }

    private static void RemoveComponentsByTypeName(GameObject target, string[] typeNames)
    {
        Component[] components = target.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            string componentTypeName = component.GetType().FullName;
            foreach (string typeName in typeNames)
            {
                if (componentTypeName == typeName)
                {
                    Object.DestroyImmediate(component);
                    break;
                }
            }
        }
    }

    private static void EnsureEnvironmentRaycastManager()
    {
        if (GameObject.Find("EnvironmentRaycastManager") != null)
        {
            return;
        }

        GameObject manager = EnsurePrefabInstance("EnvironmentRaycastManager", EnvironmentRaycastPrefabPath);
        if (manager == null)
        {
            Debug.LogWarning("Meta EnvironmentRaycastManager prefab was not found. LOTOMRPlacementController will use physics floor raycast and floorY fallback.");
        }
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
