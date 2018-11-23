using System.Collections.Generic;
using UnityEngine;

public class AiController : MonoBehaviour
{
    private PathfindingAgent _pathAgent;
    public static Pathfinding _pathScript;
    [System.NonSerialized]
    public TextMesh _behaviourText;

    public static GameObject player;
    private bool destroy = false;

    private void Awake()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        _pathAgent = GetComponent<PathfindingAgent>();

        if (_pathScript == null) { _pathScript = GameObject.FindGameObjectWithTag("GameController").GetComponent<Pathfinding>(); }

        _behaviourText = transform.Find("BehaviourText").GetComponent<TextMesh>();
        _behaviourText.text = "";

    }

    public void GetInput(ref Vector3 velocity, ref Vector2 input, ref bool jumpRequest)
    {
        _pathAgent.AiMovement(ref velocity, ref input, ref jumpRequest);
    }

    /*Destroy object on lateupdate to avoid warning errors of objects not existing*/
    void LateUpdate()
    {
        if (destroy) { Destroy(gameObject); }
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