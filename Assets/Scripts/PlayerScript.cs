using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PlayerScript : NetworkBehaviour
{

    public UnitType[] Squad;
    public List<AbstractUnit> PlayerUnits;

    //UI stuff
    public Text UiTimer;
    public Text UiPhase;
    public GameObject UiUnitPanel;
    public Text UiHpValue;
    public Text UiMoveValue;
    public Text UiInitiativeValue;
    public Text UiSightRangeValue;
    public Text UiDmgValue;
    public Text UiHitChanceValue;
    public Button UiEndTurnButton;
    [SyncVar]
    public bool EndTurnPressed;

    [SyncVar(hook = "OnTimerChange")]
    public float RoundTimer;
    [SyncVar(hook = "OnGamePhaseChange")]
    public GamePhases CurrentGamePhase;

    //spawnable prefabs
    public GameObject Sniper;

    public List<Vector3Int> GetAllUnitsFov()
    {
        var shownTiles = new List<Vector3Int>();
        foreach (var unit in PlayerUnits)
        {
            shownTiles.AddRange(unit.fieldOfVisionTiles);
        }
        return shownTiles;
    }

    public void HandleFogOfWarHide()
    {
        if (!isLocalPlayer)
            return;
        var fov = this.GetAllUnitsFov();
        var grid = FindObjectOfType<GridScript>();
        grid.FillFogOfWar(fov);
    }

    public void SetUnitUI(AbstractUnit unit)
    {
        if (!UiUnitPanel.activeInHierarchy)
        {
            UiUnitPanel.SetActive(true);
        }
        UiHpValue.text = unit.HPLeft.ToString();
        UiMoveValue.text = unit.MovePointsLeft.ToString();
        UiInitiativeValue.text = unit.BaseInitiative.ToString();
        UiSightRangeValue.text = unit.BaseSightRange.ToString();
        UiDmgValue.text = unit.BaseDamage.ToString();
        UiHitChanceValue.text = (unit.BaseHitChance * 100).ToString() + "%";
    }

    public void UiEndTurn()
    {
        if (isLocalPlayer)
            CmdEndTurn();
    }

    public void UpdateUnitUIMoves(int moveLeft)
    {
        UiMoveValue.text = moveLeft.ToString();
    }
    public void HideUnitUI()
    {
        UiUnitPanel.SetActive(false);
    }

    //-----COMMANDS-----//
    [Command]
    public void CmdSetSquad(UnitType[] squad)
    {
        this.Squad = squad;
    }

    [Command]
    public void CmdEndTurn()
    {
        this.EndTurnPressed = true;
    }

    //not command, but since it uses SpawnWithClientAuthority, should only be run by server.
    [Server]
    public void SpawnUnitsForLocalPlayer(NetworkConnection player)
    {
        foreach (var unit in Squad)
        {
            if (unit == UnitType.Sniper)
            {
                var u = Instantiate(Sniper, transform.position, Quaternion.identity);
                PlayerUnits.Add(u.GetComponent<AbstractUnit>());
                NetworkServer.SpawnWithClientAuthority(u, player);
            }
        }

    }

    //-----RPC-----//

    //-----SYNCVAR HOOK-----//
    public void OnTimerChange(float timer)
    {
        if (!isLocalPlayer)
            return;
        this.RoundTimer = timer;
        int min = Mathf.FloorToInt(timer / 60f);
        int sec = Mathf.FloorToInt(timer - min * 60);
        string niceTime = string.Format("{0:0}:{1:00}", min, sec);
        this.UiTimer.text = niceTime;
    }

    public void OnGamePhaseChange(GamePhases current)
    {
        if (!isLocalPlayer)
            return;
        this.CurrentGamePhase = current;
        this.UiPhase.text = CurrentGamePhase.ToString();
    }

    //-----START, UPDATE-----//

    void Start()
    {
        if (isLocalPlayer)
        {
            Squad = new UnitType[] { UnitType.Sniper, UnitType.Sniper };
            CmdSetSquad(Squad);
            //UI stuff
            this.UiTimer = GameObject.FindWithTag("UIRoundTime").GetComponent<Text>();
            this.UiPhase = GameObject.FindWithTag("UICurrentPhase").GetComponent<Text>();
            this.UiUnitPanel = GameObject.FindGameObjectWithTag("UIUnitPanel");
            this.UiHpValue = GameObject.FindGameObjectWithTag("UIHpValue").GetComponent<Text>();
            this.UiMoveValue = GameObject.FindGameObjectWithTag("UIMoveValue").GetComponent<Text>();
            this.UiInitiativeValue = GameObject.FindGameObjectWithTag("UIInitiativeValue").GetComponent<Text>();
            this.UiSightRangeValue = GameObject.FindGameObjectWithTag("UISightRangeValue").GetComponent<Text>();
            this.UiDmgValue = GameObject.FindGameObjectWithTag("UIDmgValue").GetComponent<Text>();
            this.UiHitChanceValue = GameObject.FindGameObjectWithTag("UIHitChanceValue").GetComponent<Text>();
            this.UiEndTurnButton = GameObject.FindGameObjectWithTag("UIEndTurnButton").GetComponent<Button>();
            UiEndTurnButton.onClick.AddListener(UiEndTurn);
            UiUnitPanel.SetActive(false);
        }
    }

    void Update()
    {        
        
    }
}
