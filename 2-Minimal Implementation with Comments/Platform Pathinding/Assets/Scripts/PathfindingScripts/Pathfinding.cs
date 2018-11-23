using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Calculates A* paths.
/// </summary>
public class Pathfinding : MonoBehaviour
{
    public GameObject _currentMap;

    public float blockSize = 1f; //each block is square. This should probably match your square 2dCollider on a tile.
    public float jumpHeight = 3.8f; //the maximum jump height of a character
    public float maxJumpBlocksX = 3f; //the furthest a character can jump without momentum
    public float jumpHeightIncrement = 1f;
    public float minimumJump = 1.8f;

    private float groundNodeHeight = 0.01f; //percentage of blockSize (Determines height off ground level for a groundNode)
    private float groundMaxWidth = 0.35f; //percentage of blockSize (Determines max spacing allowed between two groundNodes)
    private float fall_X_Spacing = 0.25f; //percentage of blockSize (Determines space away from groundNode's side to place the fallNode)
    private float fall_Y_GrndDist = 0.02f;
    private LayerMask groundLayer;
    private List<pathNode> nodes = new List<pathNode>();
    private List<pathNode> groundNodes = new List<pathNode>();

    public nodeWeight nodeWeights;

    void Awake()
    {
        groundLayer = LayerManager.instance.groundLayer;
    }

    void Start()
    {
        CreateNodeMap();
    }

    void CreateNodeMap()
    {
        nodes = new List<pathNode>();
        groundNodes = new List<pathNode>();

        List<GameObject> groundObjects = new List<GameObject>();

        //oneway.value == 1 << hit.transform.gameObject.layer
        //Find all children of tile parent
        foreach (Transform child in _currentMap.transform)
        {
            // Debug.Log(child.gameObject.layer + " " + (1 << LayerMask.NameToLayer("ground")));
            if (1 << child.gameObject.layer == groundLayer.value)
            {
                groundObjects.Add(child.gameObject);
            }
        }

        FindGroundNodes(groundObjects);
        FindFallNodes(groundNodes);
        FindJumpNodes(groundNodes);

        GroundNeighbors(groundNodes, groundNodes);

        JumpNeighbors(attachedJumpNodes(groundNodes), groundNodes);
        FallNeighbors(attachedFallNodes(groundNodes), groundNodes);  
    }

    /// <summary>
    /// Adds a new path calculation order to a thread queue. This queue is processed in the MakeThreadDoWork method, which is 
    /// called from the update method.
    /// </summary>
    public void RequestPathInstructions(GameObject character, Vector3 location, float jumpH, bool movement, bool jump, bool fall)
    {
        // Creates a data structure that stores everything we need to calculate a path.
        PathData newLocker = new PathData(character, location, jumpH, movement, jump, fall);

        // And this actually calculates the path!
        FindPath(newLocker);
    }

