using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class FindWalkableSides : MonoBehaviour {

    BoxCollider2D shape;
    Vector3[] sides = new Vector3[4];
    //Vector3[] perp = new Vector3[4];

    void Start() {
        shape = GetComponent<BoxCollider2D>();

        FindSides();

        Debug.Break();
    }


    void FindSides() {

        float size = (shape.size.x * transform.localScale.x) / 2;

        Vector2 pos = transform.position;
        float rot = transform.localEulerAngles.z; 
        sides[0] = pos + (new Vector2(-size, size)).Rotate(rot); //top left
        sides[1] = pos + (new Vector2(size, size)).Rotate(rot); //top right
        sides[2] = pos + (new Vector2(size, -size)).Rotate(rot); //bottom right
        sides[3] = pos + (new Vector2(-size, -size)).Rotate(rot); //bottom left
        

        PerpendicularPointAwayFromSides(size, 0, 1);
        PerpendicularPointAwayFromSides(size, 1, 2);
        PerpendicularPointAwayFromSides(size, 2, 3);
        PerpendicularPointAwayFromSides(size, 3, 0);
    }

    void PerpendicularPointAwayFromSides(float size, int one, int two) {
        var first = sides[one];
        var second = sides[two];
        Vector3 dir = (second - first);
        dir.Normalize();
        first += dir * size;
        var newVec = first - second;
        
        
        var newVector = Vector3.Cross(newVec, Vector3.forward);
        newVector.Normalize();

        var newPoint = 1.5f * newVector + first;
        var newPoint2 = 0 * newVector + first;

        float angle = Mathf.Atan2(newPoint2.y - newPoint.y, newPoint2.x - newPoint.x) * 180 / Mathf.PI;
        float theSlope = 60f;
        if (newPoint2.y > transform.position.y && angle <= -90 + theSlope && angle >= -90 - theSlope) {

            Debug.DrawLine(newPoint, newPoint2, Color.red);
            
            
        }


        //if (one == 0)
           // Debug.Log(angle);
        // sides[0] = Vector3.Cross(sides[0], sides[1]); Debug.Log(sides[0] + "aaa");
    }


	
	// Update is called once per frame
	void Update () {
        FindSides();
    }

    void OnDrawGizmos() {
        Gizmos.color = Color.red;
        for (int i = 0; i < sides.Length; i++) {
            Gizmos.DrawSphere(sides[i], 0.05f);
        }
       // Gizmos.color = Color.yellow;
       // Gizmos.DrawSphere(shape.bounds.extents + transform.position, 0.25f);
    }
}

public static class Vector2Extension {

    public static Vector2 Rotate(this Vector2 v, float degrees) {
        float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
        float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

        float tx = v.x;
        float ty = v.y;
        v.x = (cos * tx) - (sin * ty);
        v.y = (sin * tx) + (cos * ty);
        return v;
    }
}