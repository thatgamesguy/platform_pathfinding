using System.Collections.Generic;
using UnityEngine;

public class AiController : MonoBehaviour
{
    /*
    ****
    ****
    /**** This script is subject to change in future versions and more than likely users will want to rewrite it or change it to fit their needs.
    ****
    ****
    */

    public enum ai_state { none, groundpatrol, pathfinding, chase, flee, pathfindChase } /*Add custom AI states here!*/

    public ai_state state = ai_state.pathfinding;

    private Character _characterScript;
    private CharacterController2D _controller;
    private PathfindingAgent _pathAgent;
    public static Pathfinding _pathScript;
    [System.NonSerialized]
    public TextMesh _behaviourText;

    private float direction = 1;
    private float fleeTimer = 0.5f;
    private float fFleeTimer;

    public static GameObject player;
    private bool destroy = false;

    private void SetPathingTargetFlee(float directionX)
    {
        Vector3 positionT = transform.position;

        for (int i = 0; i < 2; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, new Vector2(3 * directionX, 2.9f), 4.5f, _controller.collisionMask);
            positionT = transform.position;
            if (hit)
            {
                hit = Physics2D.Raycast(transform.position, new Vector2(1 * directionX, 0), 1f, _controller.collisionMask);
                if (hit)
                {
                    directionX *= -1f;
                }
                else
                {
                    positionT.x += 2f * directionX; break;
                }
            }
            else
            {
                positionT.x += 3f * directionX;
                positionT.y += 2.9f;
                break;
            }
        }
        _pathAgent.RequestPath(positionT);
    }

    private bool PlayerInRange(float range, bool raycastOn)
    {
        if (player && Vector3.Distance(player.transform.position, transform.position) < range)
        {
            if (raycastOn && !Physics2D.Linecast(transform.position, player.transform.position, _controller.collisionMask))
            {
                return true;
            }
            else if (!raycastOn)
            {
                return true;
            }
        }
        return false;
    }

    private void Awake()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        _controller = GetComponent<CharacterController2D>();
        _characterScript = GetComponent<Character>();
        _pathAgent = GetComponent<PathfindingAgent>();

        if (_pathScript == null) { _pathScript = GameObject.FindGameObjectWithTag("GameController").GetComponent<Pathfinding>(); }

        _behaviourText = transform.Find("BehaviourText").GetComponent<TextMesh>();
        switch (state)
        {
            case ai_state.flee: _behaviourText.text = "Flee"; break;
            case ai_state.groundpatrol: _behaviourText.text = "Ground Patrol"; break;

            default: _behaviourText.text = ""; break;
        }
    }

    public bool NeedsPathfinding()
    {
        if (state == ai_state.pathfinding || state == ai_state.flee || state == ai_state.chase || state == ai_state.pathfindChase) { return true; }
        _pathAgent.CancelPathing();
        return false;
    }

    public void GetInput(ref Vector3 velocity, ref Vector2 input, ref bool jumpRequest)
    {
        if (_characterScript.ledgegrab.ledgeGrabbed) { _characterScript.ledgegrab.StopLedgeGrab(); }

        switch (state)
        {
            case ai_state.none: break;
            case ai_state.groundpatrol: GroundPatrol(ref input); break;
            case ai_state.flee: Flee(); break;
            case ai_state.pathfindChase: PathfindChase(); break;
            case ai_state.chase: Chase(); break; //add this line in to the GetInput method
            default: break;
        }

        if (state == ai_state.pathfinding || state == ai_state.flee || state == ai_state.chase || state == ai_state.pathfindChase)
        {
            _pathAgent.AiMovement(ref velocity, ref input, ref jumpRequest);
        }
    }

    /*Destroy object on lateupdate to avoid warning errors of objects not existing*/
    void LateUpdate()
    {
        if (destroy) { Destroy(gameObject); }
    }

    /*gets called from pathagent when character finishes navigating path*/
    public void PathCompleted()
    {
        switch (state)
        {
            case ai_state.pathfinding: _behaviourText.text = ""; break;
            case ai_state.pathfindChase: destroy = true; break; /*when character reaches house, destroy on next update*/
            case ai_state.chase: _behaviourText.text = "Chase"; break;
            case ai_state.flee: _behaviourText.text = "Flee"; break;
        }
    }

    /*gets called from pathagent when character beings navigating path*/
    public void PathStarted()
    {
        switch (state)
        {
            case ai_state.pathfinding: _behaviourText.text = "Pathfinding"; break;
            case ai_state.chase: _behaviourText.text = "Chase"; break;
            case ai_state.pathfindChase: _behaviourText.text = "Pathfinding"; break;
        }
    }

    private void PathfindChase()
    {
        //Switch to chase if player in range
        if (PlayerInRange(6f, true))
        {
            _pathAgent.pathfindingTarget = player;
            state = ai_state.chase;
            _behaviourText.text = "Chase";
        }
    }

    private void Chase()
    { //Add this method into AiController

        _pathAgent.pathfindingTarget = player;
        state = ai_state.chase;
        _behaviourText.text = "Chase";

    }

    private void Flee()
    {
        fFleeTimer += Time.deltaTime;
        if (fFleeTimer >= fleeTimer)
        {
            fFleeTimer = 0;
            if (_pathAgent.GetNodesFromCompletion() > 2) { return; }
            if (!_characterScript.ladder.isClimbing && !PlayerInRange(7f, true) && _controller.collisions.below)
            {
                state = ai_state.groundpatrol;
                _behaviourText.text = "Ground Patrol";
                _pathAgent.CancelPathing(); return;
            }

            float radius = 8f; //Max range for random node
            float innerRadius = 3f; //Min range for random node

            List<pathNode> nodes = _pathScript.getGroundAndLadders();
            List<pathNode> nodesInArea = new List<pathNode>();

            float slopeX = (player.transform.position.y - transform.position.y) * 0.1f; //inverse 1 (slope), x and y are purposefuly backwards.
            float slopeY = -(player.transform.position.x - transform.position.x);
            Vector2 a = new Vector2(transform.position.x + slopeX * radius, transform.position.y + slopeY);
            Vector2 b = new Vector2(transform.position.x - slopeX * radius, transform.position.y - slopeY);
            Debug.DrawLine(a, b, Color.red, 5f);

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].neighbours.Count > 2 &&
                    ((b.x - a.x) * (nodes[i].pos.y - a.y) - (b.y - a.y) * (nodes[i].pos.x - a.x)) > 0 &&
                    Mathf.Pow(transform.position.x - nodes[i].pos.x, 2) + Mathf.Pow(transform.position.y - nodes[i].pos.y, 2) <= Mathf.Pow(radius, 2) &&
                    Mathf.Pow(transform.position.x - nodes[i].pos.x, 2) + Mathf.Pow(transform.position.y - nodes[i].pos.y, 2) >= Mathf.Pow(innerRadius, 2))
                {
                    nodesInArea.Add(nodes[i]);
                }
            }

            if (nodesInArea.Count > 0)
            {
                Vector3 test = nodesInArea[Random.Range(0, nodesInArea.Count - 1)].pos;
                test.y += 0.5f;
                _pathAgent.RequestPath(test);
                _behaviourText.text = "Flee";
            }
            else
            {
                //could potentially run back into character, -- we can't run any further away in direction we want, so we pick a random point.

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].neighbours.Count > 2 &&
                        Mathf.Pow(transform.position.x - nodes[i].pos.x, 2) + Mathf.Pow(transform.position.y - nodes[i].pos.y, 2) <= Mathf.Pow(radius, 2) &&
                        Mathf.Pow(transform.position.x - nodes[i].pos.x, 2) + Mathf.Pow(transform.position.y - nodes[i].pos.y, 2) >= Mathf.Pow(innerRadius, 2))
                    {
                        nodesInArea.Add(nodes[i]);
                    }
                }
                if (nodesInArea.Count > 0)
                {
                    Vector3 test = nodesInArea[Random.Range(0, nodesInArea.Count - 1)].pos;
                    test.y += 0.5f;
                    _pathAgent.RequestPath(test);
                    _behaviourText.text = "Flee";
                }
                else
                {

                    _behaviourText.text = "Can't Flee & Scared.";

                }
            }
        }
    }

    private void GroundPatrol(ref Vector2 input)
    {
        //Switch to flee if player in range
        if (PlayerInRange(6f, true))
        {
            state = ai_state.flee;

        }
        if (direction == 1 && (_controller.collisions.right || (!_controller.rightGrounded && _controller.collisions.below)))
        {
            direction = -1;
        }
        else if (direction == -1 && (_controller.collisions.left || (!_controller.leftGrounded && _controller.collisions.below)))
        {
            direction = 1;
        }
        if (_characterScript.ladder.isClimbing) { _characterScript.ladder.StopClimbingLadder(); }

        input.x = direction;
    }
}