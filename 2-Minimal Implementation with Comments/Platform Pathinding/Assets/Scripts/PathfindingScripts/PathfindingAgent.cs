using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Add to any agent that will request and follow paths created by Pathfinding.
/// </summary>
public class PathfindingAgent : MonoBehaviour
{
    public bool isDebug = false; // Prints debug messages.
    public bool repathOnFail = true; // Sets whether a new path is calculated if we fail to follow the current one.

    private static Pathfinding _pathfinder; // Finds paths for us.
    private Character _character; // Stores jumping, movement ability etc. Required by the PathFinder.
    private CharacterController2D _controller; // Contains collision information.
    private AiController _aiController; // Updates text on screen to let us when we are pathfinding.
    private bool useStored = false; // Should we use a stored end point.
    private Vector3 storePoint; // Stores a previously requested end points.
    private int orderNum = -1; // When we request a path, we receive a list of orders. This is the index of the current order.
    private List<instructions> currentOrders = new List<instructions>(); // List of the current path instructions.
    private List<instructions> waitingOrders = null; // Set when we have received a new path.
    private Vector3 lastOrder; // We store the last order in case we need to calculate it again in future.
    private bool pathIsDirty = false; // If we fail to complete a path too many times (set by failAttempts) then we mark the path dirty to it is discarded or re-calculated.
    private float oldDistance; // Stores a previous distance from our current goal, if this doesn't change over time we mark the path as dirty.
    private int newPathAttempts = 3; // The number of times we attempt to retrieve a new path.
    private int newPathAttemptCount = 0; // How many times we have failed to retrieve a new path.
    private int failAttempts = 3; // How many times we can fail to follow a path before it is marked dirty.
    private float failAttemptCount = 0; // How many times we have failed to follow the current path.

    //Timers
    private float pathFailTimer = 0.25f; // The time between checks to see if we have progressed on the current path.
    private float fPathFailTimer; // The current failed path time.
    private float oneWayTimer = 0.1f; // The time between starting to fall and still being grounded that we coount it as a fail.
    private float fOneWayTimer; // Current not-falling time.

    //AI
    private float lastPointRandomAccuracy = 0.2f; // Random deviation added to last node.
    private static float pointAccuracy = 0.18f; // How close we have to get to a point before its registered as reached.
    private bool pathCompleted = true;
    private bool stopPathing = true;
    private bool hasLastOrder = false;
    private bool aiJumped = false;
    private bool onewayGrounded = false; 

    //Get Components
    private void Awake()
    {
        if (_pathfinder == null)
        {
            _pathfinder = GameObject.FindGameObjectWithTag("GameController").GetComponent<Pathfinding>();
        }

        _aiController = GetComponent<AiController>();
        _character = GetComponent<Character>();
        _controller = GetComponent<CharacterController2D>();
    }


