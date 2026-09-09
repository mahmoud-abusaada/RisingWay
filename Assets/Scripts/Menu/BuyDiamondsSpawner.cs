using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuyDiamondsSpawner : MonoBehaviour
{

    [SerializeField] private Transform diamond;
    [SerializeField] private int diamondsToSpawn;
    private IEnumerator spawnCoroutine;

    public void startSpawningDiamonds()
    {
        spawnCoroutine = spawnDiamonds();
        StartCoroutine(spawnCoroutine);
    }

    public void clearSpawnedDiamonds()
    {
        StopCoroutine(spawnCoroutine);
        Utility.clearAllChilds(transform);
    }

    private IEnumerator spawnDiamonds()
    {
        for (int i = 0; i < diamondsToSpawn; i++)
        {
            Transform spawnedDiamond = Instantiate(diamond, new Vector3(transform.position.x, transform.position.y + 3, transform.position.z), Quaternion.identity, transform);
            float randomScale = Random.Range(25, 55) / 100f;
            spawnedDiamond.localScale = new Vector3(randomScale, randomScale, randomScale);
            yield return new WaitForSeconds(0.1f);
        }
    }
}
