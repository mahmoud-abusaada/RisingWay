using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GG.Infrastructure.Utils.Swipe;

public class SwipeDetection : MonoBehaviour
{
	[SerializeField] private SwipeListener swipeListener;
	[SerializeField] private InGameUI inGameUI;

	void OnEnable()
	{
		swipeListener.OnSwipe.AddListener(OnSwipe);
	}

	private void OnSwipe(string swipe)
	{
		if (swipe.Equals("Left"))
		{
			inGameUI.TurnLeft();
		}
		else if (swipe.Equals("Right"))
		{
			inGameUI.TurnRight();
		}
	}

	void OnDisable()
	{
		swipeListener.OnSwipe.RemoveListener(OnSwipe);
	}
}
