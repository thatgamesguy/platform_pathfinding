using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Responsible for sending path instructions to the PathFollowingAgent.
/// </summary>
[RequireComponent(typeof(PathFollowingAgent))]
public class FollowPath : MonoBehaviour
{ 
    /// <summary>
    /// This is the list of intructions for the character to follow. The instruction consist of a position (transform)
    /// an 'order'. The order tells the character how they can reach this position. 
    /// Options are: 'walkable' (the character can walk to this position from the previous position) or
    /// 'jump' (the character has to jump to reach this position).
    /// </summary>
    public Instructions[] pathToFollow;

    /// <summary>
    /// The set of instructions is sent to this class to be processed.
    /// </summary>
    private PathFollowingAgent agent;

    void Awake()
    {
        agent = GetComponent<PathFollowingAgent>(); 
    }

    void Start()
    {
        // This actually starts the path finding based on the instructions.
        agent.StartFollowingPath(new List<Instructions>(pathToFollow), pathToFollow.Length > 0);
    }
}

/// <summary>
/// Consists of a transform and an 'order' (how the player can reach this position).
/// </summary>
[System.Serializable]
public class Instructions
{
    /// <summary>
    /// The transform of the position.
    /// </summary>
    public Transform moveTransform;

    /// <summary>
    /// The movement order either 'walkable' or 'jump'.
    /// </summary>
    public string order = "none";

    public Instructions(Transform moveTransform, string order)
    {
        this.moveTransform = moveTransform;
        this.order = order;
    }
}
