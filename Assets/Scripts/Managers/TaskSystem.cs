using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Controllers;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Nejvyšší mozek logistiky nad vrstvou Pathfindingu. Přijímá úkoly (Inbound/Outbound) 
    /// a koordinuje AGV vozíky i kapacity regálů k jejich asynchronnímu splnění.
    /// Zde uplatňujeme silnou abstrakci - TaskSystem nezajímá JAK se vozík hýbe, jen CO má udělat.
    /// </summary>
    public class TaskSystem : MonoBehaviour
    {
        [Header("Systems Architecture")]
        public RackManager rackManager;
        
        [Header("Physical Zones (Automaticky nahlázené ze scény)")]
        public List<ZoneController> inboundZones = new List<ZoneController>();
        public List<ZoneController> outboundZones = new List<ZoneController>();
        public List<ZoneController> restingZones = new List<ZoneController>();

        [Header("3D Assets pro vizualizaci")]
        public GameObject itemPrefab; // Sem hráč v Unity přetáhne připravený Prefab Krabice/Palety

        [Header("AGV Fleet")]
        [Tooltip("Vloženo pro sledování - všechna dostupná AGV")]
        public List<AGVController> fleet = new List<AGVController>();

        [Header("Simulation Dash")]
        public bool triggerInboundDelivery = false;
        public bool triggerOutboundOrder = false;
        
        [Header("Stress Testing (Performance & Deadlocks)")]
        public bool stressTestMixed = false;
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

            // Automatický zátěžový test navržený pro testování extrémů (deadlocky, výkon A*)
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
                        // Ratio: 60% šance na naskladnění, 40% vyskladnění, abychom měli vždy co vozit
                        if (canStore && (!canSell || Random.value > 0.4f))
                        {
                            CreateInboundTask();
                        }
                        else if (canSell)
                        {
                            CreateOutboundTask();
                        }
                    }
                }
            }

            // Automatické odesílání líných aut na nabíječku (dok)
            if (restingZones.Count > 0)
            {
                foreach (var agv in fleet)
                {
                    // Zaparkujeme pouze auta, co právě dokončila práci a flákají se
                    if (agv.currentState == AGVState.Idle)
                    {
                        agv.currentState = AGVState.Charging; // Tento stav slouží jako "Jedu parkovat / Nabíjím"
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
                // Všechna parkoviště jsou na 100% obsazena, nebo tam už jiná auta jedou!
                // Vozík hold musí počkat v uličce, změníme status zpět na Idle, aby ho mohl TaskSystem zkusit znovu za chvíli.
                agv.currentState = AGVState.Idle;
                yield break;
            }
            
            Debug.Log($"[Flotila] AGV nemá práci, odjíždí do doku na {parkZone.gridPosition}.");
            
            bool isReached = false;
            agv.MoveToAndNotify(parkZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);
            
            // Auto sedí v doku, do stavu nesaháme (stále je Charging), z tohoto stavu ho může povolat nový Task.
        }

        /// <summary>
        /// Univerzální alokátor volné zóny. Najde nejbližší Inbound, Outbound nebo Parkoviště,
        /// u kterého nikdo fyzicky nestojí a kam nemá nikdo finálně naplánovanou cestu!
        /// </summary>
        private ZoneController GetSmartFreeZone(List<ZoneController> zoneList, AGVController forAGV)
        {
            ZoneController bestZone = null;
            float minDistance = float.MaxValue;

            foreach (var zone in zoneList)
            {
                bool isOccupied = false;
                
                // Kontrola proti všem vozíkům ve flotile
                foreach (var otherAGV in fleet)
                {
                    if (otherAGV == forAGV) continue;

                    // A) Je cizí auto fyzicky zaparkované přesně na této zóně?
                    if (_gridManager != null)
                    {
                        int agvX = Mathf.RoundToInt(otherAGV.transform.position.x / _gridManager.gridConfig.nodeSize);
                        int agvY = Mathf.RoundToInt(otherAGV.transform.position.z / _gridManager.gridConfig.nodeSize);
                        if (agvX == zone.gridPosition.x && agvY == zone.gridPosition.y) { isOccupied = true; break; }
                    }
                    
                    // B) Má cizí auto sice cestu křižovatkami, ale FINÁLNĚ jede zaparkovat právě do tohoto doku?
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
        // INBOUND LOGIKA (Příjem na Sklad)
        // ==========================================
        public void CreateInboundTask()
        {
            if (inboundZones.Count == 0) 
            { 
                Debug.LogError("TaskSystem: Nemáš postavenou žádnou Inbound Zónu! Postav ji přes Editor (BuildManager)."); 
                return; 
            }

            // Najdeme zóny, které na sobě zrovna teď ŽÁDNOU krabici k vyzvednutí nemají
            List<ZoneController> freeZones = inboundZones.FindAll(z => z.currentItem == null);
            if (freeZones.Count == 0)
            {
                Debug.LogWarning("TaskSystem: Všechny Inbound zóny jsou PLNÉ! Kamiony čekají venku.");
                return;
            }

            // Hledání regálu s místem
            RackController targetRack = rackManager.GetAvailableRackForStorage();
            if (targetRack == null)
            {
                Debug.LogWarning("TaskSystem: HALA JE PLNÁ! Není regál k uskladnění dodávky.");
                return;
            }
            
            // Rezervujeme 1 virtuální slot v cílovém regálu, aby další objednávka neposlala auto na stejné obsazené místo dřív, než tam tohle auto dojede!
            targetRack.PendingIncomingItems++;

            // Hledání spícího nebo nabíjejícího se robota
            AGVController idleAGV = fleet.Find(a => a.currentState == AGVState.Idle || a.currentState == AGVState.Charging);
            if (idleAGV == null)
            {
                Debug.LogWarning($"TaskSystem: Objednávka zamítnuta! Flotila má aktuálně {fleet.Count} aut, ale pracují.");
                targetRack.PendingIncomingItems--;
                return;
            }
            
            // DŮLEŽITÉ: Nyní pro příjezd zónu vybereme s ohledem na chytré rozložení a vyvarování se překřížení aut
            ZoneController inboundZone = GetSmartFreeZone(freeZones, idleAGV);
            if (inboundZone == null)
            {
                Debug.LogWarning("TaskSystem: Generování INBOUND zrušeno, k Inbound zónám zrovna míří jiná auta!");
                targetRack.PendingIncomingItems--;
                return;
            }

            // Tvorba logického zboží
            Item newPallet = new Item("IN-" + Random.Range(1000, 9999), "Nové Zboží", 250f);
            
            // Okamžitá fyzická vizualizace zboží rovnou na zelené Příjmové Rampě (pčidáme 0.1 do Y ať se neprolne s dlaždicí)
            if (itemPrefab != null)
            {
                Vector3 spawnPos = inboundZone.transform.position + new Vector3(0, 0.1f, 0);
                newPallet.VisualModel = Instantiate(itemPrefab, spawnPos, Quaternion.identity);
            }
            
            // Fyzicky zablokujeme zónu proti dalším příjmům, dokud toto neutratíme
            inboundZone.currentItem = newPallet;
            
            StartCoroutine(ExecuteInboundSequence(idleAGV, inboundZone, targetRack, newPallet));
        }

        private IEnumerator ExecuteInboundSequence(AGVController agv, ZoneController pickupZone, RackController dropoffRack, Item cargo)
        {
            agv.currentState = AGVState.MovingToPickup;
            Debug.Log($"[Logistika IN] AGV vyráží na příjem pro {cargo.ItemID}.");

            bool isReached = false;
            agv.MoveToAndNotify(pickupZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            agv.LoadItem(cargo);
            pickupZone.currentItem = null; // Zóna je uvolněna, odteď tam může přijet další kamion!
            
            // Přilepíme model krabice přímo na záda vozíku!
            if (cargo.VisualModel != null)
            {
                cargo.VisualModel.transform.SetParent(agv.transform);
                cargo.VisualModel.transform.localPosition = new Vector3(0, 0.8f, 0); // Vynese kostku 80cm nad zem
            }
            
            Debug.Log($"[Logistika IN] Naloženo. Přesun k regálu na pos {dropoffRack.gridPosition}.");
            yield return new WaitForSeconds(0.5f); 

            agv.currentState = AGVState.MovingToDropoff;

            isReached = false;
            agv.MoveToAndNotify(dropoffRack.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            agv.UnloadItem();
            dropoffRack.PendingIncomingItems--; // Uvolnění rezervace
            dropoffRack.StoreItem(cargo); // Skutečné fyzické doložení do regálu
            
            // Krabice se usídlí fyzicky v regálu
            if (cargo.VisualModel != null)
            {
                Transform slot = dropoffRack.GetNextVisualSlot();
                if (slot != null)
                {
                    // Profesionální Snap-To-Point logika na uzel
                    cargo.VisualModel.transform.SetParent(slot);
                    cargo.VisualModel.transform.localPosition = Vector3.zero;
                    cargo.VisualModel.transform.localRotation = Quaternion.identity;
                    
                    // Následuje zrušení Colliderů nebo Rigidbody pro úsporu fyzikálního enginu
                    // To není akutně třeba, pokud je na krabici nemáš.
                }
                else
                {
                    // Fallback pokud si hráč nevytvoří v Unity Editoru žádné Snap Pointy
                    cargo.VisualModel.transform.SetParent(dropoffRack.transform);
                    cargo.VisualModel.transform.localPosition = new Vector3(0, Random.Range(0.2f, 1.8f), 0);
                }
            }
            
            Debug.Log($"[Logistika IN] {cargo.ItemID} uloženo do regálu. Mise splněna.");

            agv.currentState = AGVState.Idle;
        }

        // ==========================================
        // OUTBOUND LOGIKA (Výdej zákazníkům)
        // ==========================================
        public void CreateOutboundTask()
        {
            if (outboundZones.Count == 0)
            {
                Debug.LogError("TaskSystem: Chybí Outbound zóna pro expedici! Postav ji nejdříve.");
                return;
            }

            // Najít regál, který MÁ NĚJAKÉ ZBOŽÍ (A které už nebylo rozebráno cizími vozy na cestě)
            RackController loadedRack = rackManager.AllRacks.Find(r => r.HasAvailableItemForPickup);
            if (loadedRack == null)
            {
                Debug.LogWarning("TaskSystem: Sklad je prázdný, nebo je všechno existující zboží právě rozebíráno! Nemáme co prodat.");
                return;
            }
            
            // Zamluvíme si pro toto auto výsostné právo na jednu krabici v tomto regálu
            loadedRack.PendingOutgoingItems++;

            AGVController idleAGV = fleet.Find(a => a.currentState == AGVState.Idle || a.currentState == AGVState.Charging);
            if (idleAGV == null)
            {
                Debug.LogWarning($"TaskSystem: Žádné volné AGV pro vyřízení objednávky. Aut ve flotile: {fleet.Count}");
                loadedRack.PendingOutgoingItems--;
                return;
            }

            // Chytrý výběr volné expediční zóny (tak, aby se auta nepotkala na jedné, když jich je připraveno víc!)
            ZoneController outboundZone = GetSmartFreeZone(outboundZones, idleAGV);
            if (outboundZone == null)
            {
                Debug.LogWarning("TaskSystem: Generování OUTBOUND zrušeno, všechny Outbound zóny jsou pod dojezdem.");
                loadedRack.PendingOutgoingItems--;
                return;
            }

            StartCoroutine(ExecuteOutboundSequence(idleAGV, loadedRack, outboundZone));
        }

        private IEnumerator ExecuteOutboundSequence(AGVController agv, RackController pickupRack, ZoneController dropoffZone)
        {
            agv.currentState = AGVState.MovingToPickup;
            Debug.Log($"[Logistika OUT] AGV vyráží do skladu k regálu {pickupRack.gridPosition} pro zboží.");

            bool isReached = false;
            agv.MoveToAndNotify(pickupRack.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            // Závodní stav: Pokud cizí vozík přijel dříve a vzal poslední zboží
            if (pickupRack.IsEmpty)
            {
                Debug.LogWarning($"[Logistika OUT] Zboží z regálu {pickupRack.gridPosition} mezitím zmizelo!");
                pickupRack.PendingOutgoingItems--; // Uvolníme propadlý zámek
                agv.currentState = AGVState.Idle;
                yield break;
            }

            // Nabere logicky
            pickupRack.PendingOutgoingItems--; // Uvolníme tranzitní rezervaci
            Item item = pickupRack.RetrieveItem(); // Provedeme tvrdý výdej poslední krabice
            agv.LoadItem(item);
            
            // Nabere vizuálně 3D krabici (zpět na vidle vozíku)
            if (item.VisualModel != null)
            {
                item.VisualModel.transform.SetParent(agv.transform);
                item.VisualModel.transform.localPosition = new Vector3(0, 0.8f, 0);
            }
            
            Debug.Log($"[Logistika OUT] AGV nabralo položku {item.ItemID}. Jede na Výdej.");
            yield return new WaitForSeconds(0.5f); // Simulace manipulace s krabicí na vidlích

            agv.currentState = AGVState.MovingToDropoff;

            isReached = false;
            agv.MoveToAndNotify(dropoffZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            // Vyloží logicky z auta
            agv.UnloadItem();
            
            // Fyzické smazání zakoupené krabice ze scény
            if (item.VisualModel != null)
            {
                Destroy(item.VisualModel);
            }
            
            // STATISTIKA PRO BAKALÁŘKU: Úspěšné odbavení
            if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.RegisterItemDelivered();

            Debug.Log($"[Logistika OUT] Objednávka {item.ItemID} doručena na Rampu! Peníze se sypou.");

            agv.currentState = AGVState.Idle; // Vyvěsí vlajku, že je připraveno na další tasky
        }
    }
}
