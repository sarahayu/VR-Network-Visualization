using UnityEngine;
using UnityEngine.InputSystem;
using Whisper.Utils;

namespace Whisper.Samples
{
/// <summary>
/// Displays microphone volume using segmented colored bars.
/// Inactive bars are grey; active bars use their original colors.
/// </summary>
public class MicrophoneAudioVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MicrophoneRecord microphoneRecord;
    [Tooltip("Quest controller button action that activates the meter while held.")]
    [SerializeField] private InputActionReference activationAction;
    [SerializeField] private Renderer[] bars;

    [Header("Volume Range")]
    [Tooltip("Sound at or below this level lights no bars.")]
    [SerializeField] private float minimumDecibels = -55f;

    [Tooltip("Sound at or above this level lights every bar.")]
    [SerializeField] private float maximumDecibels = -20f;

    [Header("Appearance")]
    [SerializeField] private Color inactiveColor = Color.grey;

    [Tooltip("How quickly bars respond when the volume increases.")]
    [SerializeField] private float riseSpeed = 15f;

    [Tooltip("How quickly bars return to grey.")]
    [SerializeField] private float fallSpeed = 1f;

    [Tooltip("How long a microphone reading stays visible before the bars begin falling.")]
    [SerializeField] private float volumeHoldTime = 0.15f;

    private Color[] activeColors;
    private float displayedVolume;
    private float targetVolume;
    private float lastChunkTime;
    private bool isButtonHeld;
    private bool enabledActionHere;

    private void Awake()
    {
        InitializeBars();
    }

    private void OnEnable()
    {
        if (microphoneRecord != null)
            microphoneRecord.OnChunkReady += OnChunkReady;

        if (activationAction != null && activationAction.action != null)
        {
            activationAction.action.started += OnButtonPressed;
            activationAction.action.canceled += OnButtonReleased;

            // XR actions are often enabled by an Input Action Manager. Only
            // enable and later disable the action here if nobody else did.
            enabledActionHere = !activationAction.action.enabled;
            if (enabledActionHere)
                activationAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (microphoneRecord != null)
            microphoneRecord.OnChunkReady -= OnChunkReady;

        if (activationAction != null && activationAction.action != null)
        {
            activationAction.action.started -= OnButtonPressed;
            activationAction.action.canceled -= OnButtonReleased;

            if (enabledActionHere)
                activationAction.action.Disable();
        }

        enabledActionHere = false;
        StopVisualization();
    }

    private void Update()
    {
        if (!isButtonHeld)
            return;

        float speed = targetVolume > displayedVolume
            ? riseSpeed
            : fallSpeed;

        displayedVolume = Mathf.MoveTowards(
            displayedVolume,
            targetVolume,
            speed * Time.deltaTime);

        UpdateBarColors();

        // Hold each reading briefly so higher segments remain visible between
        // microphone chunks, then let the meter fall naturally.
        if (Time.time - lastChunkTime >= volumeHoldTime)
        {
            targetVolume = Mathf.MoveTowards(
                targetVolume,
                0f,
                fallSpeed * Time.deltaTime);
        }
    }

    private void OnChunkReady(AudioChunk chunk)
    {
        if (!isButtonHeld || chunk.Data == null || chunk.Data.Length == 0)
            return;

        float sumSquares = 0f;

        for (int i = 0; i < chunk.Data.Length; i++)
        {
            float sample = chunk.Data[i];
            sumSquares += sample * sample;
        }

        float rms = Mathf.Sqrt(sumSquares / chunk.Data.Length);

        // Prevent Log10(0), which would produce negative infinity.
        float decibels = 20f * Mathf.Log10(Mathf.Max(rms, 0.000001f));

        // Convert the selected decibel range to a value from 0 to 1.
        targetVolume = Mathf.InverseLerp(
            minimumDecibels,
            maximumDecibels,
            decibels);

        lastChunkTime = Time.time;
    }

    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        isButtonHeld = true;
    }

    private void OnButtonReleased(InputAction.CallbackContext context)
    {
        StopVisualization();
    }

    private void StopVisualization()
    {
        isButtonHeld = false;
        displayedVolume = 0f;
        targetVolume = 0f;
        lastChunkTime = 0f;
        SetAllBarsInactive();
    }

    private void InitializeBars()
    {
        int barCount = bars != null ? bars.Length : 0;
        activeColors = new Color[barCount];

        for (int i = 0; i < barCount; i++)
        {
            if (bars[i] == null)
                continue;

            // Store the color already assigned to the bar in Unity.
            activeColors[i] = bars[i].material.color;
            bars[i].material.color = inactiveColor;
        }
    }

    private void UpdateBarColors()
    {
        if (bars == null || bars.Length == 0)
            return;

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null)
                continue;

            // Keep the highest segment below 100% so it remains reachable for
            // any number of bars (for 12 bars, thresholds are 1/13 through 12/13).
            float threshold = (i + 1f) / (bars.Length + 1f);
            bool isActive = displayedVolume >= threshold;

            bars[i].material.color = isActive
                ? activeColors[i]
                : inactiveColor;
        }
    }

    private void SetAllBarsInactive()
    {
        if (bars == null)
            return;

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null)
                bars[i].material.color = inactiveColor;
        }
    }
}
}
