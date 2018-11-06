using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitDirection : MonoBehaviour {

    public HexDirections direction;

    public void SetNewDirectionAndRotion(HexDirections newDirection)
    {
        direction = newDirection;
        transform.rotation = Quaternion.Euler(0,0,-60 * (int)direction);
    }

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