    /// <summary>
    /// This actually performs the path calculation.
    /// </summary>
    /// <param name="data">Data required to calculate the path.</param>
    private void FindPath(PathData data)
    {
        // If the character is not a path finding agent then we do not want to calculate a path for it.
        if (data.character.transform.GetComponent<PathfindingAgent>() == null)
        {
            return;
        }

        Vector3 characterPos = data.charPos; // Gets the characters current position. This will be the start of the parh.
        Vector3 location = data.end; // The end of the path.
        float characterJump = data.jump; // How far can the character jump. We need to know this to calculate any jumps.

        List<instructions> instr = new List<instructions>(); // This stores a list of instructions that will be used by the character to traverse the path.

        List<pathNode> openNodes = new List<pathNode>(); // Stores all nodes that we are currently considering.
        List<pathNode> closedNodes = new List<pathNode>(); // Nodes that have already been considered.
        List<pathNode> pathNodes = new List<pathNode>(); // Stores our final path.

        ResetLists(); // Sets parent to null for each node. The parent of a node is the previous node in a path.

        pathNode startNode = getNearestGroundNode(characterPos); // The start node for the path.

        pathNode endNode = getNearestNode(location); // The end node for the path.

        if (endNode == null || startNode == null || !data.canMove) // If a point couldnt be found or if character can't move cancel path
        {
            return;
        }

        // A number of costs are used to define nodes. These costs are used to determine whether a node is added to the final path list. 
        // Lower costs are more desirable.
        // G = c + previous node g. This is the cost to move from the start node to this node. This cost is used to find the path with the least number of jumps.
        // F = how much it costs to move from this node to the end of the path. A lower cost means it is closer to the goal node.
        // H = the total cost of this node, calculated using g + f
        // C = predetermined weight based on whether the node is a ground node or jump node (ground nodes are more desirable and weigh less)

        startNode.g = 0; // Since this is the first node it has 0 cost.
        startNode.h = Vector3.Distance(startNode.pos, endNode.pos); // Calculates the cost to end of the path using the distance.

        openNodes.Add(startNode); // Adds the start node to the open list.

        pathNode currentNode = null; // Will store the current node we are considering.

        while (openNodes.Count > 0) // While there are nodes to consider.
        {
            float lowestScore = float.MaxValue; // We want to move to the node with the lowest score.

            for (int i = 0; i < openNodes.Count; i++)
            {
                if (openNodes[i].h < lowestScore && !closedNodes.Contains(openNodes[i])) // If this node has a lower score then it is more desirable.
                {
                    currentNode = openNodes[i]; // As this node has the lowest score we want to consider it.
                    lowestScore = currentNode.h;
                }
            }

            if (currentNode == endNode) // We've reached the end of the path!
            {
                closedNodes.Add(currentNode); // Adds last node to path.
                break; // Lets get out of the loop.
            }
            else // We haven't reached the end of the path yet.
            {
                closedNodes.Add(currentNode); // Adds the current node to closedNodes as we are about to consider its inclusion in the path.
                openNodes.Remove(currentNode); // Remove from open nodes so we don't use it again.

                // We can only use this node if either:
                // 1. The node is a ground node (so the player can just walk to it)
                // 2. The node is a jump node (i.e. the player has to jump to reach it) and the player can jump to reach the node.
                if (currentNode.type != "jump" || (currentNode.type == "jump"
                    && Mathf.Abs(currentNode.realHeight - characterJump) < jumpHeightIncrement * 0.92)
                    && characterJump <= currentNode.realHeight + jumpHeightIncrement * 0.08)
                {
                    for (int i = 0; i < currentNode.neighbours.Count; i++) // Loops through all node neighbors to try and find the next node to mvoe towards.
                    {
                        // If the neighbor node is a jumping node and the character cannot jump we are not interested.
                        if (!data.canJump && currentNode.neighbours[i].type == "jump") { continue; }

                        // Also if the neghbor node is a falling node and the character cannot fall then we are not interested.
                        if (!data.canFall && currentNode.neighbours[i].type == "fall") { continue; }

                        if (currentNode.neighbours[i].parent == null) // If the nodes parent is null (i.e. it has not been used yet)
                        {
                            currentNode.neighbours[i].g = currentNode.neighbours[i].c + currentNode.g; // Calculates cost from start node to this node.
                            currentNode.neighbours[i].f = Vector3.Distance(currentNode.neighbours[i].pos, endNode.pos); // Calculates how far from the goal node this node is. The closer the better!
                            if (currentNode.neighbours[i].type == "jump")
                            {
                                // If the node is a jump node we increase the cost based on the distance from the floor.
                                currentNode.neighbours[i].f += currentNode.neighbours[i].realHeight;
                            }

                            currentNode.neighbours[i].h = currentNode.neighbours[i].g + currentNode.neighbours[i].f; // Calculates total cost of node. This is used to determine whether it will be included in the path list.
                            currentNode.neighbours[i].parent = currentNode; // Sets the previous node as a parent node so, if selected, the overal path can be found.
                            openNodes.Add(currentNode.neighbours[i]); // Adds the node to the open nodes for consideration.
                        }
                        else if (currentNode.g + currentNode.neighbours[i].c < currentNode.neighbours[i].g) // As the nodes parent is not null, it's g cost has already been calculated from a previous node. 
                        {
                            currentNode.neighbours[i].g = currentNode.neighbours[i].c + currentNode.g; // Sets g score.
                            currentNode.neighbours[i].h = currentNode.neighbours[i].g + currentNode.neighbours[i].f; // Calculates h score.
                            currentNode.neighbours[i].parent = currentNode; // Sets parent.
                        }
                    }
                }
            }
        }

        // At this point we should have calculated a path, now we just need to add the path to the pathNodes list.
        while (currentNode != startNode) // Loops backwards until we reach startNode.
        {
            pathNodes.Add(currentNode); // Adds current node to list.
            currentNode = currentNode.parent; // Sets current node to the next in list.
        }

        data.passed = pathNodes[0] == endNode; // We've successfully found a path if the first node is the end node.

        pathNodes.Reverse(); // As we've worked backwards we need to reverse the list.

        for (int i = 0; i < pathNodes.Count; i++) // This builds a list of instructions for the character to follow.
        {
            instructions temp = new instructions(pathNodes[i].pos, pathNodes[i].type); // Instructions are created based on the node type, whether it is ground or jump.
            instr.Add(temp);
        }

        data.instr = instr;

        // We've created the path and built the instructions so now we need to deliver them to the character so they can be processed.
        data.character.transform.GetComponent<PathfindingAgent>().ReceivePathInstructions(data.instr, data.passed);
    }

