using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Items;
using DaggerfallConnect.Utility;


public class FastTravelEncounters : MonoBehaviour
{

    static Mod mod;
    [Invoke(StateManager.StateTypes.Start, 0)]

    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        var go = new GameObject(mod.Title);
        go.AddComponent<FastTravelEncounters>();
    }

    public class Encounter
    {
        public string message;
        public EncounterMobiles[] enemies;
        public EncounterMobiles[] allies;

        public class EncounterMobiles
        {
            public MobileTypes mobileType; //the type of enemy to spawn
            public Vector2Int mobileTypeSize;  //minimum and maximum number of mobileTypes to spawn

            public EncounterMobiles(MobileTypes newMobileType, Vector2Int newMobileTypeSize)
            {
                mobileType = newMobileType;
                mobileTypeSize = newMobileTypeSize;
            }
        }

        public Encounter(string newMessage, EncounterMobiles[] newEnemies, EncounterMobiles[] newAllies)
        {
            message = newMessage;
            enemies = newEnemies;
            allies = newAllies;
        }
    }

    public List<Encounter> encountersDay = new List<Encounter>();
    public List<Encounter> encountersNight = new List<Encounter>();
    public List<Encounter> encountersSea = new List<Encounter>();

    public List<GameObject> objectsSpawned = new List<GameObject>();

    //FastTravelEncountersPopUpWindow encountersPopUp;
    DaggerfallTravelPopUp travelPopUp;
    TravelTimeCalculator travelTimeCalculator = new TravelTimeCalculator();

    bool shipTemporary;

    bool doEncounter = false;

    public static FastTravelEncounters Instance;

    void Awake()
    {
        Instance = this;

        DaggerfallTravelPopUp.OnPreFastTravel += DaggerfallTravelPopUp_OnPreFastTravel;
        DaggerfallTravelPopUp.OnPostFastTravel += DaggerfallTravelPopUp_OnPostFastTravel;

        PlayerEnterExit.OnPreTransition += OnTransition;
        PlayerEnterExit.OnTransitionExterior += OnTransition;
        PlayerEnterExit.OnTransitionDungeonInterior += OnTransition;
        PlayerEnterExit.OnTransitionDungeonExterior += OnTransition;

        InitializeEncounters();

        mod.IsReady = true;
    }

    void InitializeEncounters()
    {
        encountersDay.Clear();
        encountersNight.Clear();
        encountersSea.Clear();

        //Pirates
        Encounter.EncounterMobiles[] piratesEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Thief, new Vector2Int(1, 3)),
            new Encounter.EncounterMobiles(MobileTypes.Rogue, new Vector2Int(1, 3)),
            //new Encounter.EncounterMobiles(MobileTypes.Nightblade, new Vector2Int(0, 2))
        };
        Encounter pirates = new Encounter("Your ship has been boarded!", piratesEnemies, null);
        encountersSea.Add(pirates);

        //Brigands
        Encounter.EncounterMobiles[] brigandsEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Thief, new Vector2Int(1, 3)),
            new Encounter.EncounterMobiles(MobileTypes.Rogue, new Vector2Int(1, 3))
            //new Encounter.EncounterMobiles(MobileTypes.Nightblade, new Vector2Int(0, 2))
        };
        Encounter brigands = new Encounter("A bird calls nearby.", brigandsEnemies, null);
        encountersDay.Add(brigands);
        encountersNight.Add(brigands);

        //Deserters
        Encounter.EncounterMobiles[] desertersEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Archer, new Vector2Int(1, 3)),
            new Encounter.EncounterMobiles(MobileTypes.Warrior, new Vector2Int(1, 3)),
            //new Encounter.EncounterMobiles(MobileTypes.Sorcerer, new Vector2Int(0, 2))
        };
        Encounter deserters = new Encounter("You smell woodsmoke.", desertersEnemies, null);
        encountersDay.Add(deserters);
        encountersNight.Add(deserters);

        //Apostates
        Encounter.EncounterMobiles[] apostatesEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Bard, new Vector2Int(1, 3)),
            new Encounter.EncounterMobiles(MobileTypes.Mage, new Vector2Int(1, 2)),
            //new Encounter.EncounterMobiles(MobileTypes.Battlemage, new Vector2Int(1, 2))
        };
        Encounter apostates = new Encounter("Your eyes suddenly sting.", apostatesEnemies, null);
        encountersDay.Add(apostates);
        encountersNight.Add(apostates);

        //Orc foragers
        Encounter.EncounterMobiles[] orcForagersEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Orc, new Vector2Int(2, 4)) };
        Encounter orcForagers  = new Encounter("You notice orc tracks.", orcForagersEnemies, null);
        encountersDay.Add(orcForagers);
        encountersNight.Add(orcForagers);

        //Orc patrol
        Encounter.EncounterMobiles[] orcPatrolEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Orc, new Vector2Int(2, 5)),
            new Encounter.EncounterMobiles(MobileTypes.OrcSergeant, new Vector2Int(1, 2)) };
        Encounter orcPatrol = new Encounter("You smell orcs. Definitely orcs.", orcPatrolEnemies, null);
        encountersDay.Add(orcPatrol);
        encountersNight.Add(orcPatrol);

        //Orc warband
        Encounter.EncounterMobiles[] orcWarbandEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.OrcSergeant, new Vector2Int(3, 7)),
            new Encounter.EncounterMobiles(MobileTypes.OrcShaman, new Vector2Int(1, 2)),
            new Encounter.EncounterMobiles(MobileTypes.OrcWarlord, new Vector2Int(1, 2)) };
        Encounter orcWarband = new Encounter("A harsh yell brings you up short.", orcWarbandEnemies, null);
        encountersDay.Add(orcWarband);
        encountersNight.Add(orcWarband);

        //Bears
        Encounter.EncounterMobiles[] bearsEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.GrizzlyBear, new Vector2Int(1, 3)) };
        Encounter bears = new Encounter("What's the rustling sound?", bearsEnemies, null);
        encountersDay.Add(bears);
        encountersNight.Add(bears);

        //Tigers
        Encounter.EncounterMobiles[] tigersEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.SabertoothTiger, new Vector2Int(1, 3)) };
        Encounter tigers = new Encounter("You feel you are being watched.", tigersEnemies, null);
        encountersDay.Add(tigers);
        encountersNight.Add(tigers);

        //Unmarked Mass Grave
        Encounter.EncounterMobiles[] zombiesEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Zombie, new Vector2Int(3, 9)) };
        Encounter zombies = new Encounter("A low, ghostly moan shivers by.", zombiesEnemies, null);
        encountersNight.Add(zombies);

        //Cursed Battlefield
        Encounter.EncounterMobiles[] skeletonsEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.SkeletalWarrior, new Vector2Int(2, 5)) };
        Encounter skeletons = new Encounter("A ghostly wail shatters the silence.", skeletonsEnemies, null);
        encountersNight.Add(skeletons);

        //Werewolves
        Encounter.EncounterMobiles[] werewolvesEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Werewolf, new Vector2Int(1, 4)) };
        Encounter werewolves = new Encounter("A wolf howls not far away.", werewolvesEnemies, null);
        encountersNight.Add(werewolves);

        //Wereboars
        Encounter.EncounterMobiles[] wereboarsEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Wereboar, new Vector2Int(1, 4)) };
        Encounter wereboars = new Encounter("You see movement in the underbrush.", wereboarsEnemies, null);
        encountersNight.Add(wereboars);

        //Lone Vampire
        Encounter.EncounterMobiles[] vampireEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.Rat, new Vector2Int(3, 5)),
            new Encounter.EncounterMobiles(MobileTypes.GiantBat, new Vector2Int(2, 4)),
            new Encounter.EncounterMobiles(MobileTypes.Vampire, new Vector2Int(1, 2)) };
        Encounter vampire = new Encounter("A huge bat flaps slowly overhead.", vampireEnemies, null);
        encountersNight.Add(vampire);

        //Ancient Vampire cohort
        Encounter.EncounterMobiles[] vampireAncientEnemies = {
            new Encounter.EncounterMobiles(MobileTypes.GiantBat, new Vector2Int(3, 5)),
            new Encounter.EncounterMobiles(MobileTypes.Vampire, new Vector2Int(2, 4)),
            new Encounter.EncounterMobiles(MobileTypes.VampireAncient, new Vector2Int(1, 2)) };
        Encounter vampireAncient = new Encounter("A strange silence envelopes you.", vampireAncientEnemies, null);
        encountersNight.Add(vampireAncient);
    }

    public static void OnTransition(PlayerEnterExit.TransitionEventArgs args)
    {
        Instance.ClearSpawnedObjects();
    }

    void ClearSpawnedObjects()
    {
        if (objectsSpawned.Count > 0)
        {
            foreach (GameObject enemy in objectsSpawned)
                Destroy(enemy);

            objectsSpawned.Clear();
        }
    }

    void DaggerfallTravelPopUp_OnPreFastTravel(DaggerfallTravelPopUp daggerfallTravelPopUp)
    {
        if (travelPopUp == null)
            travelPopUp = daggerfallTravelPopUp;

        ClearSpawnedObjects();

        doEncounter = CheckEncounter();

        if (doEncounter)
            InterruptTravel();
    }

    void DaggerfallTravelPopUp_OnPostFastTravel()
    {
        if (doEncounter)
            SpawnEncounter();
        else if (shipTemporary)
        {
            DFPosition shipCoords = DaggerfallBankManager.GetShipCoords();
            SaveLoadManager.StateManager.RemovePermanentScene(StreamingWorld.GetSceneName(5, 5));
            SaveLoadManager.StateManager.RemovePermanentScene(DaggerfallInterior.GetSceneName(2102157, BuildingDirectory.buildingKey0));
            DaggerfallBankManager.AssignShipToPlayer(ShipType.None);
            shipTemporary = false;
        }
    }

    bool CheckEncounter()
    {
        //base chance for encounter
        int chance = 50;

        //cautious travel benefits encounter avoidance
        //each day of travel adds 2 to encounter chance when travelling cautiously
        //the encounter malus increases to 6 per day when travelling recklessly
        if (travelPopUp.SpeedCautious)
            chance += Mathf.CeilToInt((travelTimeCalculator.CalculateTravelTime(travelPopUp.EndPos, travelPopUp.SpeedCautious, travelPopUp.SleepModeInn, travelPopUp.TravelShip, GameManager.Instance.TransportManager.HasHorse(), GameManager.Instance.TransportManager.HasCart()) / 1440) * 2);
        else
            chance += Mathf.CeilToInt((travelTimeCalculator.CalculateTravelTime(travelPopUp.EndPos, travelPopUp.SpeedCautious, travelPopUp.SleepModeInn, travelPopUp.TravelShip, GameManager.Instance.TransportManager.HasHorse(), GameManager.Instance.TransportManager.HasCart()) / 1440) * 6);

            //having a cart is a permanent malus to encounter avoidance
            if (GameManager.Instance.TransportManager.HasCart())
            chance += 10;
        else if (GameManager.Instance.TransportManager.HasHorse())
            chance -= 10;

        float roll = UnityEngine.Random.value * 100;

        Debug.Log("FAST TRAVEL ENCOUNTERS - " + roll.ToString() + " vs " + chance.ToString());
        //if d100 roll is below chance, encounter happens
        if (roll < chance)
            return true;
        else
            return false;
    }

    void InterruptTravel()
    {
        DFPosition startPos = TravelTimeCalculator.GetPlayerTravelPosition();
        DFPosition endPos = travelPopUp.EndPos;
        int fraction = UnityEngine.Random.Range(2, 9);
        DFPosition midPos = new DFPosition((endPos.X + startPos.X)/2, (endPos.Y + startPos.Y)/2);

        travelPopUp.EndPos = midPos;
        //FastTravelEncountersPopUpWindow.SetEndPos(midPos);
    }

    public static void SpawnEncounter()
    {
        Instance.doEncounter = false;
        Instance.StartCoroutine(Instance.SpawnEncounterCoroutine());
    }

    public IEnumerator SpawnEncounterCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        //get conditions
        GameObject playerObject = GameManager.Instance.PlayerObject;
        DaggerfallLocation currentLocation = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
        DaggerfallDateTime time = DaggerfallUnity.Instance.WorldTime.Now;
        DFPosition pos = GameManager.Instance.PlayerGPS.CurrentMapPixel;

        bool atSea = false;

        //if at sea, teleport to a ship
        if (GameManager.Instance.PlayerGPS.CurrentPoliticIndex == 64)
        {
            atSea = true;

            DFPosition shipCoords = DaggerfallBankManager.GetShipCoords();

            //player doesn't own a ship
            if (shipCoords == null)
            {
                shipTemporary = true;
                DaggerfallBankManager.AssignShipToPlayer(ShipType.Large);
            }

            GameManager.Instance.TransportManager.TransportMode = TransportModes.Ship;

            yield return new WaitForSeconds(0.5f);

            //look for Location object closest to player
            //pick random start marker in location
            //GameObject[] startMarkers = currentLocation.StartMarkers;

            if (DaggerfallBankManager.OwnedShip == ShipType.Small)
                GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(Vector3.right);
            else
                GameManager.Instance.PlayerMouseLook.SetHorizontalFacing(Vector3.forward);

            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            //reposition player to center-ish
            Ray ray = new Ray(new Vector3(playerObject.transform.position.x+200, playerObject.transform.position.y + 100, playerObject.transform.position.z + 200), Vector3.down);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit, 200f))
                playerObject.transform.position = hit.point + (Vector3.up * 0.9f);

            yield return new WaitForSeconds(0.5f);
        }

        //if sleeping at inns, encounters will always happen between 9AM and 3PM
        if (travelPopUp.SleepModeInn)
        {
            if (time.Hour < 9)
            {
                float raiseTime = (((9 - time.Hour) * 3600) - (time.Minute * 60) - time.Second);
                time.RaiseTime(raiseTime);
            }
            else if (time.Hour > 15)
            {
                float raiseTime = (((33 - time.Hour) * 3600) - (time.Minute * 60) - time.Second);
                time.RaiseTime(raiseTime);
            }
        }

        //change encounter pool depending on day/night or sea
        List<Encounter> encounters;
        if (atSea)
            encounters = encountersSea;
        else if (time.IsNight)
            encounters = encountersNight;
        else
            encounters = encountersDay;

        //if night and in the wilderness, spawn camp stuff
        if (time.Hour < 8 || time.Hour > 16)
        {
            if (atSea)
            {
                //equip a light source
                if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Lantern))
                    GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Lantern);
                else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Torch))
                    GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.UselessItems2, (int)UselessItems2.Candle))
                    GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.UselessItems2, (int)UselessItems2.Candle);
                else if (GameManager.Instance.PlayerEntity.Items.Contains(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle))
                    GameManager.Instance.PlayerEntity.LightSource = GameManager.Instance.PlayerEntity.Items.GetItem(ItemGroups.ReligiousItems, (int)ReligiousItems.Holy_candle);
            }
            else
                SpawnCamp();
        }

        Encounter encounter = encounters[UnityEngine.Random.Range(0, encounters.Count)];

        //show message
        DaggerfallUI.MessageBox(encounter.message);

        //if travel is cautious place enemies ahead of the player with a smaller radius
        float distance = 20f;
        float radius = 5f;

        if (travelPopUp.TravelShip)
        {
            distance = 10f;
            radius = 1f;
        }

        Vector3 spawnPoint = playerObject.transform.position + (playerObject.transform.forward * distance);

        //if travel is reckless place enemies around the player with a bigger radius
        if (!travelPopUp.SpeedCautious && !atSea)
        {
            spawnPoint = playerObject.transform.position;
            radius = 10f;
        }

        //GameObject[] enemyObjects = GameObjectHelper.CreateFoeGameObjects(spawnPoint, MobileTypes.Orc, UnityEngine.Random.Range(3, 6));
        for (int i = 0; i < encounter.enemies.Length; i++)
        {

            float radians = 2 * Mathf.PI / encounter.enemies.Length * i;
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius * 0.5f;
            Vector3 offset = new Vector3(spawnPoint.x + Mathf.Cos(radians) * radius + randomCircle.x, playerObject.transform.position.y, spawnPoint.z + Mathf.Sin(radians) * radius + randomCircle.y);

            int count = UnityEngine.Random.Range(encounter.enemies[i].mobileTypeSize.x, encounter.enemies[i].mobileTypeSize.y);
            if (count > 0)
            {
                GameObject[] mobiles = GameObjectHelper.CreateFoeGameObjects(offset, encounter.enemies[i].mobileType, count);

                for (int ii = 0; ii < mobiles.Length; ii++)
                {
                    objectsSpawned.Add(mobiles[ii]);

                    float radiansMobile = 2 * Mathf.PI / mobiles.Length * ii;
                    Vector3 offsetMobile = new Vector3(Mathf.Cos(radiansMobile), 0, Mathf.Sin(radiansMobile));

                    mobiles[ii].transform.Translate(offsetMobile*radius);

                    float height = 1;
                    Ray rayMobile = new Ray(mobiles[ii].transform.position + Vector3.up * 10f, Vector3.down);
                    RaycastHit hitMobile = new RaycastHit();
                    if (Physics.Raycast(rayMobile, out hitMobile, 20f))
                        height = hitMobile.point.y;
                    mobiles[ii].transform.position = new Vector3(mobiles[ii].transform.position.x, height + 1, mobiles[ii].transform.position.z);

                    mobiles[ii].transform.LookAt(playerObject.transform);

                    EnemyEntity entity = mobiles[ii].GetComponent<DaggerfallEntityBehaviour>().Entity as EnemyEntity;
                    if (entity != null)
                        entity.Team = MobileTeams.PlayerEnemy;

                    mobiles[ii].SetActive(true);
                }
            }
        }
    }

    void SpawnCamp()
    {
        Transform playerTransform = GameManager.Instance.PlayerObject.transform;

        Vector3 campPos = playerTransform.position + (playerTransform.forward * 5f);

        Ray ray = new Ray(campPos + (Vector3.up * 50f), Vector3.down);
        RaycastHit hit = new RaycastHit();
        if (Physics.Raycast(ray, out hit, 200f))
            campPos = hit.point + (Vector3.up * 0.6f);

        GameObject fireObject = GameObjectHelper.CreateDaggerfallBillboardGameObject(210, 1, GameObjectHelper.GetBestParent());
        fireObject.transform.position = campPos;
        objectsSpawned.Add(fireObject);

        //add light
        GameObject lightObject = GameObjectHelper.InstantiatePrefab(DaggerfallUnity.Instance.Option_InteriorLightPrefab.gameObject, string.Empty, fireObject.transform, campPos);
        //Light light = lightObject.GetComponent<Light>();

        //add sound
        DaggerfallAudioSource fireAudioSource = fireObject.AddComponent<DaggerfallAudioSource>();
        fireAudioSource.AudioSource.dopplerLevel = 0;
        fireAudioSource.AudioSource.rolloffMode = AudioRolloffMode.Linear;
        fireAudioSource.AudioSource.maxDistance = 5f;
        fireAudioSource.AudioSource.volume = 0.7f;
        fireAudioSource.SetSound(SoundClips.Burning, AudioPresets.LoopIfPlayerNear);
    }
}
