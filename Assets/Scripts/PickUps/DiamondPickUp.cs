using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiamondPickUp : MonoBehaviour
{
    [SerializeField] private ParticleSystem diamondEffect;
    [SerializeField] private MeshRenderer diamondMesh;
    private PartsPool partsPool;
    private PickUpsManager pickUpsManager;

    void Start()
    {
        partsPool = FindObjectOfType<PartsPool>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
    }

    // static int pickedDiamonds = 0;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // GetComponent<AudioSource>().Play();
            // ParticleSystem ps = transform.Find("DiamondEffect").GetComponent<ParticleSystem>();
            // ps.transform.position = other.transform.position + new Vector3(0, .5f, 0);
            diamondEffect.Play();
            diamondMesh.enabled = false;
            GetComponent<SphereCollider>().enabled = false;
            partsPool.setPartAfterTime(transform, diamondEffect.main.startLifetimeMultiplier);

            pickUpsManager.diamondPickedUp();

            if (Utility.boltIsOn || SoundManager.Instance.getSfxDiamondPitch() > 1)
                SoundManager.Instance.increaseSfxPitch();

            // if (lastOne)
            //     StartCoroutine(ResetPitchCoroutine());

            // pickedDiamonds++;
            // Debug.Log("Picked a diamond, count = " + pickedDiamonds);
        }
    }

    // private IEnumerator ResetPitchCoroutine()
    // {
    //     yield return new WaitForSecondsRealtime(0.5f);
    //     SoundManager.Instance.resetSfxPitch();
    // }

    public void reset()
    {
        gameObject.SetActive(true);
        diamondMesh.enabled = true;
        GetComponent<SphereCollider>().enabled = true;
        name = "PickUp";
    }
}
