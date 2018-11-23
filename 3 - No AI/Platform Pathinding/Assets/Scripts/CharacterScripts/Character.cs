using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class Character : MonoBehaviour
{
    public moveStats movement;
    public jumpStats jump;
    public bool FallNodes = true; // Allows the pathfinding agent to use 'fall' nodes

    private GameObject _graphics;
    private Animator _anim;
    private Rigidbody2D _body;        
    private CharacterController2D _controller;
    private float gravity;  // Calculated automatically inside jump.UpdateJumpHeight.
    private Vector3 velocity;
    private PathFollowingAgent _pathAgent;

    private bool facingRight = true;          /*determines the direction character is facing*/
    private bool jumped = false;              /*used for detecting if jump key was pressed (also used in ai)*/

    void Awake()
    {
        _controller = GetComponent<CharacterController2D>();
        _body = GetComponent<Rigidbody2D>();
        _anim = transform.Find("Graphics").GetComponent<Animator>();
        _graphics = transform.Find("Graphics").gameObject; /*useful for preventing things from flipping when character is facing left*/
        _pathAgent = GetComponent<PathFollowingAgent>();

        /*allow movement abilities to access character script*/
        jump.setCharacter(this);

        _body.isKinematic = true;
    }

    void Start()
    {
        jump.UpdateJumpHeight();
    }

    void FixedUpdate()
    {
        Vector2 input = Vector2.zero;

        _pathAgent.ProcessMovement(ref velocity, ref input, ref jumped);

        if (jumped)
        {
            jumped = false;
            if (jump.ability && jump.jumpCount < jump.maxJumps) { velocity.y = jump.maxJumpVelocity; jump.jumpCount++; }
        }


        if (input.x > 0 && !facingRight)
        {
            Flip();
        }
        else if (input.x < 0 && facingRight)
        {
            Flip();
        }

        //Movement-x
        if (movement.ability) //If character has the ability of moving
        {
            float targetVelocityX = input.x * movement.moveSpeed;
            velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref movement.velocityXSmoothing, (_controller.collisions.below) ? movement.accelerationTimeGrounded : jump.accelerationTimeAirborne);
        }

        //Gravity
        if (velocity.y > -jump.maxFallVelocity)
        {
            velocity.y += gravity * Time.deltaTime;
        }

        _controller.Move(velocity * Time.deltaTime, input);

        //animation
        _anim.SetFloat("speed", input.x != 0 ? 1f : 0f);
        _anim.SetBool("grounded", _controller.collisions.below);

        //Grounded + Jump reset
        if (_controller.collisions.below)
        {
            jump.jumpCount = 0;
        }

        if (_controller.collisions.above || _controller.collisions.below)
        {
            velocity.y = 0;
        }
        else if (jump.jumpCount == 0)// else if the character falls off an edge, 1 jump is lost
        {
            jump.jumpCount++;
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
}