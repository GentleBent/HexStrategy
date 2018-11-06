using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Tilemaps;

//TODO: This could be made into an component instead, added to objects that need player controlled behaviour.
public abstract class AbstractUnit : NetworkBehaviour {

    //These should be set by the actual unit!
    public  UnitType UnitType;
    public  int BaseMoves;
    public  int BaseHitPoints;
    public  int BaseSightRange;
    public  int BaseInitiative;
    public  int BaseDamage;
    public  float BaseHitChance;

    //should probably not be overriden, unless unit has som special ability that changes these.
    public readonly int DefaultMoveCost = 10;
    public readonly int DefaultRotationCost = 2;

    //network stuff
    [SyncVar(hook = "OnUnitChangeIsActivated")]
    public bool IsActivated;
    public bool HasInitialized = false;
    public bool HasReceivedAuthority = false;

    [SyncVar (hook = "OnUnitMovesLeftChanged")]
    public int MovePointsLeft;
    [SyncVar]
    public int HPLeft;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        HasReceivedAuthority = true;
        CmdHasReceivedAuthority();
    }

    public PlayerScript LocalPlayer;
    public GridScript gridscr;
    public Tile hexBorder;
    public Transform arrow;
    public bool IsDisplayingMovement;
    public bool isDisplayingRotations;
    public bool isMoving;
    public bool isFacingRight;
    public HexDirections direction;
    public List<Vector3Int> adjTilesPos;
    public List<Transform> currentArrows;
    public List<Vector3Int> fieldOfVisionTiles;



    //-----SYNCVAR HOOKS------//
    public void OnUnitChangeIsActivated(bool activated)
    {
        //Debug.Log(hasAuthority);
        if (!hasAuthority)
            return;
        if (activated)
        {
            IsActivated = true;
            StartCoroutine(WaitForOtherUnitsToDeactivate());
        }
        else
        {
            IsActivated = false;
            IsDisplayingMovement = false;
            RemoveMoveTiles();
            isDisplayingRotations = false;
            RemoveRotationArrows();
        }
        
    }

    public void OnUnitMovesLeftChanged(int moveLeft)
    {
        if (IsActivated && hasAuthority)
            MovePointsLeft = moveLeft;
            LocalPlayer.UpdateUnitUIMoves(moveLeft);
    }

    //-----COMMANDS-----//
    [Command]
    public void CmdMoveServerUnit(Vector3 pos)
    {
        if(MovePointsLeft - DefaultMoveCost >= 0)
        {
            RemoveMoveTiles();
            this.isMoving = true;
            this.MovePointsLeft -= DefaultMoveCost;
            StartCoroutine(MoveOverSeconds(gameObject, pos, 0.6f));
        }
        else
        {
            //move was not legal, ask client to move the unit back to this units position.
            RpcMoveUnit(transform.position);
        }
        
    }

    [Command]
    public void CmdHasReceivedAuthority()
    {
        this.HasReceivedAuthority = true;
    }

    //-----RPC-----//
    [ClientRpc]
    public void RpcMoveUnit(Vector3 serverPosition)
    {
        transform.position = serverPosition;
    }

    //-----UNIT METHODS-----//

    public void WhenRightClicked()
    {
        //replace move options with rotation options
        if(MovePointsLeft > 0 && IsDisplayingMovement && !isMoving && !isDisplayingRotations && IsActivated)
        {
            isDisplayingRotations = true;
            IsDisplayingMovement = false;
            RemoveMoveTiles();
            SetRotationArrows();
        }
        //return to move options
        else if (MovePointsLeft > 0 && !IsDisplayingMovement && !isMoving && isDisplayingRotations && IsActivated)
        {
            isDisplayingRotations = false;
            IsDisplayingMovement = true;
            RemoveRotationArrows();
            SetMoveTiles();
        }
    }

    public void MoveTileClicked(Vector3 pos)
    {
        if (IsDisplayingMovement && !isDisplayingRotations && MovePointsLeft - DefaultMoveCost >= 0)
        {
            RemoveMoveTiles();
            this.isMoving = true;
            CmdMoveServerUnit(pos);
            StartCoroutine(MoveOverSeconds(gameObject, pos, 0.6f));
        }
        
    }

    public void RotationArrowClicked(Arrow arr)
    {
        this.direction = arr.direction;

        if ((int)direction < 2 || (int)direction == 5)
        {
            isFacingRight = true;
        }
        else isFacingRight = false;
        if (isFacingRight)
        {
            GetComponentInChildren<SpriteRenderer>().flipX = false;
        }
        else GetComponentInChildren<SpriteRenderer>().flipX = true;

        GetComponentInChildren<UnitDirection>().SetNewDirectionAndRotion(this.direction);
        fieldOfVisionTiles = gridscr.ClearFogNormalVision(transform.position, direction, 6);
        LocalPlayer.HandleFogOfWarHide();
        
    }

    public void SetRotationArrows()
    {
        
        var arrowTiles = gridscr.GetAdjacentGridCells(transform.position);
        this.currentArrows = new List<Transform>();
        for (var i = 0; i < arrowTiles.Count; i++)
        {
            var worldPos = gridscr._grid.CellToWorld(arrowTiles[i]);
            var obj = Instantiate(arrow, worldPos, Quaternion.identity);
            obj.GetComponent<Arrow>().direction = (HexDirections)i;
            obj.transform.Rotate(0, 0, i * -60);
            this.currentArrows.Add(obj);
        }
    }

    public void RemoveRotationArrows()
    {
        if(currentArrows != null)
        {
            foreach (var trans in currentArrows)
            {
                Destroy(trans.gameObject);
                currentArrows = null;
            }
        }
        
    }

    public void SetMoveTiles()
    {
        this.adjTilesPos = gridscr.GetAdjacentGroundCells(gridscr._grid.WorldToCell(transform.position));
        foreach (var cell in adjTilesPos)
        {
            gridscr._moveSelectTileMap.SetTile(cell, Instantiate(hexBorder));
        }

    }
    public void RemoveMoveTiles()
    {
        foreach (var cell in adjTilesPos)
        {
            gridscr._moveSelectTileMap.SetTile(cell, null);
        }

    }

    //-----COROUTINES-----//
    public IEnumerator MoveOverSeconds(GameObject objectToMove, Vector3 end, float seconds)
    {
        float elapsedTime = 0;
        Vector3 startingPos = objectToMove.transform.position;
        while (elapsedTime < seconds)
        {
            objectToMove.transform.position = Vector3.Lerp(startingPos, end, (elapsedTime / seconds));
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        objectToMove.transform.position = end;
        this.isMoving = false;
        fieldOfVisionTiles = gridscr.ClearFogNormalVision(transform.position, direction, BaseSightRange);
        LocalPlayer.HandleFogOfWarHide();
        if(IsActivated)
            SetMoveTiles();
    }

    public IEnumerator WaitForOtherUnitsToDeactivate()
    {
        while(LocalPlayer.PlayerUnits.Count(u => u.IsActivated) > 1)
        {
            yield return null;
        }
        //all other deactivated
        gridscr.SelectedUnit = this;
        SetMoveTiles();
        IsDisplayingMovement = true;
        LocalPlayer.SetUnitUI(this);
    }

    //-----START/UPDATE-----//

    public void Start () {
        if (hasAuthority && !isServer)
        {
            this.LocalPlayer = FindObjectsOfType<PlayerScript>().First(x => x.isLocalPlayer);
            LocalPlayer.PlayerUnits.Add(this);
        }
        else if (isServer)
        {
            var playerList = FindObjectsOfType<PlayerScript>();
            foreach (var pl in playerList)
            {
                if (pl.PlayerUnits.Any(x => x == this))
                    LocalPlayer = pl;
            }
        }
        this.gridscr = ScriptableObject.FindObjectOfType<GridScript>();
        this.IsDisplayingMovement = false;
        this.isMoving = false;
        this.isDisplayingRotations = false;
        this.isFacingRight = true;
        this.HasInitialized = true; //GameManager waits for this to be true on all units before proceeding with initialization.
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}

public enum UnitType
{
    AbstractUnit = 0,
    Sniper = 1
}