using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

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
    public StyleSheet styleSheet;
    public string openSwitchBoxElement = "step-open-switch-box";
    public string turnPowerOffElement = "step-turn-power-off";
    public string waitForShutdownElement = "step-wait-shutdown";
    public string closeSwitchBoxElement = "step-close-switch-box";
    public string applyLockElement = "step-apply-lock";
    public string applyWarningTagElement = "step-apply-tag";
    public string openServiceDoorElement = "step-open-service-door";

    private const string PendingPrefix = "\u2610 ";
    private const string DonePrefix = "\u2713 ";

    private void OnEnable()
    {
        EnsureToolkitStyle();
    }

    private void Start()
    {
        EnsureToolkitStyle();

        if (stateController != null)
        {
            UpdateChecklist(stateController);
        }
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

        SetToolkitItem(openSwitchBoxElement, state.switchBoxOpened, "Open switch box");
        SetToolkitItem(turnPowerOffElement, state.powerHandleOff, "Turn power OFF");
        SetToolkitItem(waitForShutdownElement, state.shutdownComplete, "Wait for shutdown");
        SetToolkitItem(closeSwitchBoxElement, state.switchBoxClosed, "Close switch box");
        SetOptionalToolkitItem(applyLockElement, showLockTagSteps, state.lockApplied, "Apply lock");
        SetOptionalToolkitItem(applyWarningTagElement, showLockTagSteps, state.tagApplied, "Apply warning tag");
        SetToolkitItem(openServiceDoorElement, state.mainDoorOpened, "Open service door");
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

    private void SetToolkitItem(string elementName, bool complete, string label)
    {
        EnsureToolkitStyle();

        Label targetLabel = GetLabel(elementName);
        if (targetLabel == null)
        {
            return;
        }

        targetLabel.text = (complete ? DonePrefix : PendingPrefix) + label;
        targetLabel.EnableInClassList("step-complete", complete);
        targetLabel.style.display = DisplayStyle.Flex;
    }

    private void SetOptionalToolkitItem(string elementName, bool visible, bool complete, string label)
    {
        EnsureToolkitStyle();

        Label targetLabel = GetLabel(elementName);
        if (targetLabel == null)
        {
            return;
        }

        targetLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible)
        {
            targetLabel.text = (complete ? DonePrefix : PendingPrefix) + label;
            targetLabel.EnableInClassList("step-complete", complete);
        }
    }

    private Label GetLabel(string elementName)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null || string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        return uiDocument.rootVisualElement.Q<Label>(elementName);
    }

    private void EnsureToolkitStyle()
    {
        if (uiDocument == null || styleSheet == null || uiDocument.rootVisualElement == null)
        {
            return;
        }

        if (!uiDocument.rootVisualElement.styleSheets.Contains(styleSheet))
        {
            uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
        }
    }
}
