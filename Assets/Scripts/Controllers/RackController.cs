using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    /// <summary>
    /// Komponenta nasazená přímo na 3D modelu regálu (kostky) v Unity scéně.
    /// Sama se při startu nahlásí do centrálního RackManagera a spravuje svou vnitřní kapacitu.
    /// Zabraňuje nutnosti manuálního zadávání dat, vše se děje přes Ocenění Kontroly (IoC).
    /// </summary>
    public class RackController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Kolik palet nebo krabic (třída Item) sem maximálně vlezou?")]
        public int maxCapacity = 48;

        [Tooltip("Na jakou X, Y dlaždici Gridu chceme tento regál postavit?")]
        public Vector2Int gridPosition;

        [Header("Vizuální Úchyty pro Krabice (Snap Points)")]
        [Tooltip("Volitelné: Zde přetáhni z editoru prázdné GameObjekty, kam mají krabice ve 3D fyzicky zapadnout. AGV je tam bude milimetrově usazovat.")]
        public List<Transform> visualSlots = new List<Transform>();

        [Header("State (Debug pouhým okem)")]
        [SerializeField] private List<Item> _storedItems = new List<Item>();

        public int CurrentItemCount => _storedItems.Count;
        public bool IsFull => CurrentItemCount >= maxCapacity;
        public bool IsEmpty => CurrentItemCount == 0;

        // "Virtuální zámky" - Zabrání tomu, aby dispečer poslal 10 aut do regálu, kde je místo jenom pro 1.
        public int PendingIncomingItems { get; set; } = 0;
        public int PendingOutgoingItems { get; set; } = 0;

        // Vrací pravdu, pokud se sečtením fyzických balíků i těch očekávaných na cestě do regálu vejdeme
        public bool HasSpaceForNewItem => (CurrentItemCount + PendingIncomingItems) < maxCapacity;
        // Vrací pravdu, pokud v regálu zbyde zboží i po tom, co si z dálky jedoucí auta naberou to své
        public bool HasAvailableItemForPickup => (CurrentItemCount - PendingOutgoingItems) > 0;

        private void Start()
        {
            // Profesionální auto-align: Regál se posune přesně doprostřed určené buňky,
            // takže ho při návrhu levelu nemusíš ve 3D okně ručně posouvat na milimetry přesně!
            GridManager gm = FindFirstObjectByType<GridManager>();
            if (gm != null)
            {
                transform.position = new Vector3(
                    gridPosition.x * gm.gridConfig.nodeSize, 
                    transform.position.y, // výšku necháme, abys měl regál buď na zemi nebo levitující (podle původu modelu)
                    gridPosition.y * gm.gridConfig.nodeSize
                );
            }

            // Automatické zaregistrování do RackManageru ihned na startu (Observer Pattern)
            RackManager rm = FindFirstObjectByType<RackManager>();
            if (rm != null)
            {
                rm.RegisterRack(this); // Pošlu manažerovi zprávu "Ahoj, já tu stojím"
            }
        }

        private void OnDestroy()
        {
            RackManager rm = FindFirstObjectByType<RackManager>();
            if (rm != null)
            {
                rm.UnregisterRack(this);
            }
        }

        /// <summary>
        /// AGVčko zavolá tuto metodu, pokud přijede vyložit náklad.
        /// </summary>
        public bool StoreItem(Item newItem)
        {
            if (IsFull)
            {
                Debug.LogWarning($"[Regál {gridPosition}] Nelze uskladnit zboží '{newItem.Name}'. Regál je plný!");
                return false;
            }

            _storedItems.Add(newItem);
            return true;
        }

        /// <summary>
        /// AGVčko nebo Objednávkový systém si takto vyžádá poslední zboží ven (LIFO - Last In First Out).
        /// </summary>
        public Item RetrieveItem()
        {
            if (IsEmpty)
            {
                Debug.LogWarning($"[Regál {gridPosition}] Je prázdný, nemám co vydat.");
                return null;
            }

            int lastIndex = _storedItems.Count - 1;
            Item itemToRetrieve = _storedItems[lastIndex];
            _storedItems.RemoveAt(lastIndex); // Fyzické odebrání z paměti

            return itemToRetrieve;
        }

        /// <summary>
        /// Vyhledá přesnou souřadnici úchytu v regálu pro nově přijíždějící položku.
        /// </summary>
        public Transform GetNextVisualSlot()
        {
            if (visualSlots == null || visualSlots.Count == 0) return null;
            
            // Kolikátou věc právě teď máme v poli uloženou?
            int index = _storedItems.Count - 1; 
            
            if (index >= 0 && index < visualSlots.Count) return visualSlots[index];

            // Pokud kapacita překročí počet namalovaných 3D slotů, hází se to graficky na poslední místo
            return visualSlots[visualSlots.Count - 1]; 
        }

        /// <summary>
        /// Vrátí všechny uzly, které tento velký objekt ve 2D síti fyzicky zabírá.
        /// (Zatím hardcoded na náš 4x1 model z Asset Store)
        /// </summary>
        public List<Vector2Int> GetFootprint()
        {
            List<Vector2Int> footprint = new List<Vector2Int>();
            for(int i = 0; i < 4; i++) footprint.Add(new Vector2Int(gridPosition.x + i, gridPosition.y));
            return footprint;
        }
    }
}
