using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GraphicsManager : MonoBehaviour
{
    public static GraphicsManager Instance;
    [SerializeField] private UniversalRenderPipelineAsset urpAsset;
    [SerializeField] private Slider densitySlider;
    [SerializeField] private Toggle maxFpsToggle;
    [SerializeField] private Toggle hdrToggle;
    [SerializeField] private Toggle showFpsToggle;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        QualitySettings.vSyncCount = 0;
    }

    // Start is called before the first frame update
    void Start()
    {
        float densityLevel = PlayerStats.Instance.getDensityLevel();
        urpAsset.renderScale = densityLevel;
        densitySlider.value = densityLevel;
        densitySlider.onValueChanged.AddListener(value =>
        {
            PlayerStats.Instance.setDensityLevel(value);
            urpAsset.renderScale = value;
        });

        bool maxFpsIsOn = PlayerStats.Instance.getFramesLimit() > 0;
        applyFrames(maxFpsIsOn);
        maxFpsToggle.isOn = maxFpsIsOn;
        maxFpsToggle.onValueChanged.AddListener(value =>
        {
            PlayerStats.Instance.setFramesLimit(value ? 1 : 0);
            applyFrames(value);
        });

        bool hdrIsOn = PlayerStats.Instance.isEmissionOn();
        urpAsset.supportsHDR = hdrIsOn;
        hdrToggle.isOn = hdrIsOn;
        hdrToggle.onValueChanged.AddListener(value =>
        {
            PlayerStats.Instance.setEmissionState(value);
            urpAsset.supportsHDR = value;
        });

        // The FPS counter used to be on for everyone, release builds included.
        ShowFPS fpsCounter = FindAnyObjectByType<ShowFPS>(FindObjectsInactive.Include);
        bool showFpsIsOn = PlayerStats.Instance.isShowFpsOn();
        if (fpsCounter != null)
            fpsCounter.SetVisible(showFpsIsOn);
        if (showFpsToggle != null)
        {
            showFpsToggle.isOn = showFpsIsOn;
            showFpsToggle.onValueChanged.AddListener(value =>
            {
                PlayerStats.Instance.setShowFpsState(value);
                if (fpsCounter != null)
                    fpsCounter.SetVisible(value);
            });
        }
    }

    private void applyFrames(bool maxIsOn)
    {
        if (maxIsOn)
            Application.targetFrameRate = 1000; // Screen.currentResolution.refreshRate
        else
            Application.targetFrameRate = Screen.currentResolution.refreshRate > 60 ? 60 : 30;
    }
}
