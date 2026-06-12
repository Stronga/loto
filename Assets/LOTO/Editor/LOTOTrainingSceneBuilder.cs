using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class LOTOTrainingSceneBuilder
{
    private const string GeneratorAssetPath = "Assets/FBX_inports/generator_unity_ar_ready.fbx";
    private const string ScenePath = "Assets/Scenes/loto.unity";
    private const string GeneratedScenePath = "Assets/Scenes/LOTO_Generator_Training.generated.unity";
    private const string AnimationFolder = "Assets/LOTO/Animation";
    private const string MaterialFolder = "Assets/LOTO/Materials";
    private const string UiFolder = "Assets/LOTO/UI";
    private const string ControllerPath = AnimationFolder + "/Generator_LOTO.controller";
    private const string HudUxmlPath = UiFolder + "/LOTOTrainingHUD.uxml";
    private const string HudUssPath = UiFolder + "/LOTOTrainingHUD.uss";
    private const string IntroUxmlPath = UiFolder + "/introUI.uxml";
    private const string ChecklistUxmlPath = UiFolder + "/lotonewui.uxml";
    private const string SuccessUxmlPath = UiFolder + "/sucessui.uxml";
    private const string ChecklistUssPath = UiFolder + "/lotonewuiuss.uss";
    private const string HudPanelSettingsPath = UiFolder + "/LOTO_HUD_PanelSettings.asset";

    private static readonly string[] BaseLayerClips =
    {
        "Door_Open",
        "Generator_Shutdown",
        "SwitchBox_Door_Unlock_And_Open",
        "MainPower_Handle_Toggle"
    };

    private const string CableClip = "Cable_Baked_Shutdown_Wiggle_BlendShapes";

    [MenuItem("LOTO/Create Training Scene")]
    public static void CreateTrainingScene()
    {
        EnsureProjectFolders();
        LOTOGeneratorFbxImportConfigurator.ConfigureGeneratorImport();

        AnimatorController animatorController = CreateOrUpdateAnimatorController();
        Material highlightMaterial = CreateMaterial("LOTO_Highlight", new Color(1f, 0.82f, 0.08f, 1f));
        Material padlockMaterial = CreateMaterial("LOTO_Padlock_Red", new Color(0.74f, 0.05f, 0.03f, 1f));
        Material tagMaterial = CreateMaterial("LOTO_Tag_White", new Color(1f, 0.96f, 0.72f, 1f));

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        string generatedScenePath = AssetDatabase.GenerateUniqueAssetPath(GeneratedScenePath);
        SceneAsset generatedSceneAsset = null;
        bool savedGeneratedScene = false;

        try
        {

        GameObject cameraObject = CreateCamera();
        EnsureSceneInput(cameraObject);
        GameObject managerObject = new GameObject("LOTO_Manager");
        LOTOStateController stateController = managerObject.AddComponent<LOTOStateController>();
        LOTOAnimationController animationController = managerObject.AddComponent<LOTOAnimationController>();
        LOTOChecklistUI checklistUI = managerObject.AddComponent<LOTOChecklistUI>();
        LOTOWarningFeedback warningFeedback = managerObject.AddComponent<LOTOWarningFeedback>();

        stateController.animationController = animationController;
        stateController.checklistUI = checklistUI;
        stateController.warningFeedback = warningFeedback;
        checklistUI.stateController = stateController;

        GameObject generatorRoot = new GameObject("Generator_Model");
        GameObject generatorInstance = InstantiateGenerator(generatorRoot.transform, animatorController);
        Animator generatorAnimator = generatorInstance != null ? generatorInstance.GetComponentInChildren<Animator>() : null;
        animationController.generatorAnimator = generatorAnimator;
        animationController.generatorLayer = 0;
        animationController.cableAnimator = generatorAnimator;
        animationController.cableLayer = 1;
        animationController.enabled = true;

        GameObject interactionRoot = new GameObject("InteractionTargets");
        GameObject propsRoot = new GameObject("Props");
        GameObject uiRoot = new GameObject("UI");

        GameObject lockSnapTarget = CreateSnapTarget("LockSnapTarget", interactionRoot.transform, new Vector3(-0.55f, 1.15f, -0.72f));
        GameObject tagSnapTarget = CreateSnapTarget("TagSnapTarget", interactionRoot.transform, new Vector3(-0.35f, 1.02f, -0.74f));

        LOTOHighlightTarget switchBoxHighlight = CreateClickTarget(
            "SwitchBoxClickTarget",
            interactionRoot.transform,
            new Vector3(-0.75f, 1.15f, -0.55f),
            new Vector3(0.42f, 0.52f, 0.18f),
            LOTOActionType.OpenSwitchBox,
            stateController,
            highlightMaterial);

        LOTOHighlightTarget powerHighlight = CreateClickTarget(
            "PowerHandleClickTarget",
            interactionRoot.transform,
            new Vector3(-0.75f, 1.05f, -0.78f),
            new Vector3(0.26f, 0.26f, 0.16f),
            LOTOActionType.TogglePowerHandle,
            stateController,
            highlightMaterial);

        LOTOHighlightTarget mainDoorHighlight = CreateClickTarget(
            "MainDoorClickTarget",
            interactionRoot.transform,
            new Vector3(0.35f, 1.02f, -0.82f),
            new Vector3(1.05f, 0.86f, 0.18f),
            LOTOActionType.TryOpenMainDoor,
            stateController,
            highlightMaterial);

        LOTOHighlightTarget lockHighlight = CreateProp(
            "Padlock",
            propsRoot.transform,
            PrimitiveType.Cube,
            new Vector3(-1.15f, 0.85f, -0.6f),
            new Vector3(0.16f, 0.22f, 0.06f),
            padlockMaterial,
            stateController,
            LOTOActionType.ApplyLock,
            lockSnapTarget.transform,
            highlightMaterial);

        LOTOHighlightTarget tagHighlight = CreateProp(
            "WarningTag",
            propsRoot.transform,
            PrimitiveType.Cube,
            new Vector3(-1.15f, 0.55f, -0.6f),
            new Vector3(0.22f, 0.3f, 0.025f),
            tagMaterial,
            stateController,
            LOTOActionType.ApplyTag,
            tagSnapTarget.transform,
            highlightMaterial);

        stateController.switchBoxHighlight = switchBoxHighlight;
        stateController.powerHandleHighlight = powerHighlight;
        stateController.lockHighlight = lockHighlight;
        stateController.tagHighlight = tagHighlight;
        stateController.mainDoorHighlight = mainDoorHighlight;

        CreateUIDocumentUi(uiRoot.transform, checklistUI, warningFeedback);
        CreateLighting();

        EditorSceneManager.SaveScene(scene, generatedScenePath);
        AddSceneToBuildSettings(generatedScenePath);
        generatedSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(generatedScenePath);
        savedGeneratedScene = true;

        Debug.Log($"Created generated LOTO training scene at {generatedScenePath}. The open working scene was left untouched.");
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (savedGeneratedScene && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        if (generatedSceneAsset != null)
        {
            Selection.activeObject = generatedSceneAsset;
            EditorGUIUtility.PingObject(generatedSceneAsset);
        }
    }

    [MenuItem("LOTO/Setup loto Scene")]
    public static void SetupLotoScene()
    {
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        }

        CompleteOpenSceneSetup();
    }

    [MenuItem("LOTO/Complete Open Scene Setup")]
    public static void CompleteOpenSceneSetup()
    {
        EnsureProjectFolders();
        LOTOGeneratorFbxImportConfigurator.ConfigureGeneratorImport();

        AnimatorController animatorController = CreateOrUpdateAnimatorController();
        Material highlightMaterial = CreateMaterial("LOTO_Highlight", new Color(1f, 0.82f, 0.08f, 1f));
        Material padlockMaterial = CreateMaterial("LOTO_Padlock_Red", new Color(0.74f, 0.05f, 0.03f, 1f));
        Material tagMaterial = CreateMaterial("LOTO_Tag_White", new Color(1f, 0.96f, 0.72f, 1f));

        GameObject cameraObject = Camera.main != null ? Camera.main.gameObject : CreateCamera();
        EnsureSceneInput(cameraObject);
        GameObject managerObject = GetOrCreateRoot("LOTO_Manager");
        LOTOStateController stateController = GetOrAdd<LOTOStateController>(managerObject);
        LOTOAnimationController animationController = GetOrAdd<LOTOAnimationController>(managerObject);
        LOTOChecklistUI checklistUI = GetOrAdd<LOTOChecklistUI>(managerObject);
        LOTOWarningFeedback warningFeedback = GetOrAdd<LOTOWarningFeedback>(managerObject);

        stateController.animationController = animationController;
        stateController.checklistUI = checklistUI;
        stateController.warningFeedback = warningFeedback;
        checklistUI.stateController = stateController;

        GameObject generatorRoot = GetOrCreateRoot("Generator_Model");
        Animator generatorAnimator = generatorRoot.GetComponentInChildren<Animator>();
        if (generatorAnimator == null)
        {
            GameObject generatorInstance = InstantiateGenerator(generatorRoot.transform, animatorController);
            generatorAnimator = generatorInstance != null ? generatorInstance.GetComponentInChildren<Animator>() : null;
        }
        else
        {
            generatorAnimator.runtimeAnimatorController = animatorController;
            generatorAnimator.applyRootMotion = false;
        }

        animationController.generatorAnimator = generatorAnimator;
        animationController.cableAnimator = generatorAnimator;
        animationController.generatorLayer = 0;
        animationController.cableLayer = 1;

        GameObject interactionRoot = GetOrCreateRoot("InteractionTargets");
        GameObject propsRoot = GetOrCreateRoot("Props");
        GameObject uiRoot = GetOrCreateRoot("UI");

        GameObject lockSnapTarget = FindOrCreateSnapTarget("LockSnapTarget", interactionRoot.transform, new Vector3(-0.55f, 1.15f, -0.72f));
        GameObject tagSnapTarget = FindOrCreateSnapTarget("TagSnapTarget", interactionRoot.transform, new Vector3(-0.35f, 1.02f, -0.74f));

        stateController.switchBoxHighlight = FindOrCreateClickTarget(
            "SwitchBoxClickTarget",
            interactionRoot.transform,
            new Vector3(-0.75f, 1.15f, -0.55f),
            new Vector3(0.42f, 0.52f, 0.18f),
            LOTOActionType.OpenSwitchBox,
            stateController,
            highlightMaterial);

        stateController.powerHandleHighlight = FindOrCreateClickTarget(
            "PowerHandleClickTarget",
            interactionRoot.transform,
            new Vector3(-0.75f, 1.05f, -0.78f),
            new Vector3(0.26f, 0.26f, 0.16f),
            LOTOActionType.TogglePowerHandle,
            stateController,
            highlightMaterial);

        stateController.mainDoorHighlight = FindOrCreateClickTarget(
            "MainDoorClickTarget",
            interactionRoot.transform,
            new Vector3(0.35f, 1.02f, -0.82f),
            new Vector3(1.05f, 0.86f, 0.18f),
            LOTOActionType.TryOpenMainDoor,
            stateController,
            highlightMaterial);

        stateController.lockHighlight = FindOrCreateProp(
            "Padlock",
            propsRoot.transform,
            PrimitiveType.Cube,
            new Vector3(-1.15f, 0.85f, -0.6f),
            new Vector3(0.16f, 0.22f, 0.06f),
            padlockMaterial,
            stateController,
            LOTOActionType.ApplyLock,
            lockSnapTarget.transform,
            highlightMaterial);

        stateController.tagHighlight = FindOrCreateProp(
            "WarningTag",
            propsRoot.transform,
            PrimitiveType.Cube,
            new Vector3(-1.15f, 0.55f, -0.6f),
            new Vector3(0.22f, 0.3f, 0.025f),
            tagMaterial,
            stateController,
            LOTOActionType.ApplyTag,
            tagSnapTarget.transform,
            highlightMaterial);

        CreateUIDocumentUi(uiRoot.transform, checklistUI, warningFeedback);

        if (GameObject.Find("Key Light") == null && GameObject.Find("Directional Light") == null)
        {
            CreateLighting();
        }

        string activeScenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrWhiteSpace(activeScenePath))
        {
            activeScenePath = ScenePath;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), activeScenePath);
        AddSceneToBuildSettings(activeScenePath);
        Selection.activeObject = managerObject;

        Debug.Log("Completed LOTO setup for the open scene. Adjust generated target positions to match the FBX parts.");
    }

    [MenuItem("LOTO/Create Animator Controller")]
    public static void CreateAnimatorControllerMenu()
    {
        CreateOrUpdateAnimatorController();
    }

    public static AnimatorController CreateOrUpdateAnimatorController()
    {
        EnsureProjectFolders();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        int baseLayerIndex = EnsureLayer(controller, "Base Layer");
        int cableLayerIndex = EnsureLayer(controller, "Cable");

        AnimatorState baseIdle = EnsureState(controller.layers[baseLayerIndex].stateMachine, "Idle", null, false);
        controller.layers[baseLayerIndex].stateMachine.defaultState = baseIdle;

        foreach (string clipName in BaseLayerClips)
        {
            EnsureState(controller.layers[baseLayerIndex].stateMachine, clipName, FindClip(clipName), true);
        }

        AnimatorControllerLayer cableLayer = controller.layers[cableLayerIndex];
        cableLayer.defaultWeight = 1f;
        AnimatorControllerLayer[] layers = controller.layers;
        layers[cableLayerIndex] = cableLayer;
        controller.layers = layers;

        AnimatorState cableIdle = EnsureState(controller.layers[cableLayerIndex].stateMachine, "Idle", null, false);
        controller.layers[cableLayerIndex].stateMachine.defaultState = cableIdle;
        EnsureState(controller.layers[cableLayerIndex].stateMachine, CableClip, FindClip(CableClip), true);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return controller;
    }

    private static void EnsureProjectFolders()
    {
        EnsureFolder("Assets/LOTO");
        EnsureFolder(AnimationFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(UiFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static int EnsureLayer(AnimatorController controller, string layerName)
    {
        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == layerName)
            {
                return i;
            }
        }

        controller.AddLayer(layerName);
        return controller.layers.Length - 1;
    }

    private static AnimatorState EnsureState(AnimatorStateMachine stateMachine, string stateName, Motion motion, bool warnIfMotionMissing)
    {
        AnimatorState existing = null;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                existing = childState.state;
                break;
            }
        }

        AnimatorState state = existing ?? stateMachine.AddState(stateName);
        state.motion = motion;
        state.writeDefaultValues = true;

        if (motion == null && warnIfMotionMissing)
        {
            Debug.LogWarning($"Animation clip '{stateName}' was not found under {GeneratorAssetPath}.");
        }

        return state;
    }

    private static AnimationClip FindClip(string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(GeneratorAssetPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 1.65f, -3.2f);
        cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.018f, 0.02f, 1f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 50f;

        cameraObject.AddComponent<AudioListener>();
        EnsureSceneInput(cameraObject);
        return cameraObject;
    }

    private static void EnsureSceneInput(GameObject cameraObject)
    {
        Camera targetCamera = cameraObject.GetComponent<Camera>();

        LOTORaycastInput raycastInput = GetOrAdd<LOTORaycastInput>(cameraObject);
        raycastInput.targetCamera = targetCamera;
        raycastInput.interactionMask = ~0;
        raycastInput.maxDistance = 100f;
        raycastInput.blockWhenPointerOverUi = false;

        GetOrAdd<LOTOEventSystemBootstrap>(cameraObject);
        GetOrAdd<PhysicsRaycaster>(cameraObject);
        EnsureEventSystemObject();

        EditorUtility.SetDirty(raycastInput);
        EditorUtility.SetDirty(cameraObject);
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

    private static GameObject InstantiateGenerator(Transform parent, RuntimeAnimatorController controller)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorAssetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Generator FBX was not found at {GeneratorAssetPath}.");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
        {
            return null;
        }

        instance.name = "generator_unity_ar_ready";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Animator animator = instance.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = instance.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        return instance;
    }

    private static GameObject CreateSnapTarget(string name, Transform parent, Vector3 position)
    {
        GameObject snapTarget = new GameObject(name);
        snapTarget.transform.SetParent(parent);
        snapTarget.transform.position = position;
        snapTarget.transform.rotation = Quaternion.identity;
        return snapTarget;
    }

    private static GameObject FindOrCreateSnapTarget(string name, Transform parent, Vector3 position)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            existing.transform.SetParent(parent);
            return existing;
        }

        return CreateSnapTarget(name, parent, position);
    }

    private static LOTOHighlightTarget CreateClickTarget(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        LOTOActionType actionType,
        LOTOStateController stateController,
        Material highlightMaterial)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.SetParent(parent);
        target.transform.position = position;
        target.transform.localScale = scale;

        Renderer renderer = target.GetComponent<Renderer>();
        renderer.enabled = false;

        LOTOClickable clickable = target.AddComponent<LOTOClickable>();
        clickable.stateController = stateController;
        clickable.actionType = actionType;

        LOTOHighlightTarget highlightTarget = target.AddComponent<LOTOHighlightTarget>();
        highlightTarget.highlightMaterial = highlightMaterial;
        highlightTarget.targetRenderers = new[] { renderer };
        return highlightTarget;
    }

    private static LOTOHighlightTarget FindOrCreateClickTarget(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        LOTOActionType actionType,
        LOTOStateController stateController,
        Material highlightMaterial)
    {
        GameObject existing = GameObject.Find(name);
        if (existing == null)
        {
            return CreateClickTarget(name, parent, position, scale, actionType, stateController, highlightMaterial);
        }

        existing.transform.SetParent(parent);
        if (existing.GetComponent<Collider>() == null)
        {
            BoxCollider collider = existing.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
        }

        LOTOClickable clickable = GetOrAdd<LOTOClickable>(existing);
        clickable.stateController = stateController;
        clickable.actionType = actionType;

        LOTOHighlightTarget highlightTarget = GetOrAdd<LOTOHighlightTarget>(existing);
        highlightTarget.highlightMaterial = highlightMaterial;
        if (highlightTarget.targetRenderers == null || highlightTarget.targetRenderers.Length == 0)
        {
            highlightTarget.targetRenderers = existing.GetComponentsInChildren<Renderer>();
        }

        return highlightTarget;
    }

    private static LOTOHighlightTarget CreateProp(
        string name,
        Transform parent,
        PrimitiveType primitiveType,
        Vector3 position,
        Vector3 scale,
        Material material,
        LOTOStateController stateController,
        LOTOActionType actionType,
        Transform snapTarget,
        Material highlightMaterial)
    {
        GameObject prop = GameObject.CreatePrimitive(primitiveType);
        prop.name = name;
        prop.transform.SetParent(parent);
        prop.transform.position = position;
        prop.transform.localScale = scale;

        Renderer renderer = prop.GetComponent<Renderer>();
        renderer.sharedMaterial = material;

        LOTOSnapObject snapObject = prop.AddComponent<LOTOSnapObject>();
        snapObject.stateController = stateController;
        snapObject.notifyAction = actionType;
        snapObject.snapTarget = snapTarget;

        return CreateOrUpdatePropIndicator(name, parent, position, scale, highlightMaterial);
    }

    private static LOTOHighlightTarget FindOrCreateProp(
        string name,
        Transform parent,
        PrimitiveType primitiveType,
        Vector3 position,
        Vector3 scale,
        Material material,
        LOTOStateController stateController,
        LOTOActionType actionType,
        Transform snapTarget,
        Material highlightMaterial)
    {
        GameObject existing = GameObject.Find(name);
        if (existing == null)
        {
            return CreateProp(name, parent, primitiveType, position, scale, material, stateController, actionType, snapTarget, highlightMaterial);
        }

        existing.transform.SetParent(parent);
        Renderer renderer = existing.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        LOTOSnapObject snapObject = GetOrAdd<LOTOSnapObject>(existing);
        snapObject.stateController = stateController;
        snapObject.notifyAction = actionType;
        snapObject.snapTarget = snapTarget;

        return CreateOrUpdatePropIndicator(name, parent, existing.transform.position, existing.transform.localScale, highlightMaterial);
    }

    private static LOTOHighlightTarget CreateOrUpdatePropIndicator(
        string propName,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Material highlightMaterial)
    {
        string indicatorName = propName + "Indicator";
        GameObject indicator = GameObject.Find(indicatorName);
        if (indicator == null)
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = indicatorName;
        }

        indicator.transform.SetParent(parent);
        indicator.transform.position = position;
        indicator.transform.localScale = scale * 1.3f;

        Collider collider = indicator.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        indicatorRenderer.enabled = false;

        LOTOHighlightTarget highlightTarget = GetOrAdd<LOTOHighlightTarget>(indicator);
        highlightTarget.highlightMaterial = highlightMaterial;
        highlightTarget.targetRenderers = new[] { indicatorRenderer };
        return highlightTarget;
    }

    private static UIDocument CreateUIDocumentUi(
        Transform parent,
        LOTOChecklistUI checklistUI,
        LOTOWarningFeedback warningFeedback)
    {
        PanelSettings panelSettings = GetOrCreatePanelSettings();
        VisualTreeAsset introVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(IntroUxmlPath);
        VisualTreeAsset checklistVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ChecklistUxmlPath);
        VisualTreeAsset successVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SuccessUxmlPath);
        VisualTreeAsset visualTreeAsset = introVisualTree != null
            ? introVisualTree
            : checklistVisualTree != null
                ? checklistVisualTree
                : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ChecklistUssPath);
        if (styleSheet == null)
        {
            styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(HudUssPath);
        }

        if (visualTreeAsset == null)
        {
            Debug.LogWarning($"UI Toolkit visual tree was not found at {HudUxmlPath}. Let Unity import assets, then run setup again.");
        }

        GameObject uiObject = GameObject.Find("LOTO_UIDocument");
        if (uiObject == null)
        {
            uiObject = new GameObject("LOTO_UIDocument");
        }

        uiObject.transform.SetParent(parent);
        uiObject.transform.localPosition = Vector3.zero;
        uiObject.transform.localRotation = Quaternion.identity;
        uiObject.transform.localScale = Vector3.one;

        UIDocument uiDocument = GetOrAdd<UIDocument>(uiObject);
        uiDocument.panelSettings = panelSettings;
        uiDocument.visualTreeAsset = visualTreeAsset;

        checklistUI.uiDocument = uiDocument;
        checklistUI.styleSheet = styleSheet;
        checklistUI.introVisualTree = introVisualTree;
        checklistUI.checklistVisualTree = checklistVisualTree != null ? checklistVisualTree : visualTreeAsset;
        checklistUI.successVisualTree = successVisualTree;
        checklistUI.showIntroOnStart = introVisualTree != null;
        checklistUI.lockInputUntilStarted = introVisualTree != null;
        warningFeedback.uiDocument = uiDocument;
        warningFeedback.warningPanel = null;
        warningFeedback.warningText = null;

        EditorUtility.SetDirty(uiDocument);
        EditorUtility.SetDirty(checklistUI);
        EditorUtility.SetDirty(warningFeedback);
        return uiDocument;
    }

    private static PanelSettings GetOrCreatePanelSettings()
    {
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelSettingsPath);
        if (panelSettings != null)
        {
            return panelSettings;
        }

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        AssetDatabase.CreateAsset(panelSettings, HudPanelSettingsPath);
        AssetDatabase.SaveAssets();
        return panelSettings;
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Key Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightObject.transform.rotation = Quaternion.Euler(46f, -32f, 0f);
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
        scenes[scenes.Length - 1] = new EditorBuildSettingsScene(path, true);
        EditorBuildSettings.scenes = scenes;
    }

    private static GameObject GetOrCreateRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        return existing != null ? existing : new GameObject(name);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
