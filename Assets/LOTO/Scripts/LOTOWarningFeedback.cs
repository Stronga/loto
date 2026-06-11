using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class LOTOWarningFeedback : MonoBehaviour
{
    public GameObject warningPanel;
    public TextMeshProUGUI warningText;
    public AudioSource buzzerAudio;
    public Light warningLight;
    public float warningDuration = 2f;
    public float lightFlashInterval = 0.15f;
    public bool useGeneratedBuzzerIfMissing = true;
    public float generatedBuzzerFrequency = 720f;
    public float generatedBuzzerDuration = 0.35f;

    [Header("UI Toolkit")]
    public UIDocument uiDocument;
    public string warningPanelElement = "warning-panel";
    public string warningTextElement = "warning-message";

    private Coroutine warningRoutine;
    private bool originalLightState;
    private VisualElement toolkitWarningPanel;
    private Label toolkitWarningLabel;

    private void Awake()
    {
        if (warningPanel != null)
        {
            warningPanel.SetActive(false);
        }

        if (warningLight != null)
        {
            originalLightState = warningLight.enabled;
        }

        EnsureBuzzerAudio();
        CacheToolkitElements();
    }

    public void ShowWarning(string message)
    {
        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
        }

        if (warningPanel != null)
        {
            warningPanel.SetActive(true);
        }

        if (warningText != null)
        {
            warningText.text = message;
        }

        CacheToolkitElements();
        if (toolkitWarningPanel != null)
        {
            toolkitWarningPanel.style.display = DisplayStyle.Flex;
        }

        if (toolkitWarningLabel != null)
        {
            toolkitWarningLabel.text = message;
        }

        buzzerAudio?.Play();
        warningRoutine = StartCoroutine(HideWarningAfterDelay());
    }

    private void EnsureBuzzerAudio()
    {
        if (!useGeneratedBuzzerIfMissing)
        {
            return;
        }

        if (buzzerAudio == null)
        {
            buzzerAudio = GetComponent<AudioSource>();
        }

        if (buzzerAudio == null)
        {
            buzzerAudio = gameObject.AddComponent<AudioSource>();
        }

        if (buzzerAudio.clip == null)
        {
            buzzerAudio.clip = CreateBuzzerClip();
        }

        buzzerAudio.playOnAwake = false;
        buzzerAudio.spatialBlend = 0f;
    }

    private void CacheToolkitElements()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            return;
        }

        if (toolkitWarningPanel == null && !string.IsNullOrWhiteSpace(warningPanelElement))
        {
            toolkitWarningPanel = uiDocument.rootVisualElement.Q<VisualElement>(warningPanelElement);
        }

        if (toolkitWarningLabel == null && !string.IsNullOrWhiteSpace(warningTextElement))
        {
            toolkitWarningLabel = uiDocument.rootVisualElement.Q<Label>(warningTextElement);
        }

        if (toolkitWarningPanel != null)
        {
            toolkitWarningPanel.style.display = DisplayStyle.None;
        }
    }

    private AudioClip CreateBuzzerClip()
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * generatedBuzzerDuration));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * generatedBuzzerFrequency * t) * 0.35f * envelope;
        }

        AudioClip clip = AudioClip.Create("Generated_LOTO_Buzzer", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private IEnumerator HideWarningAfterDelay()
    {
        float elapsed = 0f;
        float flashTimer = 0f;

        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            flashTimer += Time.deltaTime;

            if (warningLight != null && flashTimer >= lightFlashInterval)
            {
                warningLight.enabled = !warningLight.enabled;
                flashTimer = 0f;
            }

            yield return null;
        }

        if (warningPanel != null)
        {
            warningPanel.SetActive(false);
        }

        if (toolkitWarningPanel != null)
        {
            toolkitWarningPanel.style.display = DisplayStyle.None;
        }

        if (warningLight != null)
        {
            warningLight.enabled = originalLightState;
        }

        warningRoutine = null;
    }
}
