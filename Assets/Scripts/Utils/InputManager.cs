using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{

    private PlayerMovement playerMovement;
    private PlayerStats playerStats;
    private MenusController menusController;

    void Start()
    {
        playerMovement = FindObjectOfType<PlayerMovement>();
        playerStats = PlayerStats.Instance;
        menusController = FindObjectOfType<MenusController>();
    }

    void Update()
    {
        if (userCanControl())
        {
            if (Input.GetButtonDown("turnRight"))
            {
                playerMovement.turnRight();
            }
            if (Input.GetButtonDown("turnLeft"))
            {
                playerMovement.turnLeft();
            }
            if (Input.GetButtonDown("autoTurn"))
            {
                playerMovement.autoTurn();
            }
        }
        if (Application.platform == RuntimePlatform.Android)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                menusController.handleSystemBack();
            }
        }
    }

    public bool userCanControl()
    {
        return Utility.gameStarted && Utility.camFollowPlayer && !Utility.boltIsOn && (!playerStats.isTutorialsOn() || playerStats.isTutorialsOn() && Utility.stoppedForTutorials);
    }
}