    private void ResetLists()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].parent = null;
        }
    }

    //Used for runtime adding terrain block to pathfinding nodes
    public void CreateBlockCalled(Vector3 position)
    {

        GameObject newGameObject = (GameObject)Instantiate(Resources.Load("Tiles/GroundTile"), new Vector3(position.x, position.y, 0.0f), Quaternion.identity) as GameObject;
        newGameObject.GetComponent<SpriteRenderer>().sprite = Resources.Load("Sprites/ground2", typeof(Sprite)) as Sprite;
        //add the ground node, 

        Vector3 ground = newGameObject.transform.position; ground.y += blockSize * 0.5f + blockSize * groundNodeHeight;
        pathNode newGroundNode = new pathNode("walkable", ground);
        groundNodes.Add(newGroundNode);

        newGroundNode.c = nodeWeights.GetNodeWeightByString(newGroundNode.type);
        nodes.Add(newGroundNode);

        newGroundNode.gameObject = newGameObject;

        //run it through the list

        RefreshAreaAroundBlock(newGameObject, false);

    }

    //Used by creating and Removing pathfinding nodes
    public void RefreshAreaAroundBlock(GameObject go, bool blockRemoved)
    {

        List<pathNode> collect = new List<pathNode>();
        List<pathNode> largerCollect = new List<pathNode>();
        float searchSize = 4.2f;
        float largerSearchSize = 9f;

        //we remove all node connections related to the destroyed block... if a block needs to be removed...
        if (blockRemoved)
        {
            for (int i = 0; i < groundNodes.Count; i++)
            {
                if (groundNodes[i].gameObject == go)
                {

                    while (groundNodes[i].createdJumpNodes.Count > 0)
                    {

                        nodes.Remove(groundNodes[i].createdJumpNodes[0]);
                        groundNodes[i].createdJumpNodes.RemoveAt(0);
                    }
                    while (groundNodes[i].createdFallNodes.Count > 0)
                    {

                        nodes.Remove(groundNodes[i].createdFallNodes[0]);
                        groundNodes[i].createdFallNodes.RemoveAt(0);
                    }

                    nodes.Remove(groundNodes[i]);
                    groundNodes.Remove(groundNodes[i]);
                    break;
                }
            }
        }

        //find all nearby blocks based on searchSizes
        for (int i = 0; i < groundNodes.Count; i++)
        {

            if ((groundNodes[i].pos - go.transform.position).magnitude < searchSize)
            {

                groundNodes[i].neighbours = new List<pathNode>();
                collect.Add(groundNodes[i]);
            }
            if ((groundNodes[i].pos - go.transform.position).magnitude < largerSearchSize ||
                (Mathf.Abs(groundNodes[i].pos.x - go.transform.position.x) < searchSize && Mathf.Abs(groundNodes[i].pos.y - go.transform.position.y) < 8f))
            {
                largerCollect.Add(groundNodes[i]);
            }
        }

        //Debug.Log(collect.Count);
        UpdateNodes(collect, largerCollect);
    }

    public void UpdateNodes(List<pathNode> collection, List<pathNode> largerCollection)
    {

        //resetAllLists();
        for (int i = 0; i < collection.Count; i++)
        {

            while (collection[i].createdJumpNodes.Count > 0)
            {

                nodes.Remove(collection[i].createdJumpNodes[0]);
                collection[i].createdJumpNodes.RemoveAt(0);
            }
            while (collection[i].createdFallNodes.Count > 0)
            {

                nodes.Remove(collection[i].createdFallNodes[0]);
                collection[i].createdFallNodes.RemoveAt(0);
            }
        }


        FindFallNodes(collection);
        FindJumpNodes(collection);

        GroundNeighbors(collection, largerCollection);

        JumpNeighbors(attachedJumpNodes(collection), largerCollection);
        FallNeighbors(attachedFallNodes(collection), largerCollection);
    }



    private void FindGroundNodes(List<GameObject> objects)
    {
        nodes = new List<pathNode>();

        for (int i = 0; i < objects.Count; i++)
        {
            Vector3 ground = objects[i].transform.position; ground.y += blockSize * 0.5f + blockSize * groundNodeHeight;
            pathNode newGroundNode = new pathNode("walkable", ground);
            groundNodes.Add(newGroundNode);

            newGroundNode.c = nodeWeights.GetNodeWeightByString(newGroundNode.type);
            nodes.Add(newGroundNode);

            newGroundNode.gameObject = objects[i];
        }
    }

    private void FindOnewayNodes(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            Vector3 ground = objects[i].transform.position; ground.y += blockSize * 0.5f + blockSize * groundNodeHeight;
            pathNode newGroundNode = new pathNode("walkable", ground);
            groundNodes.Add(newGroundNode);

            newGroundNode.c = nodeWeights.GetNodeWeightByString(newGroundNode.type);
            nodes.Add(newGroundNode);

            newGroundNode.gameObject = objects[i];
        }
    }

    private void GroundNeighbors(List<pathNode> fromNodes, List<pathNode> toNodes)
    {
        //Distance max distance allowed between two nodes
        float distanceBetween = blockSize + groundMaxWidth;

        for (int i = 0; i < fromNodes.Count; i++)
        {
            pathNode a = fromNodes[i];

            for (int t = 0; t < toNodes.Count; t++)
            {
                pathNode b = toNodes[t];

                //PREFORM A - > B TESTING HERE

                //testing distance between nodes
                if (Mathf.Abs(a.pos.y - b.pos.y) < blockSize * 0.7 && Vector3.Distance(a.pos, b.pos) < distanceBetween)
                {
                    //testing collision between nodes
                    if (!Physics2D.Linecast(a.pos, b.pos, groundLayer))
                    {
                        a.neighbours.Add(b);
                    }
                }

                //END TESTING
            }
        }
    }

    private void FindJumpNodes(List<pathNode> searchList)
    {
        if (jumpHeight > 0)
        {
            for (int i = 0; i < searchList.Count; i++)
            {
                float curHeight = jumpHeight;

                while (curHeight >= minimumJump)
                {
                    Vector3 air = searchList[i].pos; air.y += curHeight;

                    if (!Physics2D.Linecast(searchList[i].pos, air, groundLayer))
                    {
                        pathNode newJumpNode = new pathNode("jump", air);

                        newJumpNode.spawnedFrom = searchList[i]; //this node has been spawned from a groundNode
                        //jumpNodes.Add(newJumpNode);
                        newJumpNode.c = nodeWeights.GetNodeWeightByString(newJumpNode.type);
                        newJumpNode.height = curHeight;
                        newJumpNode.realHeight = curHeight;
                        nodes.Add(newJumpNode);

                        newJumpNode.spawnedFrom.createdJumpNodes.Add(newJumpNode);
                    }
                    else
                    {
                        float h = curHeight;
                        float minHeight = blockSize * 1f; //2f
                        while (h > minHeight)
                        {
                            Vector3 newHeight = new Vector3(air.x, air.y - (curHeight - h), air.z);
                            if (!Physics2D.Linecast(searchList[i].pos, newHeight, groundLayer))
                            {
                                pathNode newJumpNode = new pathNode("jump", newHeight);

                                newJumpNode.spawnedFrom = searchList[i]; //this node has been spawned from a groundNode
                                //jumpNodes.Add(newJumpNode);
                                newJumpNode.c = nodeWeights.GetNodeWeightByString(newJumpNode.type);
                                newJumpNode.realHeight = curHeight;
                                newJumpNode.height = h;
                                nodes.Add(newJumpNode);

                                newJumpNode.spawnedFrom.createdJumpNodes.Add(newJumpNode);
                                break;
                            }
                            else
                            {
                                //0.5f
                                h -= blockSize * 0.1f;
                            }
                        }
                    }
                    curHeight -= jumpHeightIncrement;
                }
            }
        }
    }

    private void JumpNeighbors(List<pathNode> fromNodes, List<pathNode> toNodes)
    {
        for (int i = 0; i < fromNodes.Count; i++)
        {
            pathNode a = fromNodes[i];

            for (int t = 0; t < toNodes.Count; t++)
            {
                pathNode b = toNodes[t];

                //PREFORM A - > B TESTING HERE

                a.spawnedFrom.neighbours.Add(a);

                //float realJumpHeight = a.pos.y - a.spawnedFrom.pos.y;
                float xDistance = Mathf.Abs(a.pos.x - b.pos.x);

                if (xDistance < blockSize * maxJumpBlocksX + blockSize + groundMaxWidth) //
                    //the x distance modifier used to be 0.72!
                    if (b != a.spawnedFrom && a.pos.y > b.pos.y + blockSize * 0.5f &&

                        a.pos.y - b.pos.y > Mathf.Abs(a.pos.x - b.pos.x) * 0.9f - blockSize * 1f &&
                          Mathf.Abs(a.pos.x - b.pos.x) < blockSize * 4f + groundMaxWidth)
                    { //4.7, 4Xjump, +1Y isnt working
                        if (!Physics2D.Linecast(a.pos, b.pos, groundLayer))
                        {
                            bool hitTest = true;
                            if ((Mathf.Abs(a.pos.x - b.pos.x) < blockSize + groundMaxWidth && a.spawnedFrom.pos.y == b.pos.y) ||
                                (a.pos.y - a.spawnedFrom.pos.y + 0.01f < a.height && Mathf.Abs(a.pos.x - b.pos.x) > blockSize + groundMaxWidth))
                            {
                                hitTest = false;

                            }

                            //hit head code... jump height must be above 2.5 to move Xdistance2.5 else you can only move 1 block when hitting head.
                            if (a.realHeight > a.height)
                            {
                                float tempFloat = a.height > 2.5f ? 3.5f : 1.5f;
                                if (tempFloat == 1.5f && a.height > 1.9f) { tempFloat = 2.2f; }
                                if (a.spawnedFrom.pos.y < b.pos.y && Mathf.Abs(a.spawnedFrom.pos.x - b.pos.x) > blockSize * 1.5f) { tempFloat = 0f; }
                                if (Mathf.Abs(a.spawnedFrom.pos.x - b.pos.x) > blockSize * tempFloat)
                                {
                                    hitTest = false;
                                }

                            }


                            if (hitTest)
                            {
                                // if (xDistance < blockSize + groundMaxWidth) {
                                //if (a.spawnedFrom.pos.y >= b.pos.y) {
                                float middle = -(a.pos.x - b.pos.x) / 2f;
                                float quarter = middle / 2f;

                                Vector3 origin = a.spawnedFrom.pos;
                                Vector3 midPoint = new Vector3(a.pos.x + middle, a.pos.y, a.pos.z);
                                Vector3 quarterPoint = new Vector3(a.pos.x + quarter, a.pos.y, a.pos.z);

                                Vector3 quarterPastMidPoint = new Vector3(a.pos.x + middle + quarter, a.pos.y - blockSize, a.pos.z);
                                Vector3 lowerMid = new Vector3(a.pos.x + middle, a.pos.y - blockSize, a.pos.z);
                                Vector3 straightUp = new Vector3(b.pos.x, a.pos.y - blockSize, a.pos.z);

                                //Debug.DrawLine(origin, quarterPoint, Color.yellow);
                                //Debug.DrawLine(origin, midPoint, Color.yellow);

                                // Debug.DrawLine(lowerMid, b.pos, Color.yellow);
                                // Debug.DrawLine(b.pos, quarterPastMidPoint, Color.yellow);
                                //Debug.DrawLine(b.pos, straightUp, Color.yellow);
                                if (xDistance > blockSize + groundMaxWidth)
                                    if (Physics2D.Linecast(origin, quarterPoint, groundLayer) ||

                                        (xDistance > blockSize + groundMaxWidth &&
                                         Physics2D.Linecast(b.pos, quarterPastMidPoint, groundLayer) &&
                                         a.spawnedFrom.pos.y >= b.pos.y - groundNodeHeight) ||

                                        (Physics2D.Linecast(origin, midPoint, groundLayer)) ||

                                          (xDistance > blockSize + groundMaxWidth &&
                                         a.spawnedFrom.pos.y >= b.pos.y - groundNodeHeight &&
                                         Physics2D.Linecast(lowerMid, b.pos, groundLayer)) ||

                                            (xDistance > blockSize * 1f + groundMaxWidth &&
                                         a.spawnedFrom.pos.y >= b.pos.y &&
                                          Physics2D.Linecast(b.pos, straightUp, groundLayer))
                                       )
                                    {
                                        hitTest = false;
                                    }
                                //}
                                //}
                            }

                            if (hitTest)
                            {
                                a.neighbours.Add(b);
                            }
                        }
                    }

                //END TESTING
            }
        }
    }

    private void FindFallNodes(List<pathNode> searchList)
    {
        float spacing = blockSize * 0.5f + blockSize * fall_X_Spacing;

        for (int i = 0; i < searchList.Count; i++)
        {
            Vector3 leftNode = searchList[i].pos; leftNode.x -= spacing;
            Vector3 rightNode = searchList[i].pos; rightNode.x += spacing;

            //raycheck left
            if (!Physics2D.Linecast(searchList[i].pos, leftNode, groundLayer))
            {
                Vector3 colliderCheck = leftNode;
                colliderCheck.y -= fall_Y_GrndDist;

                //raycheck down
                if (!Physics2D.Linecast(leftNode, colliderCheck, groundLayer))
                {
                    pathNode newFallNode = new pathNode("fall", leftNode);

                    newFallNode.spawnedFrom = searchList[i]; //this node has been spawned from a groundNode
                    //fallNodes.Add(newFallNode);

                    newFallNode.c = nodeWeights.GetNodeWeightByString(newFallNode.type);
                    nodes.Add(newFallNode);

                    newFallNode.spawnedFrom.createdFallNodes.Add(newFallNode);

                    //Debug.DrawLine(nodes[i].pos, temp.pos, Color.red);
                    //climbNodes("right", nodes[i]);
                }
            }

            //raycheck right
            if (!Physics2D.Linecast(searchList[i].pos, rightNode, groundLayer))
            {
                Vector3 colliderCheck = rightNode;
                colliderCheck.y -= fall_Y_GrndDist;

                //raycheck down
                if (!Physics2D.Linecast(rightNode, colliderCheck, groundLayer))
                {
                    pathNode newFallNode = new pathNode("fall", rightNode);

                    newFallNode.spawnedFrom = searchList[i]; //this node has been spawned from a groundNode
                    //fallNodes.Add(newFallNode);

                    newFallNode.c = nodeWeights.GetNodeWeightByString(newFallNode.type);
                    nodes.Add(newFallNode);

                    newFallNode.spawnedFrom.createdFallNodes.Add(newFallNode);

                    //Debug.DrawLine(nodes[i].pos, temp.pos, Color.red);
                    //climbNodes("right", nodes[i]);
                }
            }
        }
    }

    private void FallNeighbors(List<pathNode> fromNodes, List<pathNode> toNodes)
    {
        for (int i = 0; i < fromNodes.Count; i++)
        {
            pathNode a = fromNodes[i];

            for (int t = 0; t < toNodes.Count; t++)
            {
                pathNode b = toNodes[t];

                //PREFORM A - > B TESTING HERE
                float xDistance = Mathf.Abs(a.pos.x - b.pos.x);
                a.spawnedFrom.neighbours.Add(a);

                if ((xDistance < blockSize * 1f + groundMaxWidth && a.pos.y > b.pos.y) || (a.pos.y - b.pos.y > Mathf.Abs(a.pos.x - b.pos.x) * 2.2f + blockSize * 1f && //2.2 + blocksize * 1f
                    xDistance < blockSize * 4f))
                {
                    if (!Physics2D.Linecast(a.pos, b.pos, groundLayer))
                    {
                        bool hitTest = true;

                        float middle = -(a.pos.x - b.pos.x) * 0.5f;
                        float quarter = middle / 2f;

                        float reduceY = Mathf.Abs(a.pos.y - b.pos.y) > blockSize * 4f ? blockSize * 1.3f : 0f;

                        Vector3 middlePointDrop = new Vector3(a.pos.x + middle, a.pos.y - reduceY, a.pos.z);
                        Vector3 quarterPointTop = new Vector3(a.pos.x + quarter, a.pos.y, a.pos.z);
                        Vector3 quarterPointBot = new Vector3(b.pos.x - quarter, b.pos.y, b.pos.z);

                        Vector3 corner = new Vector3(b.pos.x, (a.pos.y - blockSize * xDistance - blockSize * 0.5f) - groundNodeHeight, a.pos.z);

                        //Debug.DrawLine(middlePointDrop, b.pos, Color.yellow);
                        //Debug.DrawLine (quarterPointTop, b.pos, Color.yellow);
                        //Debug.DrawLine (quarterPointBot, a.pos, Color.yellow);
                        //Debug.DrawLine (corner, b.pos, Color.yellow);

                        if (Physics2D.Linecast(quarterPointTop, b.pos, groundLayer) ||
                            Physics2D.Linecast(middlePointDrop, b.pos, groundLayer) ||
                            a.pos.y > b.pos.y + blockSize + groundNodeHeight && Physics2D.Linecast(corner, b.pos, groundLayer) ||
                            Physics2D.Linecast(quarterPointBot, a.pos, groundLayer))
                        {
                            hitTest = false;
                        }
                        if (hitTest)
                        {
                            a.neighbours.Add(b);
                        }
                    }
                }

                //END TESTING
            }
        }
    }

    //get nearest node ladder, ground. Useful for finding start and end points of the path
    private pathNode getNearestNode(Vector3 obj)
    {
        float dist = float.MaxValue;
        pathNode node = null;

        for (int i = 0; i < groundNodes.Count; i++)
        {
            if (groundNodes[i].neighbours.Count > 0 && obj.y > groundNodes[i].pos.y && Mathf.Abs(obj.x - groundNodes[i].pos.x) < blockSize
                /*only find ground nodes that are within 4f*/&& obj.y - groundNodes[i].pos.y < 4f)
            {
                float temp = Vector3.Distance(obj, (Vector3)groundNodes[i].pos);
                if (dist > temp)
                {
                    dist = temp; node = groundNodes[i];
                }
            }
        }

        return node;
    }

    private pathNode getNearestGroundNode(Vector3 obj)
    {
        float dist = float.MaxValue;
        pathNode node = null;

        for (int i = 0; i < groundNodes.Count; i++)
        {
            if (groundNodes[i].neighbours.Count > 0)
            {
                float temp = Vector3.Distance(obj, (Vector3)groundNodes[i].pos);
                if (dist > temp)
                {
                    if (obj.y >= groundNodes[i].pos.y && Mathf.Abs(obj.x - groundNodes[i].pos.x) < blockSize)
                    {
                        dist = temp; node = groundNodes[i];
                    }
                }
            }
        }
        return node;
    }


    //Used when reconstructing pathnode connections
    List<pathNode> attachedJumpNodes(List<pathNode> pGround)
    {

        List<pathNode> returnNodes = new List<pathNode>();
        for (int i = 0; i < pGround.Count; i++)
        {

            returnNodes.AddRange(pGround[i].createdJumpNodes);
        }
        return returnNodes;
    }

    List<pathNode> attachedFallNodes(List<pathNode> pGround)
    {

        List<pathNode> returnNodes = new List<pathNode>();
        for (int i = 0; i < pGround.Count; i++)
        {

            returnNodes.AddRange(pGround[i].createdFallNodes);
        }
        return returnNodes;
    }

    public class PathData
    {
        public GameObject character;
        public bool passed = false;
        public Vector3 charPos, end;
        public float jump;
        public List<instructions> instr = null;

        //abilities
        public bool canMove;
        public bool canJump;
        public bool canFall;

        public PathData(GameObject pC, Vector3 pE, float jumpHeight, bool cMove, bool cJump, bool cFall)
        {
            character = pC;
            charPos = pC.transform.position;
            end = pE;
            jump = jumpHeight;

            canMove = cMove;
            canJump = cJump;
            canFall = cFall;
        }
    }

    [System.Serializable]
    public class nodeWeight
    {

        public float groundNode = 1f;
        public float jumpNode = 9.2f;
        public float fallNode = 1f;


        public float GetNodeWeightByString(string nodeType)
        {
            switch (nodeType)
            {
                case "walkable": return groundNode;
                case "jump": return jumpNode;
                case "fall": return fallNode;
            }
            return 0f;
        }

        public Color GetNodeColorByString(string nodeType)
        {
            switch (nodeType)
            {
                case "walkable": return Color.yellow;
                case "jump": return Color.blue;
                case "fall": return Color.black;
                case "climb": return Color.cyan;
            }
            return Color.white;
        }
    }
}

//Accessible classes from other scripts below

public class pathNode
{
    public Vector3 pos;
    public string type;
    public float realHeight = 0f;
    public float height = 0f;

    public float h = 0f; //estimated distance from finish
    public float g = 0f; //cost to get to node
    public float c = 0f; //cost of node

    public float f = 0f; //nodeValue

    public pathNode parent = null;
    public GameObject gameObject;

    public pathNode spawnedFrom = null; //the node that created this.
    public List<pathNode> createdJumpNodes = new List<pathNode>();
    public List<pathNode> createdFallNodes = new List<pathNode>();

    public List<pathNode> neighbours = new List<pathNode>();

    public pathNode(string typeOfNode, Vector3 position)
    {
        pos = position;
        type = typeOfNode;
    }
}

public class instructions
{
    public Vector3 pos = Vector3.zero;
    public string order = "none";

    public instructions(Vector3 position, string pOrder)
    {
        pos = position;
        order = pOrder;
    }
}