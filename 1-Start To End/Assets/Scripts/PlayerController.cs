using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WindowsInput;

public class PlayerController : MonoBehaviour {

    public float moveSpeed; // Pour contrôler la vitesse du player
    private Rigidbody2D myRigidbody; // Pour le mouvement du player
    public float jumpSpeed;
    private int i;
	void Start () {
        myRigidbody = GetComponent<Rigidbody2D>(); // Pour indiquer qu'on parle du Rigidbody qui est attaché au player
     
        
    }

    // Update is called once per frame
    void Update() {
        
        InputSimulator.SimulateKeyPress(VirtualKeyCode.RIGHT); // very cool library to simulate the keyboard (it is working by using the folder called InputSimulator)
      
        if (Input.GetAxisRaw("Horizontal") > 0f) // Pour savoir si le player est en mouvement vers la droite
        {
            myRigidbody.velocity = new Vector3(moveSpeed, myRigidbody.velocity.y, 0f); // Attribution de la valeur du moveSpeed à la vitesse du player à droite qui n'affectera pas le mouvement vertical si on descend.
            
        } else if (Input.GetAxisRaw("Horizontal") < 0f) // Pour savoir si le player est en mouvement vers la gauche
        {
            myRigidbody.velocity = new Vector3(-moveSpeed, myRigidbody.velocity.y, 0f); // Même chose mais pour le mouvement à gauche avec un moveSpeed négatif
        } else {
            myRigidbody.velocity = new Vector3(0f, myRigidbody.velocity.y, 0f); // Position d'arrêt
        }

        if(Input.GetButtonDown("Jump")) // Pour savoir si le player est en mouvement vers la droite
        {
            myRigidbody.velocity = new Vector3(myRigidbody.velocity.x, jumpSpeed, 0f); // Attribution de la valeur du moveSpeed à la vitesse du player à droite qui n'affectera pas le mouvement vertical si on descend.

        }

    }
}
