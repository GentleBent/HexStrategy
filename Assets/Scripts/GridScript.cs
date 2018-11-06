using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridScript : MonoBehaviour
{

    public Grid _grid;
    public Tilemap _groundTileMap;
    public Tilemap _moveSelectTileMap;
    public Tilemap _fogOfWarTileMap;
    public Tilemap _obstaclesTileMap;
    public Tilemap _HexborderTileMap;
    public Camera _camera;
    public Tile _fogTile;
    public Tile _hexBorderTile;

    public AbstractUnit SelectedUnit;
    private List<Vector3Int> allGroundTilePos;


    // Use this for initialization
    void Start()
    {
        _grid = GetComponent<Grid>();

        allGroundTilePos = new List<Vector3Int>();
        foreach (var cell in _groundTileMap.cellBounds.allPositionsWithin)
        {
            allGroundTilePos.Add(new Vector3Int(cell.x, cell.y, cell.z));
        }
        FillFogOfWar();
        //fill map with hexborders
        foreach (var item in allGroundTilePos)
        {
            _HexborderTileMap.SetTile(item, Instantiate(_hexBorderTile));
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Left Mouse Button
        if (Input.GetMouseButtonUp(0) && dragtime < 0.2f)
        {
            var mousePos = _camera.ScreenToWorldPoint(Input.mousePosition);
            var gridPos = _grid.WorldToCell(mousePos);
            //check if cell has unit
            var transform = GetUnitTransformInCell(gridPos);
            if (transform != null && transform.GetComponentInParent<AbstractUnit>() == SelectedUnit)
            {
                //this.SelectedUnit = transform.GetComponentInParent<AbstractUnit>();
                //this.SelectedUnit.WhenLeftClicked();
            }
            //check if direction change arrow was clicked
            else if(transform != null && transform.GetComponent<Arrow>() != null)
            {
                
                SelectedUnit.RotationArrowClicked(transform.GetComponent<Arrow>());
            }

            //check if the cell is a movement selector cell, call "MoveTileClicked" on last selected unit.
            if (_moveSelectTileMap.HasTile(gridPos))
            {
                SelectedUnit.MoveTileClicked(_grid.CellToWorld(gridPos));
            }
        }
        // Right Mouse Button
        if (Input.GetMouseButtonUp(1))
        {
            var mousePos = _camera.ScreenToWorldPoint(Input.mousePosition);
            var gridPos = _grid.WorldToCell(mousePos);

            var transform = GetUnitTransformInCell(gridPos);
            if (transform != null && transform.GetComponentInParent<AbstractUnit>() == SelectedUnit)
            {
                this.SelectedUnit.WhenRightClicked();
            }

        }
    }

    private float dragSpeed = 16f;
    private bool isDragging;
    private float dragtime;
    private Vector3 dragOrigin;

    private void LateUpdate()
    {
        if (isDragging)
        {
            dragtime += Time.deltaTime;
        }
        if (Input.GetMouseButton(0) && !isDragging)
        {
            dragOrigin = Input.mousePosition;
            isDragging = true;
        }
        if(dragtime > 0.2f && isDragging)
        {
            Vector3 pos = _camera.ScreenToViewportPoint(Input.mousePosition - dragOrigin);
            Vector3 move = new Vector3(pos.x * -1 * dragSpeed * Time.deltaTime, pos.y * -1 * dragSpeed * Time.deltaTime, 0);
            _camera.transform.Translate(move, Space.World);
        }
        if (!Input.GetMouseButton(0))
        {
            isDragging = false;
            dragtime = 0;
        }
    }







    public Transform GetUnitTransformInCell(Vector3Int cell)
    {
        var worldPos = _grid.GetCellCenterWorld(cell);
        var collider = Physics2D.OverlapPoint(worldPos);
        if (collider != null)
        {
            return collider.transform;
        }
        return null;
    }

    public List<Vector3Int> GetAdjacentGroundCells(Vector3Int cell)
    {
        var adj = GetAdjacentGridCells(cell);
        var adjGround = new List<Vector3Int>();
        foreach (var c in adj)
        {
            if (_groundTileMap.HasTile(c) && !_obstaclesTileMap.HasTile(c))
            {
                adjGround.Add(c);
            }
        }
        return adjGround;
    }

    public void FillFogOfWar()
    {
        foreach (var cell in allGroundTilePos)
        {
            _fogOfWarTileMap.SetTile(cell, _fogTile);
        }
    }
    public void FillFogOfWar(List<Vector3Int> exclude)
    {
        foreach (var cell in allGroundTilePos)
        {
            if(!exclude.Any(exludeVec => exludeVec.x == cell.x && exludeVec.y == cell.y))
            _fogOfWarTileMap.SetTile(cell, _fogTile);
        }
    }

    public List<Vector3Int> ClearFogNormalVision(Vector3 worldPos, HexDirections dir, int range)
    {
        var fieldOfVisionTiles = new List<Vector3Int>();

        var gridPos = _grid.WorldToCell(worldPos);
        //clear unit tile
        _fogOfWarTileMap.SetTile(gridPos, null);
        fieldOfVisionTiles.Add(gridPos);
        //clear forward
        var forwardSightRange = range;
        Vector3Int forwardClear = gridPos;
        for (int i = 0; i < range; i++)
        {
            forwardClear = GetNextCellInDirection(forwardClear, dir);
            if (_obstaclesTileMap.HasTile(forwardClear))
            {
                forwardSightRange = i + 1;
                _fogOfWarTileMap.SetTile(forwardClear, null);
                fieldOfVisionTiles.Add(forwardClear);
                break;
            }
            _fogOfWarTileMap.SetTile(forwardClear, null);
            fieldOfVisionTiles.Add(forwardClear);
        }
        //clear left over sight range
        int obstacleDeptLeft = int.MaxValue;
        for (int i = 0; i < forwardSightRange; i++)
        {
            var startpos = GetCellInDirectionTimesCells(gridPos, dir, i);
            obstacleDeptLeft = ClearFogDirectionReturnObstacleDept(startpos, dir, HexRotation.Left, range - i, obstacleDeptLeft, fieldOfVisionTiles);
        }

        //clear right over sight range
        int obstacleDeptRight = int.MaxValue;
        for (int i = 0; i < forwardSightRange; i++)
        {
            var startpos = GetCellInDirectionTimesCells(gridPos, dir, i);
            obstacleDeptRight = ClearFogDirectionReturnObstacleDept(startpos, dir, HexRotation.Right, range - i, obstacleDeptRight, fieldOfVisionTiles);
        }
        return fieldOfVisionTiles;
    }

    private int ClearFogDirectionReturnObstacleDept(Vector3Int position, HexDirections direction, HexRotation rotation, int range,
        int deptOfKnownObstacle, List<Vector3Int> addVisableToList)
    {
        bool shouldTurn = true;
        int dept = 0;
        Vector3Int currentPos = position;
        for (int i = 0; i < range; i++)
        {
            
            if (shouldTurn)
            {
                dept++;
                if (dept == deptOfKnownObstacle)
                {
                    return dept;
                }
                    var tempdir = RotateDirection(direction, rotation);
                currentPos = GetNextCellInDirection(currentPos, tempdir);
                if (_obstaclesTileMap.HasTile(currentPos))
                {
                    _fogOfWarTileMap.SetTile(currentPos, null);
                    addVisableToList.Add(currentPos);
                    return dept;
                }
                else
                {
                    _fogOfWarTileMap.SetTile(currentPos, null);
                    addVisableToList.Add(currentPos);
                    shouldTurn = false;
                }

            }
            //turned last iteration
            else
            {
                currentPos = GetNextCellInDirection(currentPos, direction);
                if (_obstaclesTileMap.HasTile(currentPos))
                {
                    _fogOfWarTileMap.SetTile(currentPos, null);
                    addVisableToList.Add(currentPos);
                    return dept;
                }
                else
                {
                    _fogOfWarTileMap.SetTile(currentPos, null);
                    addVisableToList.Add(currentPos);
                    shouldTurn = true;
                }
                    
            }
        }
        return int.MaxValue;
    }

    public Vector3Int GetCellInDirectionTimesCells(Vector3Int current, HexDirections dir, int cells = 1)
    {
        if (cells > 0)
        {
            Vector3Int pos = current;
            for (int i = 0; i < cells; i++)
            {
                pos = GetNextCellInDirection(pos, dir);
            }
            return pos;
        }
        else return current;
    }

    public List<Vector3Int> GetAdjacentGridCells(Vector3Int vector)
    {
        var listOfAdj = new List<Vector3Int>();
        var allDirections = (HexDirections[])Enum.GetValues(typeof(HexDirections));
        foreach (var dir in allDirections)
        {
            listOfAdj.Add(GetNextCellInDirection(vector, dir));
        }
        return listOfAdj;
    }
    public List<Vector3Int> GetAdjacentGridCells(Vector3 vector)
    {
        return this.GetAdjacentGridCells(_grid.WorldToCell(vector));
    }

    public HexDirections RotateDirection(HexDirections currentDir, HexRotation rotation)
    {
        if (rotation == HexRotation.Right)
        {
            var newDir = (int)currentDir + 1;
            if (newDir == 6)
            {
                newDir = 0;
            }
            return (HexDirections)newDir;
        }
        else if (rotation == HexRotation.Left)
        {
            var newDir = (int)currentDir - 1;
            if (newDir == -1)
            {
                newDir = 5;
            }
            return (HexDirections)newDir;
        }
        else throw new Exception("Invalid rotation!");
    }

    public Vector3Int GetNextCellInDirection(Vector3Int current, HexDirections dir)
    {
        switch (dir)
        {
            case HexDirections.Right:
                return new Vector3Int(current.x + 1, current.y, 0);
            case HexDirections.DownRight:
                if (current.y % 2 == 0)
                {
                    return new Vector3Int(current.x, current.y - 1, 0);
                }
                else
                {
                    return new Vector3Int(current.x + 1, current.y - 1, 0);
                }
            case HexDirections.DownLeft:
                if (current.y % 2 == 0)
                {
                    return new Vector3Int(current.x - 1, current.y - 1, 0);
                }
                else
                {
                    return new Vector3Int(current.x, current.y - 1, 0);
                }
            case HexDirections.Left:
                return new Vector3Int(current.x - 1, current.y, 0);
            case HexDirections.UpLeft:
                if (current.y % 2 == 0)
                {
                    return new Vector3Int(current.x - 1, current.y + 1, 0);
                }
                else
                {
                    return new Vector3Int(current.x, current.y + 1, 0);
                }
            case HexDirections.UpRight:
                if (current.y % 2 == 0)
                {
                    return new Vector3Int(current.x, current.y + 1, 0);
                }
                else
                {
                    return new Vector3Int(current.x + 1, current.y + 1, 0);
                }
            default:
                throw new Exception("Invalid direction!");
        }
    }

}

public enum HexDirections
{
    Right = 0,
    DownRight = 1,
    DownLeft = 2,
    Left = 3,
    UpLeft = 4,
    UpRight = 5
}

public enum HexRotation
{
    Right = 0,
    Left = 1
}