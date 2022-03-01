using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotate : MonoBehaviour {

    public float speed = 8;
	public Vector3  board; 

	void Start () {//Set up things on the start method
        //transform.LookAt(board);//makes the camera look to it
    }
   
    void Update () {//makes the camera rotate around "point" coords, rotating around its Y axis, 20 degrees per second times the speed modifier
        transform.RotateAround (board,new Vector3(0,0,1),20 * Time.deltaTime * speed);
    }

}