    public void CancelPathing()
    {
        if (isDebug) { Debug.Log("path canceled"); }

        //Remove orders and prevent pathfinding
        hasLastOrder = false;
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

    private void PathNotFound()
    {
        if (isDebug) { Debug.Log("path not found"); }

        newPathAttemptCount++;

        if (newPathAttemptCount >= newPathAttempts)
        {
            CancelPathing(); if (isDebug) { Debug.Log("newpath attempt limit reached. cancelling path."); }
        }
    }

    //Used for refreshing paths, example: Flee behaviour 
    public int GetNodesFromCompletion()
    {
        if (currentOrders == null) { return 0; }
        int r = currentOrders.Count - orderNum;
        return r;
    }

    //Request path towards Vector3
    public void RequestPath(Vector3 pathVector)
    {
        if (_controller.collisions.below) // If the player is touching the ground.
        {
            useStored = false; // We don't want to store the path for later calculation, we're going to calculate one now instead.
            lastOrder = pathVector; // Let's store the end point of the path so we can calculate the path again in future.

            // This actually processes the path request:
            _pathfinder.RequestPathInstructions(gameObject, lastOrder, _character.jump.maxJumpHeight
                , _character.movement.ability
                , _character.jump.ability
                , _character.FallNodes
                );
        }
        else // If the player is not touching the ground.
        {
            // We'll store the path and keep attempting to calculate a path until the player is touching the ground.
            useStored = true;
            storePoint = pathVector;
        }
    }

    /// <summary>
    /// Adds path to waiting orders to be processed when the character is grounded.
    /// </summary>
    public void ReceivePathInstructions(List<instructions> instr, bool passed)
    {
        if (!passed)
        {
            PathNotFound();
            return;
        }

        waitingOrders = instr; //Storage for the path until we're ready to use it
    }

    /// <summary>
    /// Follows instructions for the current path.
    /// </summary>
    public void AiMovement(ref Vector3 velocity, ref Vector2 input, ref bool jumpRequest)
    {
        bool orderComplete = false; // We haven't reached the end node.

        if (!stopPathing && currentOrders != null && orderNum < currentOrders.Count) // We still want to follow the path.
        {
            if (currentOrders[orderNum].order != "jump") // We don't need to jump.
            {
                // So we can just walk to the next node.
                input.x = transform.position.x > currentOrders[orderNum].pos.x ? -1 : 1;
            }

            //prevent overshooting jumps and moving backwards & overcorrecting
            if (orderNum - 1 > 0
                && (currentOrders[orderNum - 1].order == "jump" || currentOrders[orderNum - 1].order == "fall")
                && transform.position.x + 0.18f > currentOrders[orderNum].pos.x &&
                transform.position.x - pointAccuracy < currentOrders[orderNum].pos.x)
            {
                // If we are in danger of jumping too far the stop our x velocity.
                velocity.x = 0f;
                transform.position = new Vector3(Mathf.Lerp(transform.position.x, currentOrders[orderNum].pos.x, 0.2f), transform.position.y, transform.position.z);
            }

            //match X position of node (Ground, Fall)
            if (currentOrders[orderNum].order != "jump"
                && transform.position.x + pointAccuracy > currentOrders[orderNum].pos.x
                && transform.position.x - pointAccuracy < currentOrders[orderNum].pos.x)
            {
                input.x = 0f;
                if (transform.position.y + 0.866f > currentOrders[orderNum].pos.y
                && transform.position.y - 0.866f < currentOrders[orderNum].pos.y)
                {
                    //if next node is a jump, remove velocity.x, and lerp position to point.
                    if (orderNum + 1 < currentOrders.Count && currentOrders[orderNum + 1].order == "jump")
                    {
                        velocity.x *= 0.0f;
                        transform.position = new Vector3(Mathf.Lerp(transform.position.x, currentOrders[orderNum].pos.x, 0.2f), transform.position.y, transform.position.z);
                    }

                    //if last node was a jump, and next node is a fall, remove velocity.x, and lerp position to point
                    if (orderNum + 1 < currentOrders.Count && orderNum - 1 > 0 && currentOrders[orderNum + 1].order == "fall" && currentOrders[orderNum + -1].order == "jump")
                    {
                        velocity.x *= 0.0f;
                        transform.position = new Vector3(Mathf.Lerp(transform.position.x, currentOrders[orderNum].pos.x, 0.5f), transform.position.y, transform.position.z);
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
            if (currentOrders[orderNum].order == "jump" && !aiJumped && _controller.collisions.below)
            {
                //velocity.y = characterScript.jumpVelocity;
                jumpRequest = true;
                aiJumped = true;
                if (orderNum + 1 < currentOrders.Count && Mathf.Abs(currentOrders[orderNum + 1].pos.x - currentOrders[orderNum].pos.x) > 1f)
                {
                    orderComplete = true;
                    aiJumped = false;
                }
            }
            else if (aiJumped && transform.position.y + 1f > currentOrders[orderNum].pos.y && transform.position.y - 1f < currentOrders[orderNum].pos.y)
            {
                orderComplete = true;
                aiJumped = false;
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
                fOneWayTimer = 0;

                if (orderNum < currentOrders.Count - 1) //used for DirtyPath
                {
                    oldDistance = Vector3.Distance(transform.position, currentOrders[orderNum].pos);
                }

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
        if (useStored)
        {
            RequestPath(storePoint);
        }

        if (waitingOrders != null && _controller.collisions.below) // We've received a path finding instruction and are currently grounded.
        {
            // Sets variables so character will start following path.
            currentOrders = waitingOrders;
            waitingOrders = null;
            pathCompleted = false;
            stopPathing = false;
            hasLastOrder = true;


            newPathAttemptCount = 0;
            orderNum = 0;

            failAttemptCount = 0;
            if (currentOrders != null && orderNum < currentOrders.Count - 1) //used for DirtyPath
            {
                oldDistance = Vector3.Distance(transform.position, currentOrders[orderNum].pos);
            }

            //If character is nowhere near starting node, we try to salvage the path by picking the nearest node and setting it as the start.
            if (Vector3.Distance(transform.position, currentOrders[0].pos) > 2f)
            {
                float closest = float.MaxValue;
                for (int i = 0; i < currentOrders.Count; i++)
                {
                    float distance = Vector3.Distance(currentOrders[i].pos, transform.position);
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

            //Add Random deviation to last node position to stagger paths (Staggers character positions / Looks better.)
            if (currentOrders.Count - 1 > 0 && currentOrders[currentOrders.Count - 1].order == "walkable")
            {
                currentOrders[currentOrders.Count - 1].pos.x += Random.Range(-1, 1) * lastPointRandomAccuracy;
            }

            PathStarted();
        }
    }

    //Requesting new path timers
    private void FixedUpdate()
    {
        if (onewayGrounded)
        {
            fOneWayTimer += Time.deltaTime;
            if (fOneWayTimer >= oneWayTimer)
            {
                fOneWayTimer = 0;
            }
        }

        if (repathOnFail && !pathCompleted)
        {
            fPathFailTimer += Time.deltaTime;

            if (fPathFailTimer > pathFailTimer)
            {
                fPathFailTimer = 0;
                if (currentOrders != null && currentOrders.Count > orderNum)
                {
                    float newDistance = Vector3.Distance(transform.position, currentOrders[orderNum].pos);
                    if (oldDistance <= newDistance)
                    {
                        failAttemptCount++;
                        if (failAttemptCount >= failAttempts && _controller.collisions.below)
                        {
                            failAttemptCount = 0;
                            pathIsDirty = true;
                        }
                    }
                    else
                    {
                        failAttemptCount = 0;
                    }

                    oldDistance = newDistance;
                }
            }
        }

        if (pathIsDirty) // If path is dirty, request new path.
        {
            pathIsDirty = false;
            if (hasLastOrder) { RequestPath(lastOrder); }
            if (isDebug)
            {
                Debug.Log("path is dirty");
            }
        }
    }
}