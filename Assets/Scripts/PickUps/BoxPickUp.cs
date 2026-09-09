using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxPickUp : MonoBehaviour
{
    [SerializeField] private ParticleSystem boxEffect;
    [SerializeField] private ParticleSystem boxNameEffect;
    [SerializeField] private ParticleSystem shineEffect;
    [SerializeField] private MeshRenderer boxMesh;
    private ScoreManager scoreManager;
    private PartsPool partsPool;
    private PickUpsManager pickUpsManager;
    private PlayerMovement playerMovement;

    void Awake()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        partsPool = FindObjectOfType<PartsPool>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        playerMovement = FindObjectOfType<PlayerMovement>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // if (MenuCont.volumeIsOn)
            //     myAudio.PlayOneShot(diamondSound, 0.85F);
            // ParticleSystem ps = transform.Find("DiamondEffect").GetComponent<ParticleSystem>();
            // ps.transform.position = other.transform.position + new Vector3(0, .5f, 0);
            boxEffect.Play();
            boxNameEffect.Play();
            boxMesh.enabled = false;
            shineEffect.gameObject.SetActive(false);
            partsPool.setPartAfterTime(transform, boxNameEffect.main.startLifetimeMultiplier);
            pickUpsManager.boxPickedUp();
        }
    }

    public void reset()
    {
        gameObject.SetActive(true);
        shineEffect.gameObject.SetActive(true);
        boxMesh.enabled = true;
        name = "PickUp";
    }
}
