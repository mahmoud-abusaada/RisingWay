using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChancePickUp : MonoBehaviour
{
    [SerializeField] private ParticleSystem chanceEffect;
    [SerializeField] private ParticleSystem chanceNameEffect;
    [SerializeField] private ParticleSystem shineEffect;
    [SerializeField] private MeshRenderer chanceMesh;
    private ScoreManager scoreManager;
    private PartsPool partsPool;
    private PickUpsManager pickUpsManager;

    void Start()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        partsPool = FindObjectOfType<PartsPool>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // GetComponent<AudioSource>().Play();
            // if (MenuCont.volumeIsOn)
            //     myAudio.PlayOneShot(diamondSound, 0.85F);
            // ParticleSystem ps = transform.Find("DiamondEffect").GetComponent<ParticleSystem>();
            // ps.transform.position = other.transform.position + new Vector3(0, .5f, 0);
            chanceEffect.Play();
            chanceNameEffect.Play();
            chanceMesh.enabled = false;
            shineEffect.gameObject.SetActive(false);
            partsPool.setPartAfterTime(transform, chanceEffect.main.startLifetimeMultiplier);

            pickUpsManager.activateChance();
        }
    }

    public void reset()
    {
        gameObject.SetActive(true);
        shineEffect.gameObject.SetActive(true);
        chanceMesh.enabled = true;
        name = "PickUp";
    }
}
