using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiamondsShopMenu : MonoBehaviour
{

    [SerializeField] private List<Transform> diamondSpawners;
    private MenusController menusController;

    void Start()
    {
        menusController = FindObjectOfType<MenusController>();
    }

    void OnEnable()
    {
        foreach (Transform diamondSpawner in diamondSpawners)
        {
            diamondSpawner.parent.gameObject.SetActive(true);
            diamondSpawner.GetComponent<BuyDiamondsSpawner>().startSpawningDiamonds();
        }
    }

    void OnDisable()
    {
        foreach (Transform diamondSpawner in diamondSpawners)
        {
            diamondSpawner.parent.gameObject.SetActive(false);
            diamondSpawner.GetComponent<BuyDiamondsSpawner>().clearSpawnedDiamonds();
        }
    }

    public void Back()
    {
        menusController.hideCurrentMenu(false);
    }

}
