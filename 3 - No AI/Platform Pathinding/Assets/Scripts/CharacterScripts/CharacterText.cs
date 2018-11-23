using System.Collections.Generic;
using UnityEngine;

public class CharacterText : MonoBehaviour
{
    [System.NonSerialized]
    public TextMesh _behaviourText;

    public static GameObject player;

    private void Awake()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        _behaviourText = transform.Find("BehaviourText").GetComponent<TextMesh>();
        _behaviourText.text = "";
    }


    /*gets called from pathagent when character finishes navigating path*/
    public void PathCompleted()
    {
        _behaviourText.text = "";
    }

    /*gets called from pathagent when character beings navigating path*/
    public void PathStarted()
    {
       _behaviourText.text = "Pathfinding"; 
    }
}