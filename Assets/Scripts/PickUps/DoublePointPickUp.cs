using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoublePointPickUp : MonoBehaviour
{
    [SerializeField] private ParticleSystem doublePointEffect;
    [SerializeField] private ParticleSystem doublePointNameEffect;
    [SerializeField] private ParticleSystem shineEffect;
    [SerializeField] private MeshRenderer doublePointMesh;
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
            // if (MenuCont.volumeIsOn)
            //     myAudio.PlayOneShot(diamondSound, 0.85F);
            // ParticleSystem ps = transform.Find("DiamondEffect").GetComponent<ParticleSystem>();
            // ps.transform.position = other.transform.position + new Vector3(0, .5f, 0);
            doublePointEffect.Play();
            doublePointNameEffect.Play();
            doublePointMesh.enabled = false;
            shineEffect.gameObject.SetActive(false);
            partsPool.setPartAfterTime(transform, doublePointEffect.main.startLifetimeMultiplier);

            pickUpsManager.activateDoublePoint();
        }
    }

    public void reset()
    {
        gameObject.SetActive(true);
        shineEffect.gameObject.SetActive(true);
        doublePointMesh.enabled = true;
        name = "PickUp";
    }
}
