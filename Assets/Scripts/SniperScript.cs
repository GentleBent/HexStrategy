using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SniperScript : AbstractUnit {

    // Use this for initialization
    new void Start () {
        base.Start();
        //setting sniper specific values
        this.UnitType = UnitType.Sniper;
        this.BaseMoves = 80;
        this.BaseHitPoints = 45;
        this.BaseSightRange = 7;
        this.BaseInitiative = 7;
        this.BaseDamage = 20;
        this.BaseHitChance = 0.75f;

        this.MovePointsLeft = BaseMoves;
        this.HPLeft = BaseHitPoints;
        this.direction = HexDirections.Right;
        
        this.fieldOfVisionTiles = gridscr.ClearFogNormalVision(transform.position, direction, BaseSightRange);

    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
