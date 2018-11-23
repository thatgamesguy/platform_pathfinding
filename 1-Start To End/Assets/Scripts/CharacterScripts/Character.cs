using UnityEngine;
using System.Collections.Generic;
using WindowsInput;

[RequireComponent(typeof(CharacterController2D))]
[RequireComponent(typeof(Rigidbody2D))]       /*Used for collision detection*/
public class Character : MonoBehaviour
{
    public GameObject target;

    [System.NonSerialized]
    public GameObject _graphics;
    [System.NonSerialized]
    public AiController _ai;
    BoxCollider2D _box;
    [System.NonSerialized]
    public Animator _anim;
    Rigidbody2D _body;                        /*Used for collision detection*/
    PathfindingAgent _pathingAgent;
    CharacterController2D _controller;

    public moveStats movement;
    public jumpStats jump;
    public wallSlideStats wallslide;
    public jetPackStats jetpack;
    public ladderClimbStats ladder;
    public ledgeGrabStats ledgegrab;
    public portalStats portal;

    [System.NonSerialized]
    public float gravity;                     /*is calculated automatically inside jump.UpdateJumpHeight, future versions may contain optional jump/gravity/apex customizations*/
    Vector3 velocity;

    private bool facingRight = true;          /*determines the direction character is facing*/
    public bool jumped = false;              /*used for detecting if jump key was pressed (also used in ai)*/
    public bool isAiControlled = false;      /*allows ai to take control over inputs*/
    public bool playerControlled = false;    /*allows input by player*/
    public bool teleport = false;            /*debugging bool to teleport character and set control to player*/
    public bool rightClickPathFind = false;  /*allows player to search for path by left click*/
    public bool FallNodes = true;            /*true-- Allows the pathfinding agent to use 'fall' nodes*/


    void Awake()
    {
        _controller = GetComponent<CharacterController2D>();
        _body = GetComponent<Rigidbody2D>();
        _box = GetComponent<BoxCollider2D>();
        _pathingAgent = GetComponent<PathfindingAgent>();
        _ai = GetComponent<AiController>();
        _anim = transform.Find("Graphics").GetComponent<Animator>();
        _graphics = transform.Find("Graphics").gameObject; /*useful for preventing things from flipping when character is facing left*/

        /*allow movement abilities to access character script*/
        ladder.setCharacter(this);
        ledgegrab.setCharacter(this);
        jump.setCharacter(this);

        _body.isKinematic = true;

    }

    void Start()
    {
        jump.UpdateJumpHeight();

        if (target)
        {
            isAiControlled = true; //allow character to be controlled by AI for when we recieve pathfinding
            _ai.state = AiController.ai_state.pathfinding; //set character AI type to pathfinding
            _pathingAgent.RequestPath(target.transform.position + Vector3.up);
        }
    }

    void Update()
    {

        if (teleport && Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl))
        {
            Vector3 mouseP = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            transform.position = new Vector3(mouseP.x, mouseP.y, transform.position.z);
            velocity.x = 0; velocity.y = 0;
            _pathingAgent.CancelPathing();

            //force control to player
            isAiControlled = false;
            playerControlled = true;

            ledgegrab.StopLedgeGrab();
            ladder.StopClimbingLadder();
        }

        if (playerControlled)
        {

            if (Input.GetKeyDown(KeyCode.Space) && !ladder.isClimbing && !ledgegrab.ledgeGrabbed)
            {
                if (jetpack.fJetpackFuelTime < jetpack.jetpackFuelTime)
                {
                    jumped = true;
                    jetpack.jetpack = true;

                }
            }
            if (Input.GetKeyUp(KeyCode.Space))
            {
                jetpack.jetpack = false;
                jetpack.jetpackStartupBool = false;
                jetpack.fJetpackStartupTime = 0f;
            }

        }

