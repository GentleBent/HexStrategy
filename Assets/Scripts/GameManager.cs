using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class GameManager : NetworkManager
{

    public List<PlayerScript> Players;

    //GamePhase, Round, Timers, etc.
    public GamePhases CurrentPhase;
    public int Round;
    public bool RoundTimerActive;
    public float RoundTimer;
    public readonly float DefaultRoundTime = 180f;
    public List<AbstractUnit> MovementPhaseSortedUnitsList;
    public PlayerScript ActivePLayer;
    public AbstractUnit ActiveUnit;
    public List<AbstractUnit> ActionPhaseSortedUnitsList;

    //-----GAME PHASES, ROUNDS-----//
    public void StartMovementPhase()
    {
        CurrentPhase = GamePhases.MovementPhase;
        foreach (var player in Players)
        {
            player.CurrentGamePhase = CurrentPhase;
        }
        CreatePhaseSortedUnitList();
        ActivateNextUnitMovement(true);
        RoundTimerActive = true;
    }

    public void StartActionPhase()
    {
        CurrentPhase = GamePhases.ActionPhase;
        foreach (var player in Players)
        {
            player.CurrentGamePhase = CurrentPhase;
        }
        CreatePhaseSortedUnitList();
        ActivateNextUnitAction(true);
        RoundTimerActive = true;
    }

    public void ActivateNextUnitMovement(bool isFirstMoveInPhase)
    {
        if (!isFirstMoveInPhase)
        {
            MovementPhaseSortedUnitsList.First().IsActivated = false;
            MovementPhaseSortedUnitsList.Remove(MovementPhaseSortedUnitsList.First());
        }
        if (MovementPhaseSortedUnitsList.Any())
        {
            ActiveUnit = MovementPhaseSortedUnitsList.First();
        }
        else
        {
            //no more units left to move, start action phase.
            StartActionPhase();
            return;
        }
        ActiveUnit.IsActivated = true; //syncvar hook will tell client that unit is active.
        RoundTimer = DefaultRoundTime;
    }

    public void ActivateNextUnitAction(bool isFirstActionInPhase)
    {
        if (!isFirstActionInPhase)
        {
            ActionPhaseSortedUnitsList.First().IsActivated = false;
            ActionPhaseSortedUnitsList.Remove(ActionPhaseSortedUnitsList.First());
        }
        if (ActionPhaseSortedUnitsList.Any())
        {
            ActiveUnit = ActionPhaseSortedUnitsList.First();
        }
        else
        {
            //no more units left for action, reset unit movepoints and change phase.
            this.Round += 1;
            foreach (var pl in Players)
            {
                pl.PlayerUnits.ForEach( u => u.MovePointsLeft = u.BaseMoves);
            }
            StartMovementPhase();
            return;
        }
        ActiveUnit.IsActivated = true; //syncvar hook will tell client that unit is active.
        RoundTimer = DefaultRoundTime;
    }

    private void CreatePhaseSortedUnitList()
    {
        //TODO: if multiple units have same initiative, every odd player should get their unit activated.
        var temp = new List<AbstractUnit>();
        foreach (var player in Players)
        {
            temp.AddRange(player.PlayerUnits);
        }
        if (CurrentPhase == GamePhases.MovementPhase)
        {
            temp.OrderBy(unit => unit.BaseInitiative);
            MovementPhaseSortedUnitsList = temp;
        }
        else
        {
            temp.OrderByDescending(unit => unit.BaseInitiative);
            ActionPhaseSortedUnitsList = temp;
        }

    }

    //-----COROUTINES-----//
    IEnumerator WaitForGameToBeReady()
    {
        while (!SquadsHaveBeenSet() || !PlayersConnectionReady() || Players.Count < 1)
        {
            yield return null;
        }
        //will be run when game is ready
        foreach (var player in Players)
        {
            player.SpawnUnitsForLocalPlayer(player.connectionToClient);
        }
        StartCoroutine(WaitForUnitsToSpawn());
    }

    IEnumerator WaitForUnitsToSpawn()
    {
        while (!AllUnitsReady())
        {
            yield return null;
        }
        StartMovementPhase();
    }

    bool PlayersConnectionReady()
    {
        return !Players.Any(player => player.connectionToClient.isReady == false);
    }

    bool SquadsHaveBeenSet()
    {
        return !Players.Any(player => player.Squad.Length == 0);
    }

    bool AllUnitsReady()
    {
        var temp = new List<AbstractUnit>();
        foreach (var player in Players)
        {
            temp.AddRange(player.PlayerUnits);
        }
        return !temp.Any(unit => unit.HasInitialized == false || unit.HasReceivedAuthority == false);
    }

    //-----SERVER CALLBACK-----
    public override void OnServerAddPlayer(NetworkConnection con, short playerControllerId)
    {
        base.OnServerAddPlayer(con, playerControllerId);
        Players = FindObjectsOfType<PlayerScript>().ToList();
    }



    //-----START/UPDATE-----
    void Start()
    {
        CurrentPhase = GamePhases.None;
        StartCoroutine(WaitForGameToBeReady());
    }

    void Update()
    {

        //update RoundTimer for all players
        if (RoundTimerActive)
        {
            RoundTimer -= Time.deltaTime;
            foreach (var player in Players)
            {
                player.RoundTimer = this.RoundTimer;
                if (player.RoundTimer <= 0)
                {
                    ActivateNextUnitMovement(false);
                }
            }
            //change active unit if player has run out of moves.
            if (ActiveUnit.MovePointsLeft == 0 && CurrentPhase == GamePhases.MovementPhase)
            {
                ActivateNextUnitMovement(false);
            }
            //TODO: change active unit if player has run out of actions.

            //change active unit if player has clicked EndTurn.
            if (ActiveUnit.LocalPlayer.EndTurnPressed && CurrentPhase == GamePhases.MovementPhase)
            {
                ActiveUnit.LocalPlayer.EndTurnPressed = false;
                ActivateNextUnitMovement(false);
            }
            if (ActiveUnit.LocalPlayer.EndTurnPressed && CurrentPhase == GamePhases.ActionPhase)
            {
                ActiveUnit.LocalPlayer.EndTurnPressed = false;
                ActivateNextUnitAction(false);
            }
        }
    }

    void LateUpdate()
    {

    }
}

public enum GamePhases
{
    None = 0,
    MovementPhase = 1,
    ActionPhase = 2
}
