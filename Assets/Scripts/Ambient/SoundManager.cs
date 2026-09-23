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
        ambientSource.volume = PlayerStats.Instance.getAmbientLevel();
        sfxSource.volume = PlayerStats.Instance.getSfxLevel();
        sfxDiamondSource.volume = PlayerStats.Instance.getSfxLevel();
        superSpeedLoopSource.volume = PlayerStats.Instance.getSfxLevel();
        menusSource.volume = PlayerStats.Instance.getMenusLevel();

        musicSlider.value = musicSource.volume;
        ambientSlider.value = ambientSource.volume;
        sfxSlider.value = sfxSource.volume;
        menusSlider.value = menusSource.volume;

        musicSlider.onValueChanged.AddListener(value =>
        {
            musicSource.volume = value;
            PlayerStats.Instance.setMusicLevel(value);
        });
        ambientSlider.onValueChanged.AddListener(value =>
        {
            ambientSource.volume = value;
            PlayerStats.Instance.setAmbientLevel(value);
        });
        sfxSlider.onValueChanged.AddListener(value =>
        {
            sfxSource.volume = value;
            sfxDiamondSource.volume = value;
            superSpeedLoopSource.volume = value;
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

        if (musicFilter.cutoffFrequency != targetFilterValue)
        {
            musicFilter.cutoffFrequency = Mathf.Lerp(musicFilter.cutoffFrequency, targetFilterValue, timeElapsedFilter / 15);
            timeElapsedFilter += Time.unscaledDeltaTime;
        }
        if (musicSource.pitch != targetPitchValue)
        {
            musicSource.pitch = Mathf.Lerp(musicSource.pitch, targetPitchValue, timeElapsedPitch / 15);
            timeElapsedPitch += Time.unscaledDeltaTime;
        }
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
    public void PlayBack() { menusSource.PlayOneShot(backAudio); }
    public void PlayMenu() { menusSource.PlayOneShot(menuAudio); }
}