        if (rightClickPathFind && Input.GetMouseButtonDown(1))
        { //GetKeyDown(KeyCode.C)) {//
            isAiControlled = true; //allow character to be controlled by AI for when we recieve pathfinding
            _ai.state = AiController.ai_state.pathfinding; //set character AI type to pathfinding
            _pathingAgent.RequestPath(Camera.main.ScreenToWorldPoint(Input.mousePosition)); //request a path and wait for instructions
        }
    }

    void FixedUpdate()
    {

        CooldownTicks();

        Vector2 input = Vector2.zero;
        if (playerControlled)
        {
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (isAiControlled && (input.x != 0 || input.y != 0)) { if (isAiControlled) { _pathingAgent.CancelPathing(); } isAiControlled = false; _ai._behaviourText.text = ""; } /*turns off Ai control to avoid confusion user error*/
        }
        if (isAiControlled)
        {
            _ai.GetInput(ref velocity, ref input, ref jumped);
        }


        //Ledgegrabbing
        if (ledgegrab.ability)
        {
            if ((input.y == -1 || (facingRight != (input.x == 1) && input.x != 0)) && ledgegrab.ledgeGrabbed)
            {
                ledgegrab.StopLedgeGrab();
            }
            if ((input.y == 1 || Input.GetKey(KeyCode.Space)) && ledgegrab.ledgeGrabbed && ledgegrab.ledgeState == 0)
            {

                //start climb process, in fixed, we raycast in the direction we're facing and climbing upwards until no collision, then
                //we move towards direction we're looking until we have ground collision
                ledgegrab.ledgeState = 1;
            }
            if (input.y != -1 && !_controller.collisions.fallingThroughPlatform) { ledgegrab.LedgeGrabState(); }
        }


        //Portal
        if (portal.ability && input.y == 1 && portal.usePortal && portal.canUsePortal)
        {
            transform.position = portal.usePortal.transform.GetComponent<Portal>().connectedTo.transform.position;
            velocity.x *= -0.9f; velocity.y *= -0.9f;
            portal.canUsePortal = false;
        }

        //Ladder
        if (ladder.ability && !ledgegrab.ledgeGrabbed)
        {

            ladder.ClimbLadder(input.y, input.x);
        }

        //Wallslide
        int wallDirX = (_controller.collisions.left) ? -1 : 1;
        bool wallSliding = false;
        if (wallslide.ability && (_controller.collisions.left || _controller.collisions.right) && !_controller.collisions.below && velocity.y < 0)
        {
            wallSliding = true;
            if (velocity.y < -wallslide.wallSlideSpeedMax)
            {
                velocity.y = -wallslide.wallSlideSpeedMax;
            }
            if (wallslide.timeToWallUnstick > 0)
            {
                movement.velocityXSmoothing = 0;
                velocity.x = 0;
                if (input.x != wallDirX && input.x != 0)
                {
                    wallslide.timeToWallUnstick -= Time.deltaTime;
                }
                else
                {
                    wallslide.timeToWallUnstick = wallslide.wallStickTime;
                }
            }
            else
            {
                wallslide.timeToWallUnstick = wallslide.wallStickTime;
            }
        }
        //Wallslide-Jump + Jump
        if (jumped)
        {
            jumped = false;
            if (wallslide.ability && wallSliding)
            {
                if (wallDirX == input.x)
                {
                    velocity.x = -wallDirX * wallslide.wallJumpClimb.x;
                    velocity.y = wallslide.wallJumpClimb.y;
                }
                else if (input.x == 0)
                {
                    velocity.x = -wallDirX * wallslide.wallJumpOff.x;
                    velocity.y = wallslide.wallJumpOff.y;
                }
                else
                {
                    velocity.x = -wallDirX * wallslide.wallLeap.x;
                    velocity.y = wallslide.wallLeap.y;
                }
            }
            if (jump.ability && jump.jumpCount < jump.maxJumps && !wallSliding) { velocity.y = jump.maxJumpVelocity; jump.jumpCount++; }
        }
        //Jump sensitivity
        if (jump.ability && Input.GetKeyUp(KeyCode.Space))
        { //think about adding an isCurrentlyJumping bool that gets reset to false on jetpack or landing or other forces affecting y
            if (velocity.y > jump.minJumpVelocity)
            {
                velocity.y = jump.minJumpVelocity;
            }
        }
        //Jetpack
        if (jetpack.ability)
        {
            if (jetpack.hasFuel && jetpack.jetpackStartupBool)
            {

                jetpack.isJetpacking = true;
                if (velocity.y < 0 && jetpack.fJetpackFuelTime < jetpack.jetpackFuelTime - 1f)
                {
                    velocity.y = 0;
                }

                velocity.y += -1 * gravity * Time.deltaTime * jetpack.jetpackGravityReduction;
                if (velocity.y < jetpack.jetpackMaxSpeed)
                {
                    velocity.y += jetpack.jetpackForce;
                }

                _anim.SetBool("jetpack", true);

            }
            else
            {
                _anim.SetBool("jetpack", false); jetpack.isJetpacking = false;
                if (!jetpack.hasFuel)
                {
                    jetpack.jetpackStartupBool = false; jetpack.fJetpackStartupTime = 0f;
                }
            }
        }


        if (!ladder.isClimbing && !ledgegrab.ledgeGrabbed)
        {

            if (input.x > 0 && !facingRight)
            {
                Flip();
            }
            else if (input.x < 0 && facingRight)
            {
                Flip();
            }

            //Movement-x
            if (movement.ability)
            { //If character has the ability of moving
                float targetVelocityX = input.x * movement.moveSpeed;
                velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref movement.velocityXSmoothing, (_controller.collisions.below) ? movement.accelerationTimeGrounded : jump.accelerationTimeAirborne);
            }
            //Gravity
            if (velocity.y > -jump.maxFallVelocity)
            {
                velocity.y += gravity * Time.deltaTime;
            }
            _controller.Move(velocity * Time.deltaTime, input);
        }

        //animation
        _anim.SetFloat("speed", input.x != 0 ? 1f : 0f);
        _anim.SetBool("grounded", _controller.collisions.below);

        //Grounded + Jump reset
        if (_controller.collisions.below || ladder.isClimbing)
        {
            jump.jumpCount = 0;
        }
        if (_controller.collisions.above || _controller.collisions.below)
        {
            velocity.y = 0;

        }
        else
        { // else if the character falls off an edge, 1 jump is lost
            if (jump.jumpCount == 0) { jump.jumpCount++; }
        }
    }

    private void OnTriggerEnter2D(Collider2D triggeringCollider)
    {

        //Ladder Collision
        if (_controller.ladderCollision.value == 1 << triggeringCollider.gameObject.layer)
        {
            ladder.AddLadder(triggeringCollider.gameObject);
        }
        if (_controller.portalCollision.value == 1 << triggeringCollider.gameObject.layer)
        {
            portal.AddPortal(triggeringCollider.gameObject);
        }

    }

    private void OnTriggerExit2D(Collider2D triggeringCollider)
    {

        if (_controller.ladderCollision.value == 1 << triggeringCollider.gameObject.layer)
        {
            ladder.RemoveLadder(triggeringCollider.gameObject);
        }
        if (_controller.portalCollision.value == 1 << triggeringCollider.gameObject.layer)
        {
            portal.RemovePortal(triggeringCollider.gameObject);
        }
    }

    //changes characters facing direction
    private void Flip()
    {
        // Switch the way the player is labelled as facing
        facingRight = !facingRight;
        // Multiply the player's x local scale by -1
        Vector3 theScale = _graphics.transform.localScale;
        theScale.x *= -1; _graphics.transform.localScale = theScale;
    }

    //keep all timers that must be called in fixed update
    private void CooldownTicks()
    {

        if (jetpack.jetpack)
        {
            if (jetpack.fJetpackStartupTime < jetpack.jetpackStartupTime)
            {
                jetpack.fJetpackStartupTime += Time.deltaTime;
            }
            if (jetpack.fJetpackStartupTime >= jetpack.jetpackStartupTime)
            {
                jetpack.jetpackStartupBool = true;

            }
        }

        if (jetpack.isJetpacking)
        {
            if (jetpack.fJetpackFuelTime < jetpack.jetpackFuelTime)
            {
                jetpack.fJetpackFuelTime += Time.deltaTime;
            }
            else
            {
                jetpack.fJetpackFuelTime = jetpack.jetpackFuelTime;
            }
            if (jetpack.fJetpackFuelTime >= jetpack.jetpackFuelTime)
            {
                jetpack.hasFuel = false;
            }
        }
        else
        {
            if (jetpack.fJetpackFuelTime > 0)
            {
                jetpack.fJetpackFuelTime -= Time.deltaTime * jetpack.jetpackRechargeRate;
                jetpack.hasFuel = true;
            }
            else { jetpack.fJetpackFuelTime = 0; jetpack.hasFuel = true; }
        }


        if (ledgegrab.ledgeCooldownBool)
        {
            ledgegrab.fLedgeCooldown += Time.deltaTime;
            if (ledgegrab.fLedgeCooldown >= ledgegrab.ledgeCooldown)
            {
                ledgegrab.fLedgeCooldown = 0f;
                ledgegrab.ledgeCooldownBool = false;
            }
        }

        if (ladder.isClimbing)
        {

            ladder.fLeaveLadderFeelingCooldown += Time.deltaTime;
            if (ladder.fLeaveLadderFeelingCooldown >= ladder.leaveLadderFeelingCooldown)
            {
                ladder.canLeaveLadderBool = true;
            }
        }

        if (!ladder.isClimbing && !ladder.climbCooldownComplete && ladder.fLadderClimbCooldown < ladder.ladderClimbCooldown)
        {
            ladder.fLadderClimbCooldown += Time.deltaTime;
            if (ladder.fLadderClimbCooldown >= ladder.ladderClimbCooldown)
            {
                ladder.climbCooldownComplete = true;
                ladder.fLadderClimbCooldown = 0f;
            }
        }
        if (!portal.canUsePortal)
        {
            portal.fUsePortalCooldown += Time.deltaTime;
            if (portal.fUsePortalCooldown >= portal.usePortalCooldown) { portal.canUsePortal = true; portal.fUsePortalCooldown = 0f; }
        }

    }

    //Movement Class Abilities
    public class movementEssentials
    {         /*gets inherited by movement abilities*/
        [System.NonSerialized]
        public Character _character;
        public void setCharacter(Character c)
        {
            _character = c;
        }
    }
    [System.Serializable]
    public class jumpStats : movementEssentials
    {

        public bool ability = true;

        [SerializeField]
        private float maxHeight = 2.5f;
        [SerializeField]
        private float minHeight = 2.5f;

        public float maxJumpHeight
        {
            get { return maxHeight; }
            set { maxHeight = value; UpdateJumpHeight(); }
        }

        public float minJumpHeight
        {
            get { return minHeight; }
            set { minHeight = value; UpdateJumpHeight(); }
        }

        public float timeToApex = 0.435f;
        public float accelerationTimeAirborne = 0.09f;
        public float maxFallVelocity = 25f;
        public int maxJumps = 1;
        [System.NonSerialized]
        public int jumpCount = 0;

        [System.NonSerialized]
        public float maxJumpVelocity;
        [System.NonSerialized]
        public float minJumpVelocity;

        //If jump height changes during runtime, this must be called to adjust physics.
        public void UpdateJumpHeight()
        {
            _character.gravity = -(2 * maxHeight) / Mathf.Pow(timeToApex, 2);
            maxJumpVelocity = Mathf.Abs(_character.gravity) * timeToApex;
            minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(_character.gravity) * minHeight);
            //print("Gravity: " + gravity + "  Jump Velocity: " + maxJumpVelocity);
        }
    }
    [System.Serializable]
    public class moveStats
    {

        public bool ability = true;
        public float accelerationTimeGrounded = 0.06f;
        public float moveSpeed = 4.87f;
        [System.NonSerialized]
        public float velocityXSmoothing;
    }
    [System.Serializable]
    public class wallSlideStats
    {

        public bool ability = true;
        public Vector2 wallJumpClimb = new Vector2(5, 9);
        public Vector2 wallJumpOff = new Vector2(5, 9);
        public Vector2 wallLeap = new Vector2(5, 9);
        public float wallSlideSpeedMax = 3.8f;
        public float wallStickTime = .25f;
        [System.NonSerialized]
        public float timeToWallUnstick;
    }
    [System.Serializable]
    public class jetPackStats
    {

        public bool ability = true;
        public bool isJetpacking = false;
        public float jetpackForce = 0.65f;
        public float jetpackGravityReduction = 0.65f;
        public float jetpackMaxSpeed = 4f;
        public float jetpackStartupTime = 0.38f;
        public float jetpackFuelTime = 1.3f;
        public float jetpackRechargeRate = 1f;


        public bool hasFuel = true;

        //[System.NonSerialized]
        public float fJetpackStartupTime = 0.1f;

        public float fJetpackFuelTime = 0;

        // [System.NonSerialized]
        public bool jetpack = false;
        //[System.NonSerialized]
        public bool jetpackStartupBool = false;
    }
    [System.Serializable]
    public class ladderClimbStats : movementEssentials
    {

        public bool ability = true;
        public bool canClimb = false;
        public bool isClimbing = false;
        public float ladderClimbingSpeed = 4.1f;
        public float leaveLadderFeelingCooldown = 0.6f;
        public float ladderClimbCooldown = 0.35f;
        public bool canLeaveLadderBool = false;

        [System.NonSerialized]
        public float fLeaveLadderFeelingCooldown;
        [System.NonSerialized]
        public float fLadderClimbCooldown;
        [System.NonSerialized]
        public bool climbCooldownComplete = true;
        [System.NonSerialized]
        public List<GameObject> currentLadders = new List<GameObject>();

        public void AddLadder(GameObject obj)
        {

            if (!currentLadders.Contains(obj))
            {

                currentLadders.Add(obj);
                canClimb = true;
            }
        }
        public void RemoveLadder(GameObject obj)
        {
            if (currentLadders.Contains(obj))
            {
                currentLadders.Remove(obj);
            }
            if (currentLadders.Count == 0)
            {
                canClimb = false;
                StopClimbingLadder();
            }
        }

        //this is for dismounting from ladder after climbing too low or too high
        public bool LadderClimbRayCasts(float pInputY)
        {

            Vector3 above = _character.transform.position;
            above.y -= _character.transform.localScale.y * _character._box.size.y * 0.5f - 0.01f;

            if (pInputY == 1)
            {
                if (!Physics2D.Raycast(above, Vector2.up, 0.2f, _character._controller.ladderCollision))
                {
                    StopClimbingLadder();
                    return false;
                }
            }

            Vector3 below = _character.transform.position;
            below.y -= _character.transform.localScale.y * _character._box.size.y * 0.5f + 0.01f;
            if (pInputY == -1)
            {
                //stop climbing if we hit the bottom of the ladder. (no more ladders avail)
                bool returnBool = Physics2D.Raycast(below, -Vector2.up, 0.2f, _character._controller.ladderCollision);

                if (!returnBool)
                {
                    StopClimbingLadder();
                    fLadderClimbCooldown = 0;
                    return false;
                }
            }
            return true;
        }

        public void StopClimbingLadder()
        {

            if (isClimbing)
            {
                climbCooldownComplete = false;
                _character._anim.speed = 1f;
                isClimbing = false;
                fLeaveLadderFeelingCooldown = 0f;
                canLeaveLadderBool = false;

                _character.velocity.x = 0;
                _character.velocity.y = 0;
                _character._anim.SetBool("climbing", false);
            }
        }

        public void ClimbLadder(float direction, float directionX)
        {

            if (direction != 0 && !isClimbing && climbCooldownComplete && currentLadders.Count > 0)
            {
                if (direction == 1)
                {

                    if (Physics2D.Raycast(_character.transform.position, Vector2.up, _character.transform.localScale.y * 0.5f + 0.1f, _character._controller.ladderCollision))
                    {
                        if (!isClimbing) { fLeaveLadderFeelingCooldown = 0f; }
                        isClimbing = true;
                        Vector3 newPos = _character.transform.position;
                        newPos.x = currentLadders[0].transform.position.x;
                        _character.transform.position = newPos;

                    }
                }
                if (direction == -1)
                {
                    Vector3 below = _character.transform.position;
                    below.y -= _character.transform.localScale.y * 0.5f + 0.1f;
                    if (Physics2D.Raycast(below, -Vector2.up, 0.1f, _character._controller.ladderCollision))
                    {
                        if (!isClimbing) { fLeaveLadderFeelingCooldown = 0f; }
                        isClimbing = true;
                        Vector3 newPos = _character.transform.position;
                        newPos.x = currentLadders[0].transform.position.x;
                        _character.transform.position = newPos;

                    }
                }
            }
            if (isClimbing && currentLadders.Count > 0)
            {
                _character.velocity.x = 0f;
                _character.velocity.y = 0f;

                if (direction != 0 && LadderClimbRayCasts(direction))
                {

                    _character._anim.speed = 1f;
                    Vector3 climbPosition = _character.transform.position;
                    climbPosition.y += direction * ladderClimbingSpeed * Time.deltaTime;
                    climbPosition.x = currentLadders[0].transform.position.x; /*lock to center of ladder*/
                    _character.transform.position = climbPosition;
                    _character._anim.SetBool("climbing", true);
                }
                if (direction == 0) { _character._anim.speed = 0f; }
            }

            if (directionX != 0)
            {
                if (isClimbing && canLeaveLadderBool)
                {
                    _character._controller.Test(new Vector3(directionX * 0.1f, -0.1f, 0f));

                    if (!_character._controller.collisions.right && directionX > 0 || !_character._controller.collisions.left && directionX < 0)
                    {
                        StopClimbingLadder();
                    }

                }
            }
        }

    }
    [System.Serializable]
    public class ledgeGrabStats : movementEssentials
    {

        public bool ability = true;
        public float grabHeight = 0.2f;
        public float characterYPosition = -0.15f;
        public float ledgeClimbSpeed = 0.05f;
        public float ledgeMoveDistance = 0.35f;
        public float ledgeGrabCooldown = 0.3f;
        public float ledgeGrabDistance = 0.12f;
        public float ledgeCooldown = 0.8f;
        public bool ledgeGrabbed = false;

        [System.NonSerialized]
        public Vector2 grabbedCorner = Vector2.zero;
        [System.NonSerialized]
        public bool ledgeCooldownBool = false;
        [System.NonSerialized]
        public float fLedgeGrabCooldown;
        [System.NonSerialized]
        public float fLedgeCooldown;
        [System.NonSerialized]
        public int ledgeState = 0;


        public void LedgeGrabState()
        {

            if (!_character.ladder.isClimbing && !ledgeGrabbed && !_character._controller.collisions.below && _character.velocity.y <= 0 && !ledgeCooldownBool)
            {
                Vector2 direction = _character.facingRight ? Vector2.right : Vector2.right * -1f;
                Vector3 position = _character.transform.position;
                position.y += _character.transform.localScale.y * _character._box.size.y * 0.5f + characterYPosition + grabHeight;

                /*check collision (if no collision, the next collision check will determine if it can be grabbed)*/
                if (!Physics2D.Raycast(position, direction, ledgeGrabDistance + _character.transform.localScale.x * _character._box.size.x * 0.5f, _character._controller.collisionMask))
                {

                    RaycastHit2D hit = Physics2D.Raycast(_character.transform.position, direction, ledgeGrabDistance + _character.transform.localScale.x * _character._box.size.x * 0.5f, _character._controller.collisionMask);
                    if (hit && hit.collider.tag != "oneway")
                    { /*check collision beside character, if its oneway, ignore*/

                        RaycastHit2D vertHit = Physics2D.Raycast(_character.transform.position, Vector2.up, _character.transform.localScale.y * _character._box.size.y, _character._controller.collisionMask);
                        if (!vertHit || vertHit.collider.tag == "oneway")
                        { /*check collision above character, if its oneway, ignore*/

                            Vector3 reposition = Vector3.zero;
                            reposition.x = hit.point.x - (direction.x * _character.transform.localScale.x * _character._box.size.x * 0.5f + 0.01f);
                            reposition.y = hit.collider.transform.position.y + hit.collider.transform.localScale.y * _character._box.size.y * 0.5f;
                            grabbedCorner = new Vector2(hit.point.x, reposition.y);
                            reposition.y -= (characterYPosition + _character.transform.localScale.y * _character._box.size.y * 0.5f);
                            ledgeGrabbed = true;
                            _character._anim.SetBool("ledge", true);
                            _character.transform.position = reposition;

                            Debug.DrawRay(position, direction, Color.red, 2f);
                            Debug.DrawRay(_character.transform.position, Vector2.up, Color.red, 2f);
                        }
                    }
                }

            }

            if (ledgeGrabbed && ledgeState != 0 && !ledgeCooldownBool)
            {

                Vector2 direction = _character.facingRight ? Vector2.right : Vector2.right * -1f;

                if (ledgeState == 1)
                {
                    _character._anim.SetBool("ledgeClimbing", true);

                    _character._controller.Test(new Vector3(0.0001f, 0.001f, 0f));
                    if (_character._controller.collisions.above)
                    {

                        StopLedgeGrab();
                    }
                    if (_character.transform.position.y - _character.transform.localScale.y * _character._box.size.y * 0.5f < grabbedCorner.y)
                    {
                        Vector3 newPos = _character.transform.position;
                        newPos.y += ledgeClimbSpeed;
                        _character.transform.position = newPos;
                    }
                    else
                    {
                        ledgeState = 2;
                    }

                }
                if (ledgeState == 2)
                {

                    Vector3 newPos = _character.transform.position;
                    newPos.x += direction.x * ledgeClimbSpeed;
                    _character.transform.position = newPos;


                    _character._controller.Test(new Vector3(direction.x * 0.01f, 0, 0f));
                    if (direction.x == 1)
                    {
                        if (_character._controller.collisions.right || (_character.transform.position.x - _character.transform.localScale.x * _character._box.size.x * 0.5f) > grabbedCorner.x)
                        {

                            StopLedgeGrab();
                        }
                    }
                    if (direction.x == -1)
                    {
                        if (_character._controller.collisions.left || (_character.transform.position.x + _character.transform.localScale.x * _character._box.size.x * 0.5f) < grabbedCorner.x)
                        {

                            StopLedgeGrab();
                        }
                    }

                }
            }
        }

        public void StopLedgeGrab()
        {
            if (ledgeGrabbed)
            {
                ledgeGrabbed = false;
                ledgeState = 0;
                _character._anim.SetBool("ledge", false);
                _character._anim.SetBool("ledgeClimbing", false);
                ledgeCooldownBool = true;
                _character.velocity.x = 0;
                _character.velocity.y = 0;
            }
        }

    }
    [System.Serializable]
    public class portalStats
    {

        public bool ability = true;
        public GameObject usePortal;
        public bool canUsePortal = true;
        public float usePortalCooldown = 0.7f;

        [System.NonSerialized]
        public float fUsePortalCooldown;

        public void AddPortal(GameObject obj)
        {
            usePortal = obj;
        }

        public void RemovePortal(GameObject obj)
        {
            if (usePortal == obj)
            {
                usePortal = null;
            }
        }
    }
}