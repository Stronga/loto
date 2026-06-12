using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LOTOChecklistUI : MonoBehaviour
{
    [Header("Optional State Source")]
    public LOTOStateController stateController;
    public bool showLockTagSteps = true;

    [Header("Checklist Text")]
    public TextMeshProUGUI openSwitchBoxText;
    public TextMeshProUGUI turnPowerOffText;
    public TextMeshProUGUI waitForShutdownText;
    public TextMeshProUGUI closeSwitchBoxText;
    public TextMeshProUGUI applyLockText;
    public TextMeshProUGUI applyWarningTagText;
    public TextMeshProUGUI openServiceDoorText;

    [Header("UI Toolkit")]
    public UIDocument uiDocument;
    public UIDocument introDocument;
    public UIDocument checklistDocument;
    public UIDocument successDocument;
    public StyleSheet styleSheet;
    public VisualTreeAsset introVisualTree;
    public VisualTreeAsset checklistVisualTree;
    public VisualTreeAsset successVisualTree;
    public bool useSeparateToolkitDocuments;
    public bool showIntroOnStart = true;
    public bool lockInputUntilStarted = true;

    [Header("XR UI Ray Interaction")]
    public Vector2 introPanelSizeMeters = new Vector2(0.95f, 0.75f);
    public Vector2 checklistPanelSizeMeters = new Vector2(0.82f, 0.9f);
    public Vector2 successPanelSizeMeters = new Vector2(1.25f, 0.65f);
    public bool debugUiRayHits;

    [Header("UI Toolkit Elements")]
    public string openSwitchBoxElement = "step-open-switch-box";
    public string turnPowerOffElement = "step-turn-power-off";
    public string waitForShutdownElement = "step-wait-shutdown";
    public string closeSwitchBoxElement = "step-close-switch-box";
    public string applyLockElement = "step-apply-lock";
    public string applyWarningTagElement = "step-apply-tag";
    public string openServiceDoorElement = "step-open-service-door";
    public string openSwitchBoxRow = "row-open-switch-box";
    public string turnPowerOffRow = "row-turn-power-off";
    public string waitForShutdownRow = "row-wait-shutdown";
    public string closeSwitchBoxRow = "row-close-switch-box";
    public string applyLockRow = "row-apply-lock";
    public string applyWarningTagRow = "row-apply-tag";
    public string openServiceDoorRow = "row-open-service-door";
    public string progressFillElement = "progress-fill";
    public string startButtonElement = "start-process-button";
    public string hintButtonElement = "hint-button";
    public string restartButtonElement = "restart-button";
    public string successRestartButtonElement = "success-restart-button";
    public string warningPanelElement = "warning-panel";

    private const string PendingPrefix = "\u2610 ";
    private const string DonePrefix = "\u2713 ";
    private const string CompleteSymbol = "\u2713";
    private const string CurrentSymbol = "\u25cf";
    private const string PendingSymbol = "\u25cb";

    private enum ToolkitView
    {
        None,
        Intro,
        Checklist,
        Success
    }

    private ToolkitView currentToolkitView;
    private UIDocument activeToolkitDocument;
    private bool subscribedToState;

#if UNITY_EDITOR
    private const string EditorIntroUxmlPath = "Assets/LOTO/UI/introUI.uxml";
    private const string EditorChecklistUxmlPath = "Assets/LOTO/UI/lotonewui.uxml";
    private const string EditorSuccessUxmlPath = "Assets/LOTO/UI/sucessui.uxml";
    private const string EditorChecklistUssPath = "Assets/LOTO/UI/lotonewuiuss.uss";

    private void Reset()
    {
        AutoAssignToolkitAssetsInEditor();
    }

    private void OnValidate()
    {
        AutoAssignToolkitAssetsInEditor();
    }
#endif

    private void OnEnable()
    {
        ResolveStateController();
        SubscribeToState();
        EnsureToolkitStyle();
    }

    private void Start()
    {
        ResolveStateController();
        SubscribeToState();

        if (GetDocumentForView(ToolkitView.Intro) != null && showIntroOnStart && introVisualTree != null)
        {
            ShowIntro();
            return;
        }

        ShowChecklist(false);
    }

    private void OnDisable()
    {
        UnsubscribeFromState();
    }

    public void UpdateChecklist(LOTOStateController state)
    {
        if (state == null)
        {
            return;
        }

        SetItem(openSwitchBoxText, state.switchBoxOpened, "Open switch box");
        SetItem(turnPowerOffText, state.powerHandleOff, "Turn power OFF");
        SetItem(waitForShutdownText, state.shutdownComplete, "Wait for shutdown");
        SetItem(closeSwitchBoxText, state.switchBoxClosed, "Close switch box");
        SetOptionalItem(applyLockText, showLockTagSteps, state.lockApplied, "Apply lock");
        SetOptionalItem(applyWarningTagText, showLockTagSteps, state.tagApplied, "Apply warning tag");
        SetItem(openServiceDoorText, state.mainDoorOpened, "Open service door");

        UpdateToolkitChecklist(state);
    }

    public void ShowIntro()
    {
        BuildToolkitView(introVisualTree, ToolkitView.Intro);
        SetInputLocked(lockInputUntilStarted);

        Button startButton = GetButton(startButtonElement);
        if (startButton != null)
        {
            startButton.clicked += StartProcess;
        }
    }

    public void ShowChecklist(bool resetTraining)
    {
        BuildToolkitView(checklistVisualTree, ToolkitView.Checklist);
        SetInputLocked(false);
        BindChecklistButtons();

        if (resetTraining && stateController != null)
        {
            stateController.ResetTraining();
        }
        else if (stateController != null)
        {
            UpdateChecklist(stateController);
        }
    }

    public void ShowSuccess()
    {
        BuildToolkitView(successVisualTree, ToolkitView.Success);
        SetInputLocked(true);

        Button restartButton = GetButton(successRestartButtonElement);
        if (restartButton != null)
        {
            restartButton.clicked += RestartTraining;
        }
    }

    public bool TryTriggerToolkitAtRay(Ray ray, float maxDistance, out Vector3 hitPoint, out float hitDistance, out string targetName)
    {
        targetName = string.Empty;

        if (!TryPickToolkitButtonAtRay(ray, maxDistance, out Button button, out hitPoint, out hitDistance, out VisualElement pickedElement))
        {
            return false;
        }

        targetName = button != null ? button.name : pickedElement != null ? pickedElement.name : string.Empty;

        if (button == null)
        {
            LogUiRay($"LOTO UI ray consumed hit on '{targetName}' without triggering a button.");
            return true;
        }

        if (button.name == startButtonElement)
        {
            LogUiRay("LOTO UI ray selected Start.");
            StartProcess();
            return true;
        }

        if (button.name == hintButtonElement)
        {
            LogUiRay("LOTO UI ray selected Hint.");
            RevealHint();
            return true;
        }

        if (button.name == restartButtonElement || button.name == successRestartButtonElement)
        {
            LogUiRay($"LOTO UI ray selected Restart from '{button.name}'.");
            RestartTraining();
            return true;
        }

        LogUiRay($"LOTO UI ray hit unhandled button '{button.name}'.");
        return true;
    }

    public bool TryGetToolkitRayHit(Ray ray, float maxDistance, out Vector3 hitPoint, out float hitDistance, out string targetName, out bool actionable)
    {
        actionable = false;
        targetName = string.Empty;

        bool hit = TryPickToolkitButtonAtRay(ray, maxDistance, out Button button, out hitPoint, out hitDistance, out VisualElement pickedElement);
        if (!hit)
        {
            return false;
        }

        targetName = button != null ? button.name : pickedElement != null ? pickedElement.name : string.Empty;
        actionable = button != null && IsKnownToolkitButton(button.name);
        return true;
    }

    private void StartProcess()
    {
        ShowChecklist(true);
    }

    private void RestartTraining()
    {
        ShowChecklist(true);
    }

    private void RevealHint()
    {
        ResolveStateController();
        stateController?.RevealCurrentHint();
    }

    private void HandleStateChanged()
    {
        if (stateController == null)
        {
            return;
        }

        if (currentToolkitView == ToolkitView.Intro)
        {
            return;
        }

        if (stateController.mainDoorOpened)
        {
            if (currentToolkitView != ToolkitView.Success)
            {
                ShowSuccess();
            }

            return;
        }

        if (currentToolkitView != ToolkitView.Checklist)
        {
            ShowChecklist(false);
            return;
        }

        UpdateChecklist(stateController);
    }

    private void BuildToolkitView(VisualTreeAsset visualTreeAsset, ToolkitView view)
    {
        UIDocument document = GetDocumentForView(view);
        SetToolkitDocumentVisibility(document);

        if (document == null || document.rootVisualElement == null)
        {
            activeToolkitDocument = null;
            currentToolkitView = ToolkitView.None;
            return;
        }

        if (visualTreeAsset != null)
        {
            document.rootVisualElement.Clear();
            visualTreeAsset.CloneTree(document.rootVisualElement);
        }

        activeToolkitDocument = document;
        currentToolkitView = view;
        EnsureToolkitStyle(document);
        HideWarningPanel();
    }

    private bool TryPickToolkitButtonAtRay(
        Ray ray,
        float maxDistance,
        out Button button,
        out Vector3 hitPoint,
        out float hitDistance,
        out VisualElement pickedElement)
    {
        button = null;
        pickedElement = null;
        hitPoint = Vector3.zero;
        hitDistance = 0f;

        UIDocument document = activeToolkitDocument != null
            ? activeToolkitDocument
            : GetDocumentForView(currentToolkitView);

        if (document == null || !document.enabled || document.rootVisualElement == null)
        {
            return false;
        }

        if (!TryRayToDocumentPanelPoint(document, ray, maxDistance, out Vector2 panelPoint, out hitPoint, out hitDistance))
        {
            return false;
        }

        button = FindButtonContainingPoint(document.rootVisualElement, panelPoint);
        pickedElement = button;

        if (debugUiRayHits)
        {
            string pickedName = pickedElement != null ? pickedElement.name : "<none>";
            string buttonName = button != null ? button.name : "<none>";
            Debug.Log($"LOTO UI ray hit panel point {panelPoint}, picked '{pickedName}', button '{buttonName}', distance {hitDistance:0.00}.");
        }

        return true;
    }

    private bool TryRayToDocumentPanelPoint(
        UIDocument document,
        Ray ray,
        float maxDistance,
        out Vector2 panelPoint,
        out Vector3 hitPoint,
        out float hitDistance)
    {
        panelPoint = Vector2.zero;
        hitPoint = Vector3.zero;
        hitDistance = 0f;

        Transform documentTransform = document.transform;
        Vector3 normal = documentTransform.forward;
        float denominator = Vector3.Dot(ray.direction, normal);
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return false;
        }

        float distance = Vector3.Dot(documentTransform.position - ray.origin, normal) / denominator;
        if (distance < 0f || distance > maxDistance)
        {
            return false;
        }

        Vector3 worldPoint = ray.origin + ray.direction * distance;
        Vector3 localPoint = documentTransform.InverseTransformPoint(worldPoint);
        Vector2 panelSizeMeters = GetPanelSizeMetersForView(currentToolkitView);
        if (panelSizeMeters.x <= 0.001f || panelSizeMeters.y <= 0.001f)
        {
            return false;
        }

        float normalizedX = Mathf.InverseLerp(-panelSizeMeters.x * 0.5f, panelSizeMeters.x * 0.5f, localPoint.x);
        float normalizedY = Mathf.InverseLerp(panelSizeMeters.y * 0.5f, -panelSizeMeters.y * 0.5f, localPoint.y);
        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
        {
            return false;
        }

        VisualElement contentRoot = GetPanelContentRoot(document.rootVisualElement);
        Rect contentBounds = contentRoot != null ? contentRoot.worldBound : document.rootVisualElement.worldBound;
        if (contentBounds.width <= 0.01f || contentBounds.height <= 0.01f)
        {
            return false;
        }

        panelPoint = new Vector2(
            Mathf.Lerp(contentBounds.xMin, contentBounds.xMax, normalizedX),
            Mathf.Lerp(contentBounds.yMin, contentBounds.yMax, normalizedY));
        hitPoint = worldPoint;
        hitDistance = distance;
        return true;
    }

    private Vector2 GetPanelSizeMetersForView(ToolkitView view)
    {
        switch (view)
        {
            case ToolkitView.Intro:
                return introPanelSizeMeters;
            case ToolkitView.Checklist:
                return checklistPanelSizeMeters;
            case ToolkitView.Success:
                return successPanelSizeMeters;
            default:
                return checklistPanelSizeMeters;
        }
    }

    private static VisualElement GetPanelContentRoot(VisualElement root)
    {
        if (root == null)
        {
            return null;
        }

        return root.childCount > 0 ? root[0] : root;
    }

    private static Button FindButtonContainingPoint(VisualElement root, Vector2 panelPoint)
    {
        Button foundButton = null;
        FindButtonContainingPoint(root, panelPoint, ref foundButton);
        return foundButton;
    }

    private static void FindButtonContainingPoint(VisualElement element, Vector2 panelPoint, ref Button foundButton)
    {
        if (element == null || foundButton != null)
        {
            return;
        }

        for (int i = element.childCount - 1; i >= 0; i--)
        {
            FindButtonContainingPoint(element[i], panelPoint, ref foundButton);
            if (foundButton != null)
            {
                return;
            }
        }

        if (element is Button button && button.worldBound.Contains(panelPoint))
        {
            foundButton = button;
        }
    }

    private bool IsKnownToolkitButton(string buttonName)
    {
        return buttonName == startButtonElement
            || buttonName == hintButtonElement
            || buttonName == restartButtonElement
            || buttonName == successRestartButtonElement;
    }

    private void LogUiRay(string message)
    {
        if (debugUiRayHits)
        {
            Debug.Log(message);
        }
    }

    private UIDocument GetDocumentForView(ToolkitView view)
    {
        if (useSeparateToolkitDocuments)
        {
            switch (view)
            {
                case ToolkitView.Intro:
                    return introDocument != null ? introDocument : uiDocument;
                case ToolkitView.Checklist:
                    return checklistDocument != null ? checklistDocument : uiDocument;
                case ToolkitView.Success:
                    return successDocument != null ? successDocument : uiDocument;
            }
        }

        return uiDocument != null ? uiDocument : checklistDocument;
    }

    private void SetToolkitDocumentVisibility(UIDocument activeDocument)
    {
        if (!useSeparateToolkitDocuments)
        {
            if (activeDocument != null)
            {
                activeDocument.enabled = true;
            }

            return;
        }

        SetDocumentEnabled(introDocument, activeDocument);
        SetDocumentEnabled(checklistDocument, activeDocument);
        SetDocumentEnabled(successDocument, activeDocument);

        if (activeDocument != null)
        {
            activeDocument.enabled = true;
        }
    }

    private static void SetDocumentEnabled(UIDocument document, UIDocument activeDocument)
    {
        if (document == null)
        {
            return;
        }

        document.enabled = document == activeDocument;
    }

    private void BindChecklistButtons()
    {
        Button hintButton = GetButton(hintButtonElement);
        if (hintButton != null)
        {
            hintButton.clicked += RevealHint;
        }

        Button restartButton = GetButton(restartButtonElement);
        if (restartButton != null)
        {
            restartButton.clicked += RestartTraining;
        }
    }

    private void UpdateToolkitChecklist(LOTOStateController state)
    {
        UIDocument document = activeToolkitDocument != null
            ? activeToolkitDocument
            : GetDocumentForView(currentToolkitView);

        if (document == null || document.rootVisualElement == null)
        {
            return;
        }

        bool openCurrent = !state.switchBoxOpened;
        bool powerCurrent = state.switchBoxOpened && !state.switchBoxClosed && !state.powerHandleOff;
        bool waitCurrent = state.powerHandleOff && !state.shutdownComplete;
        bool closeCurrent = state.switchBoxOpened && state.powerHandleOff && state.shutdownComplete && !state.switchBoxClosed;
        bool lockCurrent = state.CanApplyLock;
        bool tagCurrent = state.CanApplyTag;
        bool doorCurrent = state.CanOpenMainDoor && !state.mainDoorOpened;

        SetToolkitStep(openSwitchBoxRow, openSwitchBoxElement, state.switchBoxOpened, openCurrent, "Open switch box");
        SetToolkitStep(turnPowerOffRow, turnPowerOffElement, state.powerHandleOff, powerCurrent, "Turn power OFF");
        SetToolkitStep(waitForShutdownRow, waitForShutdownElement, state.shutdownComplete, waitCurrent, "Wait for shutdown");
        SetToolkitStep(closeSwitchBoxRow, closeSwitchBoxElement, state.switchBoxClosed, closeCurrent, "Close switch box");
        SetOptionalToolkitStep(applyLockRow, applyLockElement, showLockTagSteps, state.lockApplied, lockCurrent, "Apply lock");
        SetOptionalToolkitStep(applyWarningTagRow, applyWarningTagElement, showLockTagSteps, state.tagApplied, tagCurrent, "Apply warning tag");
        SetToolkitStep(openServiceDoorRow, openServiceDoorElement, state.mainDoorOpened, doorCurrent, "Open service door");

        VisualElement progressFill = document.rootVisualElement.Q<VisualElement>(progressFillElement);
        if (progressFill != null)
        {
            progressFill.style.width = Length.Percent(GetCompletionPercent(state));
        }
    }

    private void SetToolkitStep(string rowName, string labelName, bool complete, bool current, string label)
    {
        Label targetLabel = GetLabel(labelName);
        if (targetLabel != null)
        {
            targetLabel.text = label;
        }

        VisualElement row = GetElement(rowName);
        if (row == null)
        {
            SetLegacyToolkitLabel(labelName, complete, label);
            return;
        }

        row.style.display = DisplayStyle.Flex;
        row.EnableInClassList("step-complete", complete);
        row.EnableInClassList("step-current", !complete && current);
        row.EnableInClassList("step-warning", false);

        Label stateLabel = row.Q<Label>(className: "step-state");
        if (stateLabel != null)
        {
            stateLabel.text = complete ? CompleteSymbol : current ? CurrentSymbol : PendingSymbol;
            stateLabel.EnableInClassList("complete-state", complete);
        }
    }

    private void SetOptionalToolkitStep(string rowName, string labelName, bool visible, bool complete, bool current, string label)
    {
        VisualElement row = GetElement(rowName);
        if (row != null)
        {
            row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        Label targetLabel = GetLabel(labelName);
        if (targetLabel != null)
        {
            targetLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (visible)
        {
            SetToolkitStep(rowName, labelName, complete, current, label);
        }
    }

    private void SetLegacyToolkitLabel(string elementName, bool complete, string label)
    {
        Label targetLabel = GetLabel(elementName);
        if (targetLabel == null)
        {
            return;
        }

        targetLabel.text = (complete ? DonePrefix : PendingPrefix) + label;
        targetLabel.EnableInClassList("step-complete", complete);
        targetLabel.style.display = DisplayStyle.Flex;
    }

    private float GetCompletionPercent(LOTOStateController state)
    {
        int total = showLockTagSteps ? 7 : 5;
        int complete = 0;

        if (state.switchBoxOpened) complete++;
        if (state.powerHandleOff) complete++;
        if (state.shutdownComplete) complete++;
        if (state.switchBoxClosed) complete++;
        if (showLockTagSteps && state.lockApplied) complete++;
        if (showLockTagSteps && state.tagApplied) complete++;
        if (state.mainDoorOpened) complete++;

        return total <= 0 ? 0f : Mathf.Clamp01((float)complete / total) * 100f;
    }

    private static void SetItem(TextMeshProUGUI targetText, bool complete, string label)
    {
        if (targetText != null)
        {
            targetText.text = (complete ? DonePrefix : PendingPrefix) + label;
        }
    }

    private static void SetOptionalItem(TextMeshProUGUI targetText, bool visible, bool complete, string label)
    {
        if (targetText == null)
        {
            return;
        }

        targetText.gameObject.SetActive(visible);
        if (visible)
        {
            SetItem(targetText, complete, label);
        }
    }

    private VisualElement GetElement(string elementName)
    {
        UIDocument document = activeToolkitDocument != null
            ? activeToolkitDocument
            : GetDocumentForView(currentToolkitView);

        if (document == null || document.rootVisualElement == null || string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        return document.rootVisualElement.Q<VisualElement>(elementName);
    }

    private Label GetLabel(string elementName)
    {
        UIDocument document = activeToolkitDocument != null
            ? activeToolkitDocument
            : GetDocumentForView(currentToolkitView);

        if (document == null || document.rootVisualElement == null || string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        return document.rootVisualElement.Q<Label>(elementName);
    }

    private Button GetButton(string elementName)
    {
        UIDocument document = activeToolkitDocument != null
            ? activeToolkitDocument
            : GetDocumentForView(currentToolkitView);

        if (document == null || document.rootVisualElement == null || string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        return document.rootVisualElement.Q<Button>(elementName);
    }

    private void HideWarningPanel()
    {
        VisualElement warningPanel = GetElement(warningPanelElement);
        if (warningPanel != null)
        {
            warningPanel.style.display = DisplayStyle.None;
        }
    }

    private void EnsureToolkitStyle()
    {
        EnsureToolkitStyle(activeToolkitDocument);
        EnsureToolkitStyle(introDocument);
        EnsureToolkitStyle(checklistDocument);
        EnsureToolkitStyle(successDocument);
        EnsureToolkitStyle(uiDocument);
    }

    private void EnsureToolkitStyle(UIDocument document)
    {
        if (document == null || styleSheet == null || document.rootVisualElement == null)
        {
            return;
        }

        if (!document.rootVisualElement.styleSheets.Contains(styleSheet))
        {
            document.rootVisualElement.styleSheets.Add(styleSheet);
        }
    }

    private void ResolveStateController()
    {
        if (stateController != null)
        {
            return;
        }

        if (LOTOStateController.Active != null)
        {
            stateController = LOTOStateController.Active;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        stateController = FindFirstObjectByType<LOTOStateController>();
#else
        stateController = FindObjectOfType<LOTOStateController>();
#endif
    }

    private void SubscribeToState()
    {
        if (subscribedToState || stateController == null)
        {
            return;
        }

        if (stateController.stateChanged == null)
        {
            stateController.stateChanged = new UnityEvent();
        }

        stateController.stateChanged.AddListener(HandleStateChanged);
        subscribedToState = true;
    }

    private void UnsubscribeFromState()
    {
        if (!subscribedToState || stateController == null)
        {
            return;
        }

        stateController.stateChanged.RemoveListener(HandleStateChanged);
        subscribedToState = false;
    }

    private void SetInputLocked(bool locked)
    {
        ResolveStateController();
        stateController?.SetInputLocked(locked);
    }

#if UNITY_EDITOR
    private void AutoAssignToolkitAssetsInEditor()
    {
        if (introVisualTree == null)
        {
            introVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorIntroUxmlPath);
        }

        if (checklistVisualTree == null)
        {
            checklistVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorChecklistUxmlPath);
        }

        if (successVisualTree == null)
        {
            successVisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorSuccessUxmlPath);
        }

        if (styleSheet == null)
        {
            styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(EditorChecklistUssPath);
        }

        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (introDocument == null && uiDocument != null && uiDocument.visualTreeAsset == introVisualTree)
        {
            introDocument = uiDocument;
        }

        if (checklistDocument == null && uiDocument != null && uiDocument.visualTreeAsset == checklistVisualTree)
        {
            checklistDocument = uiDocument;
        }

        if (successDocument == null && uiDocument != null && uiDocument.visualTreeAsset == successVisualTree)
        {
            successDocument = uiDocument;
        }

        if (uiDocument == null)
        {
            uiDocument = FindFirstObjectByType<UIDocument>(FindObjectsInactive.Include);
        }

        if (stateController == null)
        {
            stateController = GetComponent<LOTOStateController>();
        }

        if (stateController == null)
        {
            stateController = FindFirstObjectByType<LOTOStateController>(FindObjectsInactive.Include);
        }
    }
#endif
}
