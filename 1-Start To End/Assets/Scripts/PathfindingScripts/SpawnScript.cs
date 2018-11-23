using UnityEngine;
using System.Collections.Generic;

public class SpawnScript : MonoBehaviour {
    public GameObject target;
    public float t = 1.3f;
    private float fT = 0f;
    private List<GameObject> spawns = new List<GameObject>();
    public float spawnAmount = 5f;

    // Use this for initialization
    private void Awake() {
        //if (!target) { target = GameObject.FindGameObjectWithTag("house") as GameObject; }
    }

    private void spawnUnit() {

        for (int i = spawns.Count; i > 0; i--) {

            if (spawns[i - 1] == null) {
                spawns.Remove(spawns[i - 1]);
            }
        }
        if (spawns.Count < spawnAmount) {

            GameObject newGameObject = (GameObject)Instantiate(Resources.Load("pathfindChaseAi"), new Vector3(transform.position.x, transform.position.y, 0.0f), Quaternion.identity) as GameObject;
            spawns.Add(newGameObject);
            PathfindingAgent newCharScript = newGameObject.transform.GetComponent<PathfindingAgent>();
            newCharScript.RequestPath((Vector3)target.transform.position);
        }
    }

    // Update is called once per frame
    private void Update() {
        
        fT += Time.deltaTime;
        if (fT >= t) {
            fT = 0f;
            spawnUnit();
        }
        
    }
}