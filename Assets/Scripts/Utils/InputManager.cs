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
        if (PlayerMovement.WaitingForTap && (Input.GetButtonDown("autoTurn") || Input.GetButtonDown("turnLeft") || Input.GetButtonDown("turnRight")))
            playerMovement.continueAfterChillRevive(); // Chill's revive waits for this tap
        else if (userCanControl())
        {
            if (Input.GetButtonDown("turnRight"))
            {
                playerMovement.manualTurn(false);
            }
            if (Input.GetButtonDown("turnLeft"))
            {
                playerMovement.manualTurn(true);
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

    /// <summary>
    /// The back button on Android: RisingWayActivity sends every press here (UnitySendMessage to
    /// the GameManager object). Presses that still come in as the Escape key as well are one press:
    /// handleSystemBack ignores a second one within 0.3 s.
    /// </summary>
    public void OnAndroidBack(string unused)
    {
        menusController.handleSystemBack();
    }

    public bool userCanControl()
    {
        // In the tutorial too, moving or stopped: there PlayerMovement.tutorialTurn decides what a tap does.
        return Utility.gameStarted && Utility.camFollowPlayer && !Utility.boltIsOn;
    }
}
