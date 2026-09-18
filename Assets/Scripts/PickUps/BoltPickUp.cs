using System.Collections;
using System.Collections.Generic;
using CartoonFX;
using UnityEngine;

public class BoltPickUp : MonoBehaviour
{
    [SerializeField] private ParticleSystem boltEffect;
    [SerializeField] private ParticleSystem boltNameEffect;
    [SerializeField] private ParticleSystem shineEffect;
    [SerializeField] private MeshRenderer boltMesh;
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
            boltEffect.GetComponent<CFXR_Effect>().enabled = true;
            boltEffect.Play();
            boltNameEffect.Play();
            boltMesh.enabled = false;
            shineEffect.gameObject.SetActive(false);
            partsPool.setPartAfterTime(transform, boltEffect.main.startLifetimeMultiplier);

            // A bolt on a turn part turns the ball there. Queued rather than turned on the spot, so
            // it happens on the part's centre like every other automatic turn - the ball reaches
            // this pickup up to 0.7 units before the centre.
            if (transform.parent.CompareTag("LandLeft") || transform.parent.CompareTag("LandRight"))
                playerMovement.queueAutoTurn(transform.parent);

            pickUpsManager.activateBolt();
        }
    }

    public void reset()
    {
        gameObject.SetActive(true);
        shineEffect.gameObject.SetActive(true);
        boltEffect.GetComponent<CFXR_Effect>().enabled = false;
        boltMesh.enabled = true;
        name = "PickUp";
    }
}
