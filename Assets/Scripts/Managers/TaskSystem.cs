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
        
        [Header("Physical Zones")]
        [Tooltip("Zkopíruj sem GameObject Inbound Zóny ze scény")]
        public ZoneController inboundZone;
        [Tooltip("Zkopíruj sem GameObject Outbound Zóny ze scény")]
        public ZoneController outboundZone;

        [Header("AGV Fleet")]
        [Tooltip("Vloženo pro sledování - všechna dostupná AGV")]
        public List<AGVController> fleet = new List<AGVController>();

        [Header("Simulation Dash")]
        public bool triggerInboundDelivery = false;
        public bool triggerOutboundOrder = false;

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
        }

        // ==========================================
        // INBOUND LOGIKA (Příjem na Sklad)
        // ==========================================
        public void CreateInboundTask()
        {
            if (inboundZone == null) 
            { 
                Debug.LogError("TaskSystem: Chybí reference na Inbound Zónu!"); 
                return; 
            }

            // Hledání regálu s místem
            RackController targetRack = rackManager.GetAvailableRackForStorage();
            if (targetRack == null)
            {
                Debug.LogWarning("TaskSystem: HALA JE PLNÁ! Není regál k uskladnění dodávky.");
                return;
            }

            // Hledání spícího robota
            AGVController idleAGV = fleet.Find(a => a.IsIdle);
            if (idleAGV == null)
            {
                Debug.LogWarning($"TaskSystem: Objednávka zamítnuta! Flotila má aktuálně {fleet.Count} aut, ale pracují.");
                return;
            }

            // Tvorba zboží
            Item newPallet = new Item("IN-" + Random.Range(1000, 9999), "Nové Zboží", 250f);
            
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
            Debug.Log($"[Logistika IN] Naloženo. Přesun k regálu na pos {dropoffRack.gridPosition}.");
            yield return new WaitForSeconds(0.5f); 

            agv.currentState = AGVState.MovingToDropoff;

            isReached = false;
            agv.MoveToAndNotify(dropoffRack.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            agv.UnloadItem();
            dropoffRack.StoreItem(cargo);
            Debug.Log($"[Logistika IN] {cargo.ItemID} uloženo do regálu. Mise splněna.");

            agv.currentState = AGVState.Idle;
        }

        // ==========================================
        // OUTBOUND LOGIKA (Výdej zákazníkům)
        // ==========================================
        public void CreateOutboundTask()
        {
            if (outboundZone == null)
            {
                Debug.LogError("TaskSystem: Chybí reference na Outbound zónu!");
                return;
            }

            // Najít regál, který MA NĚJAKÉ ZBOŽÍ (není prázdný)
            RackController loadedRack = rackManager.AllRacks.Find(r => !r.IsEmpty);
            if (loadedRack == null)
            {
                Debug.LogWarning("TaskSystem: Sklad je prázdný! Nemáme co prodat.");
                return;
            }

            AGVController idleAGV = fleet.Find(a => a.IsIdle);
            if (idleAGV == null)
            {
                Debug.LogWarning($"TaskSystem: Žádné volné AGV pro vyřízení objednávky. Aut ve flotile: {fleet.Count}");
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
                agv.currentState = AGVState.Idle;
                yield break;
            }

            // Nabere zboží (Sníží kapacitu regálu za pochodu)
            Item item = pickupRack.RetrieveItem();
            agv.LoadItem(item);
            Debug.Log($"[Logistika OUT] AGV nabralo položku {item.ItemID}. Jede na Výdej.");
            yield return new WaitForSeconds(0.5f); // Simulace manipulace s krabicí na vidlích

            agv.currentState = AGVState.MovingToDropoff;

            isReached = false;
            agv.MoveToAndNotify(dropoffZone.gridPosition, () => isReached = true);
            yield return new WaitUntil(() => isReached);

            // Vyloží z auta
            agv.UnloadItem();
            
            // STATISTIKA PRO BAKALÁŘKU: Úspěšné odbavení
            if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.RegisterItemDelivered();

            Debug.Log($"[Logistika OUT] Objednávka {item.ItemID} doručena na Rampu! Peníze se sypou.");

            agv.currentState = AGVState.Idle; // Vyvěsí vlajku, že je připraveno na další tasky
        }
    }
}
