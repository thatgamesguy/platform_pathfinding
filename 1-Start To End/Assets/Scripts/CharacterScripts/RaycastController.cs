using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BoxCollider2D))]
public class RaycastController : MonoBehaviour {

    [Header("Inheriting RaycastController")]

    [System.NonSerialized]
    public LayerMask collisionMask;
    [System.NonSerialized]
    public LayerMask ladderCollision;
    [System.NonSerialized]
    public LayerMask portalCollision;
    [System.NonSerialized]
    public LayerMask groundAndOneway;
    [System.NonSerialized]
    public LayerMask oneway;

    public const float skinWidth = .015f;
    public int horizontalRayCount = 4;
    public int verticalRayCount = 4;

    [HideInInspector]
    public float horizontalRaySpacing;
    [HideInInspector]
    public float verticalRaySpacing;

    [HideInInspector]
    public BoxCollider2D _collider;
    public RaycastOrigins raycastOrigins;

    public virtual void Awake() {
        

    }

    public virtual void Start() {
        _collider = GetComponent<BoxCollider2D>();
        collisionMask = LayerManager.instance.groundLayer;//1 << LayerMask.NameToLayer("ground");
        portalCollision = LayerManager.instance.portalLayer;//1 << LayerMask.NameToLayer("portal");
        ladderCollision = LayerManager.instance.ladderLayer;//1 << LayerMask.NameToLayer("ladder");
        oneway = LayerManager.instance.onewayLayer;//1 << LayerMask.NameToLayer("oneway");
        groundAndOneway = LayerManager.instance.groundLayer | LayerManager.instance.onewayLayer;//collisionMask | 1 << LayerMask.NameToLayer("oneway");
        CalculateRaySpacing();
    }

    public void UpdateRaycastOrigins() {
        Bounds bounds = _collider.bounds;
        bounds.Expand(skinWidth * -2);

        raycastOrigins.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
        raycastOrigins.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
        raycastOrigins.topLeft = new Vector2(bounds.min.x, bounds.max.y);
        raycastOrigins.topRight = new Vector2(bounds.max.x, bounds.max.y);
    }

    public void CalculateRaySpacing() {
        Bounds bounds = _collider.bounds;
        bounds.Expand(skinWidth * -2);

        horizontalRayCount = Mathf.Clamp(horizontalRayCount, 2, int.MaxValue);
        verticalRayCount = Mathf.Clamp(verticalRayCount, 2, int.MaxValue);

        horizontalRaySpacing = bounds.size.y / (horizontalRayCount - 1);
        verticalRaySpacing = bounds.size.x / (verticalRayCount - 1);
    }

    public struct RaycastOrigins {
        public Vector2 topLeft, topRight;
        public Vector2 bottomLeft, bottomRight;
    }
}