using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioLowPassFilter musicFilter;
    [SerializeField] private List<AudioClip> musicTracks;
    [SerializeField] private Slider ambientSlider;
    [SerializeField] private AudioSource ambientSource;
    // The game's background sound since 1.0.2: an ambient loop in place of the music tracks.
    // Made by Tools/Ambient. The loop is seamless, so it just repeats.
    [SerializeField] private AudioClip ambientLoop;
    private AudioLowPassFilter ambientFilter;

    // ---------------------------------------------------------------------------------------
    // The sound of a run (Tools/Ambient/run_layers.py). The ambient loop above is for the menus:
    // slow chords, right for standing still and too calm for a ball that is racing. During a run
    // it gives way to three layers that follow the ball's speed - still no music:
    //   bed      a dark drone, always there
    //   pulse    the same harmony breathing in time, with a low heartbeat: the pace. Louder as
    //            the ball speeds up
    //   shimmer  high and bright, silent at first, in from about a third of the way to top speed
    // All three are played a little faster and higher as the speed climbs (RUN_PITCH_AT_TOP_SPEED),
    // so the pace itself quickens; a bolt pushes that further. And each turn plays a soft pluck
    // (turnAudio), a step higher up a pentatonic scale every time.
    // All of it follows the Ambient slider, except the pluck, which follows Sound Effects.
    // ---------------------------------------------------------------------------------------
    [SerializeField] private AudioClip runBed;
    [SerializeField] private AudioClip runPulse;
    [SerializeField] private AudioClip runShimmer;
    [SerializeField] private AudioClip turnAudio;
    private AudioSource bedSource, pulseSource, shimmerSource, turnSource;
    private readonly List<AudioLowPassFilter> layerFilters = new List<AudioLowPassFilter>();
    private PlayerMovement player;
    private float ambientLevel = 1f;
    private float run;        // 0 in the menus .. 1 in a run
    private float intensity;  // 0 at the starting speed .. 1 at top speed
    private float runPitch = 1f;
    private const float RUN_FADE_PER_SECOND = 0.8f;
    private const float INTENSITY_PER_SECOND = 0.5f;
    private const float RUN_PITCH_AT_TOP_SPEED = 1.16f;
    private const float RUN_PITCH_IN_A_BOLT = 1.26f;
    // Minor pentatonic, in semitones above the pluck's own note, two octaves up and back down.
    private static readonly int[] TurnScale = { 0, 3, 5, 7, 10, 12, 15, 17, 19, 22, 24, 22, 19, 17, 15, 12, 10, 7, 5, 3 };
    private int turnStep;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource sfxDiamondSource;
    [SerializeField] private AudioClip diamondAudio;
    [SerializeField] private AudioClip chanceOnAudio;
    [SerializeField] private AudioClip chanceOffAudio;
    [SerializeField] private AudioClip doublePointsOnAudio;
    [SerializeField] private AudioClip doublePointsOffAudio;
    [SerializeField] private AudioClip superSpeedOnAudio;
    [SerializeField] private AudioSource superSpeedLoopSource;
    [SerializeField] private AudioClip superSpeedOffAudio;
    [SerializeField] private AudioClip boxAudio;
    [SerializeField] private Slider menusSlider;
    [SerializeField] private AudioSource menusSource;
    [SerializeField] private AudioClip backAudio;
    [SerializeField] private AudioClip menuAudio;

    private float targetFilterValue = 2000;
    private float targetPitchValue = 1;
    private float timeElapsedFilter = 0;
    private float timeElapsedPitch = 0;

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
    }

    void Start()
    {
        musicSource.volume = PlayerStats.Instance.getMusicLevel();
        ambientLevel = PlayerStats.Instance.getAmbientLevel();
        ambientSource.volume = ambientLevel;
        sfxSource.volume = PlayerStats.Instance.getSfxLevel();
        sfxDiamondSource.volume = PlayerStats.Instance.getSfxLevel();
        superSpeedLoopSource.volume = PlayerStats.Instance.getSfxLevel();
        menusSource.volume = PlayerStats.Instance.getMenusLevel();

        // The ambient loop is the background sound now, muffled the way the music was (the menus,
        // pause, a fall). The music is retired: the Settings row that was Music is the Ambient
        // slider now (the scene's own Ambient row was always hidden).
        ambientFilter = ambientSource.GetComponent<AudioLowPassFilter>();
        if (ambientFilter == null)
            ambientFilter = ambientSource.gameObject.AddComponent<AudioLowPassFilter>();
        ambientFilter.cutoffFrequency = targetFilterValue;
        if (ambientLoop != null)
        {
            ambientSource.clip = ambientLoop;
            ambientSource.loop = true;
            ambientSource.Play();
        }
        musicSource.Stop();

        // The run's layers: started together, so they stay in step; silent until a run begins.
        double together = AudioSettings.dspTime + 0.2;
        bedSource = addLayer("Run Bed", runBed, together);
        pulseSource = addLayer("Run Pulse", runPulse, together);
        shimmerSource = addLayer("Run Shimmer", runShimmer, together);
        turnSource = gameObject.AddComponent<AudioSource>();
        turnSource.playOnAwake = false;
        turnSource.volume = PlayerStats.Instance.getSfxLevel();

        musicSlider.value = musicSource.volume;
        ambientSlider.value = ambientLevel;
        sfxSlider.value = sfxSource.volume;
        menusSlider.value = menusSource.volume;

        musicSlider.onValueChanged.AddListener(value =>
        {
            musicSource.volume = value;
            PlayerStats.Instance.setMusicLevel(value);
        });
        ambientSlider.onValueChanged.AddListener(value =>
        {
            ambientLevel = value; // Update() shares it out between the menu loop and the run's layers
            PlayerStats.Instance.setAmbientLevel(value);
        });
        sfxSlider.onValueChanged.AddListener(value =>
        {
            sfxSource.volume = value;
            sfxDiamondSource.volume = value;
            superSpeedLoopSource.volume = value;
            turnSource.volume = value;
            PlayerStats.Instance.setSfxLevel(value);
        });
        menusSlider.onValueChanged.AddListener(value =>
        {
            menusSource.volume = value;
            PlayerStats.Instance.setMenusLevel(value);
        });
    }

    void Update()
    {
        if (!musicSource.isPlaying && musicTracks.Count > 0)
        {
            AudioClip tempClip = musicSource.clip;
            if (tempClip == null)
                tempClip = musicTracks[0];
            else
                while (tempClip == musicSource.clip)
                    tempClip = musicTracks[Random.Range(0, musicTracks.Count)];
            musicSource.clip = tempClip;
            musicSource.Play();
        }

        if (ambientFilter != null && ambientFilter.cutoffFrequency != targetFilterValue)
        {
            ambientFilter.cutoffFrequency = Mathf.Lerp(ambientFilter.cutoffFrequency, targetFilterValue, timeElapsedFilter / 15);
            timeElapsedFilter += Time.unscaledDeltaTime;
        }
        foreach (AudioLowPassFilter filter in layerFilters)
            filter.cutoffFrequency = ambientFilter.cutoffFrequency;
        mixRun();
        if (musicSource.pitch != targetPitchValue)
        {
            musicSource.pitch = Mathf.Lerp(musicSource.pitch, targetPitchValue, timeElapsedPitch / 15);
            timeElapsedPitch += Time.unscaledDeltaTime;
        }
    }

    private AudioSource addLayer(string name, AudioClip clip, double startAt)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        layerFilters.Add(go.AddComponent<AudioLowPassFilter>());
        if (clip != null)
            source.PlayScheduled(startAt);
        return source;
    }

    // Every frame: the menu loop or the run's layers, and how hard the run is going.
    private void mixRun()
    {
        if (bedSource == null)
            return;
        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();

        // A run is on while the ball is being played: not in the menus, not once it has fallen.
        bool inRun = Utility.gameStarted && Utility.camFollowPlayer;
        float dt = Time.unscaledDeltaTime;
        run = Mathf.MoveTowards(run, inRun ? 1f : 0f, RUN_FADE_PER_SECOND * dt);
        float wanted = 0f;
        if (inRun && player != null)
            wanted = Utility.boltIsOn ? 1f : Mathf.InverseLerp(Utility.Constants.START_PLAYER_SPEED, Utility.Constants.TOP_PLAYER_SPEED, player.speed);
        intensity = Mathf.MoveTowards(intensity, wanted, INTENSITY_PER_SECOND * dt);
        if (!Utility.gameStarted)
            turnStep = 0; // the next run's turns start from the bottom of the scale

        ambientSource.volume = ambientLevel * (1f - run);
        bedSource.volume = ambientLevel * run * 0.9f;
        pulseSource.volume = ambientLevel * run * Mathf.Lerp(0.5f, 1f, intensity);
        shimmerSource.volume = ambientLevel * run * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, intensity)) * 0.9f;

        float pitch = Mathf.Lerp(1f, RUN_PITCH_AT_TOP_SPEED, intensity);
        if (Utility.boltIsOn)
            pitch = RUN_PITCH_IN_A_BOLT;
        runPitch = Mathf.MoveTowards(runPitch, pitch, 0.25f * dt);
        bedSource.pitch = pulseSource.pitch = shimmerSource.pitch = Utility.isGamePaused ? 1f : runPitch;
    }

    /// <summary>The ball turned: a pluck, one step further along the scale than the last.</summary>
    public void PlayTurn()
    {
        if (turnAudio == null || turnSource == null)
            return;
        // In tune with the layers, which the run's speed has shifted up.
        turnSource.pitch = Mathf.Pow(2f, TurnScale[turnStep] / 12f) * runPitch;
        turnSource.PlayOneShot(turnAudio, 0.6f);
        turnStep = (turnStep + 1) % TurnScale.Length;
    }

    public void setFilter(float value = 2000)
    {
        targetFilterValue = value;
        timeElapsedFilter = 0;
    }

    public void removeFilter()
    {
        targetFilterValue = 22000.0f;
        timeElapsedFilter = 0;
    }

    public void setPitch(float value = 1.03f)
    {
        targetPitchValue = value;
        timeElapsedPitch = 0;
    }

    public void resetPitch()
    {
        targetPitchValue = 1;
        timeElapsedPitch = 0;
    }

    private IEnumerator sfxDiamondPitchCoroutine;
    public void increaseSfxPitch(float value = 0.01f)
    {
        if (sfxDiamondPitchCoroutine != null)
            StopCoroutine(sfxDiamondPitchCoroutine);
        sfxDiamondPitchCoroutine = sfxDiamondPitch(value);
        // StopAllCoroutines();
        StartCoroutine(sfxDiamondPitchCoroutine);
    }

    private IEnumerator sfxDiamondPitch(float value)
    {
        sfxDiamondSource.pitch += value;
        yield return new WaitForSeconds(0.65f);
        if (!Utility.boltIsOn)
            resetSfxDiamondPitch();
    }

    public void resetSfxDiamondPitch()
    {
        sfxDiamondSource.pitch = 1;
    }

    public float getSfxDiamondPitch()
    {
        return sfxDiamondSource.pitch;
    }

    public void PauseSounds()
    {
        sfxSource.Pause();
        superSpeedLoopSource.Pause();
        setFilter();
    }

    public void ResumeSounds()
    {
        sfxSource.UnPause();
        superSpeedLoopSource.UnPause();
        removeFilter();
    }

    public void StopSounds()
    {
        sfxSource.Stop();
        superSpeedLoopSource.Stop();
    }

    public void PlayDiamond() { sfxDiamondSource.PlayOneShot(diamondAudio); }
    public void PlayChanceOn() { sfxSource.PlayOneShot(chanceOnAudio); }
    public void PlayChanceOff() { sfxSource.PlayOneShot(chanceOffAudio); }
    public void PlayDoublePointsOn() { sfxSource.PlayOneShot(doublePointsOnAudio); }
    public void PlayDoublePointsOff() { sfxSource.PlayOneShot(doublePointsOffAudio); }
    public void PlayBoltOn() { sfxSource.PlayOneShot(superSpeedOnAudio); }
    public void PlayBoltOff() { sfxSource.PlayOneShot(superSpeedOffAudio); }
    public void PlayBox() { sfxSource.PlayOneShot(boxAudio); }

    // Mystery box reveal. Its own source so the ticks can rise in pitch without detuning the
    // menu clicks that share menusSource; it follows the Menus volume.
    private AudioSource revealSource;
    private AudioSource RevealSource()
    {
        if (revealSource == null)
        {
            revealSource = gameObject.AddComponent<AudioSource>();
            revealSource.playOnAwake = false;
        }
        revealSource.volume = menusSource.volume;
        return revealSource;
    }
    public void PlayRevealTick(float pitch)
    {
        AudioSource s = RevealSource();
        s.pitch = pitch;
        s.PlayOneShot(menuAudio, 0.8f);
    }
    public void PlayRevealWin(PrizeRarity rarity)
    {
        AudioSource s = RevealSource();
        s.pitch = 1f;
        s.PlayOneShot(boxAudio);
        if (rarity >= PrizeRarity.Epic)
            sfxSource.PlayOneShot(superSpeedOnAudio, 0.7f);
        else if (rarity == PrizeRarity.Rare)
            sfxSource.PlayOneShot(chanceOnAudio, 0.6f);
    }
    public void PlayRevealCollect() { sfxDiamondSource.PlayOneShot(diamondAudio); }
    // An upgrade bought: the box's chime, a little higher for each level.
    public void PlayUpgrade(int level)
    {
        AudioSource s = RevealSource();
        s.pitch = 0.92f + 0.07f * level;
        s.PlayOneShot(boxAudio);
        sfxSource.PlayOneShot(chanceOnAudio, 0.45f);
    }
    public void PlayBack() { menusSource.PlayOneShot(backAudio); }
    public void PlayMenu() { menusSource.PlayOneShot(menuAudio); }
}
