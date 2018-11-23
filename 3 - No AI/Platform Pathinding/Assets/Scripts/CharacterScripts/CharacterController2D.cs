using UnityEngine;

/// <summary>
/// Responsible for moving the character through the scene without hitting obstacles and falling through the ground.
/// </summary>
public class CharacterController2D : RaycastController
{
    public CollisionInfo collisions; // Sets whether the character is colliding above or below.

    public override void Start()
    {
        base.Start();
        collisions.faceDir = 1;
    }

    public void Move(Vector3 velocity, Vector2 input, bool standingOnPlatform = false)
    {
        UpdateRaycastOrigins(); // Updates raycasts to new position.
        collisions.Reset(); // Resets any current collision data.

        collisions.velocityOld = velocity; 

        if (velocity.x != 0)
        {
            collisions.faceDir = (int)Mathf.Sign(velocity.x); // Sets facing direction.
        }

        if (velocity.y != 0)
        {
            // Checks if character is colliding below and stops y velocity (so player does not fall through the ground).
            VerticalCollisions(ref velocity); 
        }

        transform.Translate(velocity); // Actually does the movement.

        if (standingOnPlatform)
        {
            collisions.below = true; // Sets the collision data.
        }
    }

    /// <summary>
    /// Checks for collisions above and below the player and sets the y velocity accordingly.
    /// </summary>
    void VerticalCollisions(ref Vector3 velocity)
    {
        float directionY = Mathf.Sign(velocity.y); // The direction we are currently heading.
        float rayLength = Mathf.Abs(velocity.y) + skinWidth; // How long the raycast will be.

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i + velocity.x);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            if (hit) // We've hit an obstacle.
            {
                velocity.y = (hit.distance - skinWidth) * directionY;
                rayLength = hit.distance;

                collisions.below = directionY == -1;
                collisions.above = directionY == 1;
            }
        }
    }

    /// <summary>
    /// Stores collision data.
    /// </summary>
    public struct CollisionInfo
    {
        public bool above, below;

        public Vector3 velocityOld;
        public int faceDir;
        public bool fallingThroughPlatform;

        public void Reset()
        {
            above = below = false;
        }
    }

}