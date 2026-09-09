using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReviveMenu : MonoBehaviour
{
    [SerializeField] private Slider continueSlider;
    private PlayerFall playerFall;
    private AdmobManager adsManager;

    void Awake()
    {
        playerFall = FindObjectOfType<PlayerFall>();
        adsManager = FindObjectOfType<AdmobManager>();
    }

    void OnEnable()
    {
        continueSlider.value = 0.2f;
    }

    void FixedUpdate()
    {
        if (continueSlider.value < 1)
            continueSlider.value = Mathf.Lerp(continueSlider.value, 1, Time.deltaTime);
        if (continueSlider.value > 0.997f && continueSlider.value < 1)
        {
            CancelRevive();
            continueSlider.value = 1f;
        }
    }

    public void ShowReviveAd()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        adsManager.ShowReviveAd(() => StartCoroutine(cancelReviveAfterDelay()));
    }

    public void RevivePlayer()
    {
        playerFall.showInGameUi();
        playerFall.startRespawn();
        playerFall.numberOfRevives++;
    }

    public void CancelRevive()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        playerFall.endGame();
    }

    private IEnumerator cancelReviveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        CancelRevive();
    }
}
