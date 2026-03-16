using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Controllers;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Centrální distribuční uzel. Alokuje logistické úlohy dostupné flotile AGV
    /// na základě stavových požadavků regálů a vstupních/výstupních zón.
    /// Zajišťuje datovou i fyzickou synchronizaci toku materiálu.
    /// </summary>
    public class TaskSystem : MonoBehaviour
    {
        [Header("Systems Architecture")]
        public RackManager rackManager;
        
        [Header("Physical Zones")]
        public List<ZoneController> inboundZones = new List<ZoneController>();
        public List<ZoneController> outboundZones = new List<ZoneController>();
        public List<ZoneController> restingZones = new List<ZoneController>();

        [Header("Assets")]
        public GameObject itemPrefab;

        [Header("AGV Fleet")]
        public List<AGVController> fleet = new List<AGVController>();

        [Header("Simulation Data")]
        public bool triggerInboundDelivery = false;
        public bool triggerOutboundOrder = false;
        
        [Header("Stress Testing Parameters")]
        public bool stressTestMixed = true;
        public bool stressTestInboundOnly = false;
        public bool stressTestOutboundOnly = false;

        [Range(0.2f, 5f)]
        public float stressTestInterval = 1.5f;
        private float _stressTimer = 0f;

        private GridManager _gridManager;

        private void Awake()
        {
            _gridManager = FindFirstObjectByType<GridManager>();
        }

        private void Update()
        {
            if (triggerInboundDelivery)
            {
                triggerInboundDelivery = false;
                CreateInboundTask();
            }

            if (triggerOutboundOrder)
            {
                triggerOutboundOrder = false;
                CreateOutboundTask();
            }

            // Procesování automatického zátěžového generátoru požadavků
            if (stressTestMixed || stressTestInboundOnly || stressTestOutboundOnly)
            {
                _stressTimer += Time.deltaTime;
                if (_stressTimer >= stressTestInterval)
                {
                    _stressTimer = 0f;
                    
                    bool canStore = rackManager.GetAvailableRackForStorage() != null;
                    bool canSell = rackManager.AllRacks.Exists(r => !r.IsEmpty);
                    
                    if (stressTestInboundOnly && canStore) 
                    {
                        CreateInboundTask();
                    }
                    else if (stressTestOutboundOnly && canSell) 
                    {
                        CreateOutboundTask();
                    }
                    else if (stressTestMixed)
                    {
                        if (canStore && (!canSell || Random.value > 0.4f))
                            CreateInboundTask();
                        else if (canSell)
                            CreateOutboundTask();
                    }
                }
            }

            // Delegování nevyužitých AGV jednotek do klidových stanic (Resting Zones)
            if (restingZones.Count > 0)
            {
                foreach (var agv in fleet)
                {
                    if (agv.currentState == AGVState.Idle)
                    {
                        agv.currentState = AGVState.Charging; 
                        StartCoroutine(ParkAGVSequence(agv));
                    }
                }
            }
        }

        private IEnumerator ParkAGVSequence(AGVController agv)
        {
            ZoneController parkZone = GetSmartFreeZone(restingZones, agv);
            if (parkZone == null) 
            {
                agv.currentState = AGVState.Idle; // Vyčkáme na další dostupný volný slot
                yield break;
            }
            
            bool isReached = false;
            agv.MoveToAndNotify(parkZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);
            // Jednotka setrvává ve stavu Charging, dokud ji TaskSystem nealokuje jinam
        }

        /// <summary>
        /// Vyhledá navigačně volnou zónu, minimalizujíc potenciální kolize s probíhající flotilovou logikou.
        /// </summary>
        /// <param name="zoneList">Kolekce skenovaných zón</param>
        /// <param name="forAGV">Vozidlo, pro které je zóna alokována</param>
        private ZoneController GetSmartFreeZone(List<ZoneController> zoneList, AGVController forAGV)
        {
            ZoneController bestZone = null;
            float minDistance = float.MaxValue;

            foreach (var zone in zoneList)
            {
                bool isOccupied = false;
                
                foreach (var otherAGV in fleet)
                {
                    if (otherAGV == forAGV) continue;

                    if (_gridManager != null)
                    {
                        int agvX = Mathf.RoundToInt(otherAGV.transform.position.x / _gridManager.gridConfig.nodeSize);
                        int agvY = Mathf.RoundToInt(otherAGV.transform.position.z / _gridManager.gridConfig.nodeSize);
                        if (agvX == zone.gridPosition.x && agvY == zone.gridPosition.y) { isOccupied = true; break; }
                    }
                    
                    if (otherAGV.FinalTargetNode == zone.gridPosition) { isOccupied = true; break; }
                }

                if (!isOccupied)
                {
                    float dist = Vector2.Distance(forAGV.transform.position, zone.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestZone = zone;
                    }
                }
            }

            return bestZone;
        }

        // ==========================================
        // Logika příjmu zboží (Inbound)
        // ==========================================
        public void CreateInboundTask()
        {
            if (inboundZones.Count == 0) 
            { 
                NotificationManager.LogError("Chybí Inbound zóna na mapě pro doručení dodávky."); 
                return; 
            }

            List<ZoneController> freeZones = inboundZones.FindAll(z => z.currentItem == null);
            if (freeZones.Count == 0)
            {
                NotificationManager.LogWarning("Dodávka čeká. Všechny Inbound zóny jsou aktuálně obsazeny.");
                return;
            }

            RackController targetRack = rackManager.GetAvailableRackForStorage();
            if (targetRack == null)
            {
                NotificationManager.LogWarning("Kapacita skladu je naplněna, dodávku nelze zpracovat.");
                return;
            }
            
            // Virtuální zámek kapacity zabraňuje race conditions při paralelním zpracování vícero AGV
            targetRack.PendingIncomingItems++;

            // AGV filtrace na základě energetických kapacit
            AGVController idleAGV = fleet.Find(a => 
                (a.currentState == AGVState.Idle && a.currentBattery > 15f) || 
                (a.currentState == AGVState.Charging && a.currentBattery >= 90f)
            );
            
            if (idleAGV == null)
            {
                NotificationManager.LogWarning($"Objednávka odložena, flotila ({fleet.Count} AGV) má zadané úkoly či je vybitá.");
                targetRack.PendingIncomingItems--;
                return;
            }
            
            ZoneController inboundZone = GetSmartFreeZone(freeZones, idleAGV);
            if (inboundZone == null)
            {
                targetRack.PendingIncomingItems--;
                return;
            }

            Item newPallet = new Item("IN-" + Random.Range(1000, 9999), "Stavební/Skladový Blok", 250f);
            
            if (itemPrefab != null)
            {
                Vector3 spawnPos = inboundZone.transform.position + new Vector3(0, 0.1f, 0);
                newPallet.VisualModel = Instantiate(itemPrefab, spawnPos, Quaternion.identity);
            }
            
            inboundZone.currentItem = newPallet;
            
            StartCoroutine(ExecuteInboundSequence(idleAGV, inboundZone, targetRack, newPallet));
        }

        private IEnumerator ExecuteInboundSequence(AGVController agv, ZoneController pickupZone, RackController dropoffRack, Item cargo)
        {
            agv.currentState = AGVState.MovingToPickup;

            bool isReached = false;
            agv.MoveToAndNotify(pickupZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            agv.LoadItem(cargo);
            pickupZone.currentItem = null; 
            
            // Relokace vizuálního reprezentantu k instanci vozidla
            if (cargo.VisualModel != null)
            {
                cargo.VisualModel.transform.SetParent(agv.transform);
                cargo.VisualModel.transform.localPosition = new Vector3(0, 0.8f, 0); 
            }
            
            yield return new WaitForSeconds(0.5f); 

            agv.currentState = AGVState.MovingToDropoff;

            isReached = false;
            agv.MoveToAndNotify(dropoffRack.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            agv.UnloadItem();
            dropoffRack.PendingIncomingItems--; 
            dropoffRack.StoreItem(cargo); 
            
            // Uložení vizuálního reprezentantu v uzlu regálu
            if (cargo.VisualModel != null)
            {
                Transform slot = dropoffRack.GetNextVisualSlot();
                if (slot != null)
                {
                    cargo.VisualModel.transform.SetParent(slot);
                    cargo.VisualModel.transform.localPosition = Vector3.zero;
                    cargo.VisualModel.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    cargo.VisualModel.transform.SetParent(dropoffRack.transform);
                    cargo.VisualModel.transform.localPosition = new Vector3(0, Random.Range(0.2f, 1.8f), 0);
                }
            }

            agv.currentState = AGVState.Idle;
        }

        // ==========================================
        // Logika výdeje zboží (Outbound)
        // ==========================================
        public void CreateOutboundTask()
        {
            if (outboundZones.Count == 0)
            {
                NotificationManager.LogError("Chybí Outbound zóna pro dokončení odchozího transferu.");
                return;
            }

            RackController loadedRack = rackManager.AllRacks.Find(r => r.HasAvailableItemForPickup);
            if (loadedRack == null)
            {
                NotificationManager.LogWarning("Zamítnuto, zásoby byly vyčerpány nebo jsou již alokovány pro probíhající transfery.");
                return;
            }
            
            loadedRack.PendingOutgoingItems++;

            AGVController idleAGV = fleet.Find(a => 
                (a.currentState == AGVState.Idle && a.currentBattery > 15f) || 
                (a.currentState == AGVState.Charging && a.currentBattery >= 90f)
            );
            
            if (idleAGV == null)
            {
                NotificationManager.LogWarning($"Expedice odložena, flotila ({fleet.Count} AGV) nemá žádnou pohotovou jednotku.");
                loadedRack.PendingOutgoingItems--;
                return;
            }

            ZoneController outboundZone = GetSmartFreeZone(outboundZones, idleAGV);
            if (outboundZone == null)
            {
                loadedRack.PendingOutgoingItems--;
                return;
            }

            StartCoroutine(ExecuteOutboundSequence(idleAGV, loadedRack, outboundZone));
        }

        private IEnumerator ExecuteOutboundSequence(AGVController agv, RackController pickupRack, ZoneController dropoffZone)
        {
            agv.currentState = AGVState.MovingToPickup;

            bool isReached = false;
            agv.MoveToAndNotify(pickupRack.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            if (pickupRack.IsEmpty)
            {
                // Výjimka simulující datový defekt či souběžnou mutaci logiky regálu, AGV neprovede žádnou fyzickou operaci.
                NotificationManager.LogWarning($"Objekt {pickupRack.gridPosition} invalidován během přesunu.");
                pickupRack.PendingOutgoingItems--; 
                agv.currentState = AGVState.Idle;
                yield break;
            }

            pickupRack.PendingOutgoingItems--; 
            Item item = pickupRack.RetrieveItem(); 
            agv.LoadItem(item);
            
            if (item.VisualModel != null)
            {
                item.VisualModel.transform.SetParent(agv.transform);
                item.VisualModel.transform.localPosition = new Vector3(0, 0.8f, 0);
            }
            
            yield return new WaitForSeconds(0.5f); 

            agv.currentState = AGVState.MovingToDropoff;

            isReached = false;
            agv.MoveToAndNotify(dropoffZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            agv.UnloadItem();
            
            if (item.VisualModel != null)
            {
                Destroy(item.VisualModel);
            }
            
            if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.RegisterItemDelivered();

            agv.currentState = AGVState.Idle; 
        }
    }
}
