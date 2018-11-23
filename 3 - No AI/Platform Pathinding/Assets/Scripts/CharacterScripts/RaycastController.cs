using UnityEngine;
using System.Collections;

/// <summary>
/// Responsible for managing the characters raycasts to prevent the character from falling/jumping through the obstacles.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class RaycastController : MonoBehaviour
{
    protected LayerMask collisionMask; 
    protected const float skinWidth = .015f;
    protected int verticalRayCount = 4;
    protected float verticalRaySpacing;
    protected RaycastOrigins raycastOrigins;

    private BoxCollider2D _collider;

    public virtual void Start()
    {
        _collider = GetComponent<BoxCollider2D>();
        collisionMask = LayerManager.instance.groundLayer;//1 << LayerMask.NameToLayer("ground");
        CalculateRaySpacing();
    }

    public void UpdateRaycastOrigins()
    {
        Bounds bounds = _collider.bounds;
        bounds.Expand(skinWidth * -2);

        raycastOrigins.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        raycastOrigins.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        raycastOrigins.topLeft = new Vector2(bounds.min.x, bounds.max.y);
        raycastOrigins.topRight = new Vector2(bounds.max.x, bounds.max.y);
    }

    public void CalculateRaySpacing()
    {
        Bounds bounds = _collider.bounds;
        bounds.Expand(skinWidth * -2);

        verticalRayCount = Mathf.Clamp(verticalRayCount, 2, int.MaxValue);
        verticalRaySpacing = bounds.size.x / (verticalRayCount - 1);
    }

    public struct RaycastOrigins
    {
        public Vector2 topLeft, topRight;
        public Vector2 bottomLeft, bottomRight;
    }
}