using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Add to any agent that will request and follow paths created by Pathfinding.
/// </summary>
public class PathFollowingAgent : MonoBehaviour
{
    public bool isDebug = false; // Prints debug messages.

    private CharacterController2D _controller; // Contains collision information.
    private CharacterText _aiController; // Updates text on screen to let us when we are pathfinding.
    private int orderNum = -1; // When we request a path, we receive a list of orders. This is the index of the current order.
    private List<Instructions> currentOrders = new List<Instructions>(); // List of the current path instructions.
    private List<Instructions> waitingOrders = null; // Set when we have received a new path.
    private static float pointAccuracy = 0.18f; // How close we have to get to a point before its registered as reached.
    private bool stopPathing = true;
    private bool hasJumped = false;
    private bool onewayGrounded = false;

    private void Awake()
    {
        _aiController = GetComponent<CharacterText>();
        _controller = GetComponent<CharacterController2D>();
    }

    public void CancelPathing()
    {
        if (isDebug) { Debug.Log("path canceled"); }

        currentOrders = null;
        stopPathing = true;
    }

    private void PathStarted()
    {
        if (isDebug) { Debug.Log("path started"); }

        _aiController.PathStarted();
    }

    private void PathCompleted()
    {
        if (isDebug) { Debug.Log("path completed"); }

        CancelPathing(); //Reset Variables && Clears the debugging gizmos from drawing

        _aiController.PathCompleted(); //Path was completed
    }

    /// <summary>
    /// Adds path to waiting orders to be processed when the character is grounded.
    /// </summary>
    public void StartFollowingPath(List<Instructions> instr, bool passed)
    {
        if (passed)
        {
            waitingOrders = instr; //Storage for the path until we're ready to use it
        }
    }

    /// <summary>
    /// Follows instructions for the current path.
    /// </summary>
    public void ProcessMovement(ref Vector3 velocity, ref Vector2 input, ref bool jumpRequest)
    {
        bool orderComplete = false; // We haven't reached the end node.

        if (!stopPathing && currentOrders != null && orderNum < currentOrders.Count) // We still want to follow the path.
        {
            if (currentOrders[orderNum].order != "jump") // We don't need to jump.
            {
                // So we can just walk to the next node.
                input.x = transform.position.x > currentOrders[orderNum].moveTransform.position.x ? -1 : 1;
            }

            //prevent overshooting jumps and moving backwards & overcorrecting
            if (orderNum - 1 > 0
                && (currentOrders[orderNum - 1].order == "jump" || currentOrders[orderNum - 1].order == "fall")
                && transform.position.x + 0.18f > currentOrders[orderNum].moveTransform.position.x &&
                transform.position.x - pointAccuracy < currentOrders[orderNum].moveTransform.position.x)
            {
                // If we are in danger of jumping too far the stop our x velocity.
                velocity.x = 0f;
                transform.position = new Vector3(Mathf.Lerp(transform.position.x, currentOrders[orderNum].moveTransform.position.x, 0.2f), transform.position.y, transform.position.z);
            }

            //match X position of node (Ground, Fall)
            if (currentOrders[orderNum].order != "jump"
                && transform.position.x + pointAccuracy > currentOrders[orderNum].moveTransform.position.x
                && transform.position.x - pointAccuracy < currentOrders[orderNum].moveTransform.position.x)
            {
                input.x = 0f;
                if (transform.position.y + 0.866f > currentOrders[orderNum].moveTransform.position.y
                && transform.position.y - 0.866f < currentOrders[orderNum].moveTransform.position.y)
                {
                    //if next node is a jump, remove velocity.x, and lerp position to point.
                    if (orderNum + 1 < currentOrders.Count && currentOrders[orderNum + 1].order == "jump")
                    {
                        velocity.x *= 0.0f;
                        transform.position = new Vector3(Mathf.Lerp(transform.position.x, currentOrders[orderNum].moveTransform.position.x, 0.2f), transform.position.y, transform.position.z);
                    }

                    //if last node was a jump, and next node is a fall, remove velocity.x, and lerp position to point
                    if (orderNum + 1 < currentOrders.Count && orderNum - 1 > 0 && currentOrders[orderNum + 1].order == "fall" && currentOrders[orderNum + -1].order == "jump")
                    {
                        velocity.x *= 0.0f;
                        transform.position = new Vector3(Mathf.Lerp(transform.position.x, currentOrders[orderNum].moveTransform.position.x, 0.5f), transform.position.y, transform.position.z);
                    }

                    if (currentOrders[orderNum].order == "fall")
                    {
                        // We are falling down so set our y velocity to negative.
                        input.y = -1;
                    }

                    orderComplete = true;
                }
            }

            //Jump
            if (currentOrders[orderNum].order == "jump" && !hasJumped && _controller.collisions.below)
            {
                //velocity.y = characterScript.jumpVelocity;
                jumpRequest = true;
                hasJumped = true;
                if (orderNum + 1 < currentOrders.Count && Mathf.Abs(currentOrders[orderNum + 1].moveTransform.position.x - currentOrders[orderNum].moveTransform.position.x) > 1f)
                {
                    orderComplete = true;
                    hasJumped = false;
                }
            }
            else if (hasJumped && transform.position.y + 1f > currentOrders[orderNum].moveTransform.position.y && transform.position.y - 1f < currentOrders[orderNum].moveTransform.position.y)
            {
                orderComplete = true;
                hasJumped = false;
            }

            //oneway nodes
            if (orderNum > 0 && (currentOrders[orderNum - 1].order == "fall" || currentOrders[orderNum - 1].order == "jump"))
            {
                if (_controller.collisions.below)
                {
                    onewayGrounded = true;
                    if (onewayGrounded)
                    {
                        input.y = -1;
                        onewayGrounded = false;
                    }
                }
            }

            if (orderComplete) // We've completed the one instruction.
            {
                orderNum++;

                onewayGrounded = false; //oneway detection

                if (orderNum >= currentOrders.Count)
                {
                    velocity.x = 0;
                    PathCompleted(); //Carry out orders when the node is finally reached...
                }
            }
        }
    }

    //Applying new paths when character is ready
    void Update()
    {
        if (waitingOrders != null && _controller.collisions.below) // We've received a path finding instruction and are currently grounded.
        {
            // Sets variables so character will start following path.
            currentOrders = waitingOrders;
            waitingOrders = null;
            stopPathing = false;

            orderNum = 0;

            //If character is nowhere near starting node, we try to salvage the path by picking the nearest node and setting it as the start.
            if (Vector3.Distance(transform.position, currentOrders[0].moveTransform.position) > 2f)
            {
                float closest = float.MaxValue;
                for (int i = 0; i < currentOrders.Count; i++)
                {
                    float distance = Vector3.Distance(currentOrders[i].moveTransform.position, transform.position);
                    if (currentOrders[i].order == "walkable" && distance < closest)
                    {
                        closest = distance;
                        orderNum = i;
                    }
                }
            }

            //If possible, we skip the first node, this prevents that character from walking backwards to first node.
            if (currentOrders.Count > orderNum + 1 && currentOrders[orderNum].order == currentOrders[orderNum + 1].order &&
                currentOrders[orderNum].order == "walkable")
            {
                orderNum += 1;
            }

            PathStarted();
        }
    }


}