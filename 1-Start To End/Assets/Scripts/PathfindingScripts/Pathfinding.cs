using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Pathfinding : MonoBehaviour
{

    /*
    ****
    ****
    /**** This script is subject to change in future versions. There are definitely some areas in this script that need to be edited for your game and it's pretty sloppy.
    ****
    ****
    */

    LayerMask groundLayer;
    LayerMask ladderLayer;
    LayerMask portalLayer;
    LayerMask onewayLayer;
    //LayerMask groundAndOnewayLayer;

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
    private Thread t;

    public List<pathNode> getGroundAndLadders()
    {
        List<pathNode> gl = new List<pathNode>();
        gl.AddRange(groundNodes);
        gl.AddRange(ladderNodes);
        return gl;
    }
    private List<pathNode> nodes = new List<pathNode>();
    private List<pathNode> groundNodes = new List<pathNode>();
    private List<pathNode> ladderNodes = new List<pathNode>();
    private List<pathNode> portalNodes = new List<pathNode>();

    public List<threadLock> orders = new List<threadLock>();
    public List<threadLock> readyOrders = new List<threadLock>();

    public nodeWeight nodeWeights;

    public bool debugTools = false; /*Pauses game on runtime and displays pathnode connections*/

    void Awake()
    {


        groundLayer = LayerManager.instance.groundLayer; //1 << 0;//LayerMask.NameToLayer("ground");
        ladderLayer = LayerManager.instance.ladderLayer; //1 << 1;// LayerMask.NameToLayer("ladder");
        portalLayer = LayerManager.instance.portalLayer;//1 << 2;// LayerMask.NameToLayer("portal");
        onewayLayer = LayerManager.instance.onewayLayer;//1 << 3;//LayerMask.NameToLayer("oneway");
        //groundAndOnewayLayer = LayerManager.access.groundLayer | LayerManager.access.onewayLayer; //groundLayer | onewayLayer;

    }

    void Start()
    {
        //Debug tools do not work in awake!
        CreateNodeMap();
    }

    void Update()
    {
        DeliverPathfindingInstructions();
        MakeThreadDoWork();

        if (Input.GetKey(KeyCode.B))
        {

            Vector3 temp = (Camera.main.ScreenToWorldPoint(Input.mousePosition));
            Vector2 cPos = new Vector2(temp.x, temp.y);
            RaycastHit2D hit = Physics2D.Raycast(cPos, Vector2.zero, 0f, groundLayer);
            if (hit.collider)
            {

                hit.collider.enabled = false;
                RefreshAreaAroundBlock(hit.collider.transform.gameObject, true);
                Destroy(hit.collider.transform.gameObject);
            }
        }
        if (Input.GetKey(KeyCode.T))
        {

            Vector3 temp = (Camera.main.ScreenToWorldPoint(Input.mousePosition));
            temp.x = Mathf.FloorToInt(temp.x) + 0.5f;
            temp.y = Mathf.FloorToInt(temp.y) + 0.5f;
            Vector2 cPos = new Vector2(temp.x, temp.y);
            RaycastHit2D hit = Physics2D.Raycast(cPos, Vector2.zero, 0f, groundLayer);
            if (!hit.collider)
            {

                CreateBlockCalled(temp);
            }
        }
    }

    void CreateNodeMap()
    {

        nodes = new List<pathNode>();
        groundNodes = new List<pathNode>();
        ladderNodes = new List<pathNode>();
        portalNodes = new List<pathNode>();

        List<GameObject> groundObjects = new List<GameObject>();
        List<GameObject> onewayObjects = new List<GameObject>();
        List<GameObject> ladderObjects = new List<GameObject>();
        List<GameObject> portalObjects = new List<GameObject>();

        //oneway.value == 1 << hit.transform.gameObject.layer
        //Find all children of tile parent
        foreach (Transform child in _currentMap.transform)
        {
            // Debug.Log(child.gameObject.layer + " " + (1 << LayerMask.NameToLayer("ground")));
            if (1 << child.gameObject.layer == groundLayer.value)
            {
                groundObjects.Add(child.gameObject);
            }
            else if (1 << child.gameObject.layer == ladderLayer.value)
            {
                ladderObjects.Add(child.gameObject);
            }
            else if (1 << child.gameObject.layer == portalLayer.value)
            {
                portalObjects.Add(child.gameObject);
            }
            else if (1 << child.gameObject.layer == onewayLayer.value)
            {
                onewayObjects.Add(child.gameObject);
            }
        }

        FindGroundNodes(groundObjects);
        Debug.Log(groundNodes.Count);
        FindOnewayNodes(onewayObjects);
        FindLadderNodes(ladderObjects);
        FindFallNodes(groundNodes); //@param list of nodes to search (tiles)
        FindJumpNodes(groundNodes);
        FindPortalNodes(portalObjects);

        GroundNeighbors(groundNodes, groundNodes);

        LadderNeighbors(ladderNodes, ladderNodes, false); //manaage ladder nodes like ground nodes *************TODO
        LadderNeighbors(groundNodes, ladderNodes, true);
        LadderNeighbors(ladderNodes, groundNodes, true);

        //manaage ladder nodes like ground nodes *************TODO
        PortalNeighbors(portalNodes, portalNodes, true); //portalNodes must be in position 1
        PortalNeighbors(portalNodes, groundNodes, false);

        JumpNeighbors(attachedJumpNodes(groundNodes), groundNodes); //CHANGE this function to find all jump nodes attached to ground nodes **********TODO
        FallNeighbors(attachedFallNodes(groundNodes), groundNodes);  //CHANGE this function to find all fall nodes attached to ground nodes **********TODO

        if (debugTools)
        {
            Debug.Break();
        }
    }

    public void RequestPathInstructions(GameObject character, Vector3 location, bool usingLadder, float jumpH,/*char abilities*/ bool movement, bool jump, bool ladder, bool fall, bool portal)
    {

        bool replaced = false;
        threadLock newLocker = new threadLock(character, location, usingLadder, jumpH, movement, jump, ladder, fall, portal);

        for (int i = 1; i < orders.Count; i++)
        {
            if (orders[i].character == character)
            {
                orders[i] = newLocker; replaced = true; break;
            }
        }

        if (!replaced)
        {

            orders.Add(newLocker);
        }
    }

    public void FindPath(object threadLocker)
    {//GameObject character, Vector3 location) {

        threadLock a = (threadLock)threadLocker;
        Vector3 character = a.charPos;
        Vector3 location = a.end;
        float characterJump = a.jump;

        List<instructions> instr = new List<instructions>();

        List<pathNode> openNodes = new List<pathNode>();
        List<pathNode> closedNodes = new List<pathNode>();
        List<pathNode> pathNodes = new List<pathNode>();

        ResetLists(); //sets parent to null

        pathNode startNode = new pathNode("", Vector3.zero);
        if (a.usingLadder) { startNode = getNearestLadderNode(character); } else { startNode = getNearestGroundNode(character); }

        pathNode endNode = getNearestNode(location);

        /*if a point couldnt be found or if character can't move cancel path*/
        if (endNode == null || startNode == null || !a.canMove)
        {
            //Debug.Log("endpoint: " + endNode + ", startpoint: " + startNode);
            a.passed = false;
            a.instr = instr;
            readyOrders.Add(a);
            return;
            //purgeCurrentPath();
        }

        startNode.g = 0;
        startNode.f = Vector3.Distance(startNode.pos, endNode.pos);

        openNodes.Add(startNode);

        //evaluateNode (startNode);

        pathNode currentNode = new pathNode("0", Vector3.zero);
        while (openNodes.Count > 0)
        {
            float lowestScore = float.MaxValue;
            for (int i = 0; i < openNodes.Count; i++)
            {
                if (openNodes[i].f < lowestScore)
                {
                    currentNode = openNodes[i]; lowestScore = currentNode.f;
                }
            }
            if (currentNode == endNode) { closedNodes.Add(currentNode); break; }
            else
            {
                closedNodes.Add(currentNode);
                openNodes.Remove(currentNode);
                if (currentNode.type != "jump" || (currentNode.type == "jump"
                    && Mathf.Abs(currentNode.realHeight - characterJump) < jumpHeightIncrement * 0.92) && characterJump <= currentNode.realHeight + jumpHeightIncrement * 0.08)
                {
                    for (int i = 0; i < currentNode.neighbours.Count; i++)
                    {
                        //check if node can be used by character
                        if (!a.canJump && currentNode.neighbours[i].type == "jump") { continue; }
                        if (!a.canClimb && currentNode.neighbours[i].type == "climb") { continue; }
                        if (!a.canFall && currentNode.neighbours[i].type == "fall") { continue; }
                        if (!a.canPortal && currentNode.neighbours[i].type == "portal") { continue; }

                        if (currentNode.neighbours[i].parent == null)
                        {

                            currentNode.neighbours[i].g = currentNode.neighbours[i].c + currentNode.g;
                            currentNode.neighbours[i].h = Vector3.Distance(currentNode.neighbours[i].pos, endNode.pos);
                            if (currentNode.neighbours[i].type == "jump") { currentNode.neighbours[i].h += currentNode.neighbours[i].realHeight; }
                            currentNode.neighbours[i].f = currentNode.neighbours[i].g + currentNode.neighbours[i].h;
                            currentNode.neighbours[i].parent = currentNode;
                            openNodes.Add(currentNode.neighbours[i]);
                        }
                        else
                        {
                            if (currentNode.g + currentNode.neighbours[i].c < currentNode.neighbours[i].g)
                            {
                                currentNode.neighbours[i].g = currentNode.neighbours[i].c + currentNode.g;
                                currentNode.neighbours[i].f = currentNode.neighbours[i].g + currentNode.neighbours[i].h;
                                currentNode.neighbours[i].parent = currentNode;
                            }
                        }
                    }
                }
            }
        }

        for (int i = 0; i < 700; i++)
        {
            if (i > 600) { Debug.Log("somethingwrong"); }
            pathNodes.Add(currentNode);

            if (currentNode.parent != null)
            {
                currentNode = currentNode.parent;
            }
            else { break; }
            if (currentNode == startNode)
            {
                pathNodes.Add(startNode);
                break;
            }
        }

        if (pathNodes[0] != endNode)
        {
            //Debug.Log ("s node == e node, null instructions OR find closest h node and travel to it");
            //might also want to include an option for h node must be less than end node
            a.passed = false;
        }
        else { a.passed = true; }

        pathNodes.Reverse();
        for (int i = 0; i < pathNodes.Count; i++)
        {
            instructions temp = new instructions(pathNodes[i].pos, pathNodes[i].type);
            instr.Add(temp);
        }

        a.instr = instr;
        readyOrders.Add(a);
    }

    public void DeliverPathfindingInstructions()
    {

        for (int i = 0; i < readyOrders.Count; i++)
        {

            if (readyOrders[i].character)
            {

                if (readyOrders[i].character.transform.GetComponent<PathfindingAgent>() != null)
                {
                    readyOrders[i].character.transform.GetComponent<PathfindingAgent>().ReceivePathInstructions(readyOrders[i].instr, readyOrders[i].passed);
                }
                else
                {
                    //this is a temporary fix for rigidbody pathfinding
                    //orders[i].character.transform.GetComponent<rigidCharacter> ().receivePathInstructions (orders[i].instr);
                }
            }
        }
        readyOrders = new List<threadLock>();
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


        LadderNeighbors(collection, ladderNodes, true);
        LadderNeighbors(ladderNodes, collection, true); //ladders will contain too many ground neighbors..... need to fix this

        PortalNeighbors(portalNodes, groundNodes, false);

        //portalNeighbors(portalNodes, collection, false); //same with portals.......
        //WE NEED TO REMAKE ALL JUMP/FALL NODES FROM THE TILES 


        JumpNeighbors(attachedJumpNodes(collection), largerCollection);
        FallNeighbors(attachedFallNodes(collection), largerCollection);



        if (Input.GetKey(KeyCode.LeftShift)) { Debug.Break(); } //make node neighbor mesh visible
    }

    private void FindPortalNodes(List<GameObject> objects)
    {
        //GameObject[] objects = GameObject.FindGameObjectsWithTag("portal");

        for (int i = 0; i < objects.Count; i++)
        {
            Vector3 portal = objects[i].transform.position; //ground.y += blockSize * 0.5f + blockSize * groundNodeHeight;
            pathNode newPortalNode = new pathNode("portal", portal);
            newPortalNode.gameObject = objects[i];

            portalNodes.Add(newPortalNode);

            newPortalNode.c = nodeWeights.GetNodeWeightByString(newPortalNode.type);
            nodes.Add(newPortalNode);

            //Debug.Log (newPortalNode..name);
        }
    }
    private void PortalNeighbors(List<pathNode> fromNodes, List<pathNode> toNodes, bool onlyPortals)
    {
        //float distanceBetween = blockSize + groundMaxWidth;
        float distanceForGround = blockSize * 1.5f + 0.1f;
        float maxXDistance = blockSize * 1f;
        for (int i = 0; i < fromNodes.Count; i++)
        {
            pathNode a = fromNodes[i];

            if (a.gameObject)
            {
                GameObject aCheck = a.gameObject.transform.GetComponent<Portal>().connectedTo;
                if (aCheck != null)
                {
                    for (int t = 0; t < toNodes.Count; t++)
                    {
                        pathNode b = toNodes[t];

                        //PREFORM A - > B TESTING HERE

                        if (onlyPortals)
                        {
                            if (aCheck == b.gameObject)
                            {
                                a.neighbours.Add(b);
                                if (debugTools)
                                {
                                    Debug.DrawLine(a.pos, b.pos, Color.cyan);
                                }
                            }
                        }
                        else
                        {
                            //testing distance between nodes
                            if (Mathf.Abs(a.pos.x - b.pos.x) < maxXDistance &&
                                Vector3.Distance(a.pos, b.pos) < distanceForGround)
                            {
                                //testing collision between nodes
                                //if (!Physics2D.Linecast(a.pos, b.pos, collisionMask)) {
                                a.neighbours.Add(b);
                                b.neighbours.Add(a);
                                if (debugTools)
                                {
                                    Debug.DrawLine(a.pos, b.pos, Color.cyan);
                                }
                                //}
                            }
                        }

                        //END TESTING
                    }
                }
            }
        }
    }

    private void FindLadderNodes(List<GameObject> objects)
    {
        //GameObject[] objects = GameObject.FindGameObjectsWithTag("ladder");

        for (int i = 0; i < objects.Count; i++)
        {
            Vector3 ladder = objects[i].transform.position; //ground.y += blockSize * 0.5f + blockSize * groundNodeHeight;
            pathNode newLadderNode = new pathNode("climb", ladder);
            ladderNodes.Add(newLadderNode);

            newLadderNode.c = nodeWeights.GetNodeWeightByString(newLadderNode.type);
            nodes.Add(newLadderNode);
        }
    }
    private void LadderNeighbors(List<pathNode> fromNodes, List<pathNode> toNodes, bool includesGround)
    {
        float distanceBetween = blockSize + groundMaxWidth;
        float distanceForGround = blockSize * 0.5f + 0.2f;
        float maxXDistance = blockSize * 0.501f;
        for (int i = 0; i < fromNodes.Count; i++)
        {
            pathNode a = fromNodes[i];

            for (int t = 0; t < toNodes.Count; t++)
            {
                pathNode b = toNodes[t];

                //PREFORM A - > B TESTING HERE

                //testing distance between nodes
                if (Mathf.Abs(a.pos.x - b.pos.x) < maxXDistance &&

                    ((!includesGround && Vector3.Distance(a.pos, b.pos) < distanceBetween)
                 || (includesGround && Vector3.Distance(a.pos, b.pos) < distanceForGround))

                    )
                {
                    //testing collision between nodes
                    //if (!Physics2D.Linecast(a.pos, b.pos, collisionMask)) {
                    a.neighbours.Add(b);
                    if (debugTools)
                    {
                        Debug.DrawLine(a.pos, b.pos, Color.red);
                    }
                    //}
                }

                //END TESTING
            }
        }
    }

    private void FindGroundNodes(List<GameObject> objects)
    {
        nodes = new List<pathNode>();
        //GameObject[] objects = GameObject.FindGameObjectsWithTag("ground");

        for (int i = 0; i < objects.Count; i++)
        {
            Vector3 ground = objects[i].transform.position; ground.y += blockSize * 0.5f + blockSize * groundNodeHeight;
            pathNode newGroundNode = new pathNode("walkable", ground);
            groundNodes.Add(newGroundNode);

            newGroundNode.c = nodeWeights.GetNodeWeightByString(newGroundNode.type);
            nodes.Add(newGroundNode);

            newGroundNode.gameObject = objects[i];
        }

        /*TEMP FOR ONEWAY PLATFORMS*/
        // objects = GameObject.FindGameObjectsWithTag("oneway");


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
                        if (debugTools)
                        {
                            Debug.DrawLine(a.pos, b.pos, Color.red);
                        }

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
                if (debugTools)
                {
                    Debug.DrawLine(a.pos, a.spawnedFrom.pos, Color.red);
                }

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
                                if (debugTools)
                                {
                                    Debug.DrawLine(a.pos, b.pos, Color.blue);
                                }
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
                if (debugTools)
                {
                    Debug.DrawLine(a.spawnedFrom.pos, a.pos, Color.blue);
                }

                //FALL NODES REQUIRE TESTING
                //CHARACTER WIDTH!
                //probably a similar formula to jumpnode neighbors
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
                            if (debugTools)
                            {
                                Debug.DrawLine(a.pos, b.pos, Color.black);
                            }
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

        for (int i = 0; i < ladderNodes.Count; i++)
        {
            if (ladderNodes[i].neighbours.Count > 0 && obj.y > ladderNodes[i].pos.y && Mathf.Abs(obj.x - ladderNodes[i].pos.x) < blockSize)
            {
                float temp = Vector3.Distance(obj, (Vector3)ladderNodes[i].pos);
                if (dist > temp)
                {
                    dist = temp; node = ladderNodes[i];
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
    private pathNode getNearestLadderNode(Vector3 obj)
    {
        float dist = float.MaxValue;
        pathNode node = null;

        for (int i = 0; i < ladderNodes.Count; i++)
        {
            if (ladderNodes[i].neighbours.Count > 0)
            {
                float temp = Vector3.Distance(obj, (Vector3)ladderNodes[i].pos);
                if (dist > temp && Mathf.Abs(obj.x - ladderNodes[i].pos.x) < blockSize) { dist = temp; node = ladderNodes[i]; }
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

    public void MakeThreadDoWork()
    {
        if ((orders.Count > 0 && t == null) || (orders.Count > 0 && !t.IsAlive))
        {
            t = new Thread(new ParameterizedThreadStart(FindPath));
            t.IsBackground = true;
            t.Start(orders[0]);
            orders.RemoveAt(0);
        }
    }

    private void OnDrawGizmos()
    {
        if (debugTools)
        {

            for (int i = 0; i < nodes.Count; i++)
            {
                Gizmos.color = nodeWeights.GetNodeColorByString(nodes[i].type);
                Gizmos.DrawSphere(nodes[i].pos, 0.12f);
            }
        }
    }

    public class threadLock
    {
        public GameObject character;
        public bool passed = false;
        public bool usingLadder;
        public Vector3 charPos, end;
        public float jump;
        public List<instructions> instr = null;

        //abilities
        public bool canMove;
        public bool canJump;
        public bool canClimb;
        public bool canFall;
        public bool canPortal;

        public threadLock(GameObject pC, Vector3 pE, bool uL, float jumpHeight, bool cMove, bool cJump, bool cClimb, bool cFall, bool cPortal)
        {
            character = pC;
            usingLadder = uL;
            charPos = pC.transform.position;
            end = pE;
            jump = jumpHeight;

            canMove = cMove;
            canJump = cJump;
            canClimb = cClimb;
            canFall = cFall;
            canPortal = cPortal;
        }
    }

    [System.Serializable]
    public class nodeWeight
    {

        public float groundNode = 1f;
        public float jumpNode = 9.2f;
        public float fallNode = 1f;
        public float climbNode = 3f;
        public float portalNode = 0f;

        public float GetNodeWeightByString(string nodeType)
        {
            switch (nodeType)
            {
                case "walkable": return groundNode;
                case "jump": return jumpNode;
                case "fall": return fallNode;
                case "climb": return climbNode;
                case "portal": return portalNode;
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

    public float f = 0f; //estimated distance from finish
    public float g = 0f; //cost to get to node
    public float c = 0f; //cost of node

    public float h = 0f; //nodeValue

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