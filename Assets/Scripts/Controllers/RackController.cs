using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    /// <summary>
    /// Komponenta reprezentující skladovací úložný prostor (Rack) uvnitř simulačního prostředí.
    /// Konfiguruje abstraktní kapacitu regálu a zprostředkovává IoC (Inversion of Control)
    /// integraci do globálního alokátoru kapacit (RackManager).
    /// </summary>
    public class RackController : MonoBehaviour
    {
        [Header("Capacity Settings")]
        public int maxCapacity = 48;
        public Vector2Int gridPosition;

        [Header("Visual Interpolation Points")]
        public List<Transform> visualSlots = new List<Transform>();

        [Header("Inventory State")]
        [SerializeField] private List<Item> _storedItems = new List<Item>();

        public int CurrentItemCount => _storedItems.Count;
        public bool IsFull => CurrentItemCount >= maxCapacity;
        public bool IsEmpty => CurrentItemCount == 0;

        /// <summary> Predikční mutex systém odstiňující souběh (Race-conditions) alokování logistiky. </summary>
        public int PendingIncomingItems { get; set; } = 0;
        public int PendingOutgoingItems { get; set; } = 0;

        public bool HasSpaceForNewItem => (CurrentItemCount + PendingIncomingItems) < maxCapacity;
        public bool HasAvailableItemForPickup => (CurrentItemCount - PendingOutgoingItems) > 0;

        private void Start()
        {
            GridManager gm = FindFirstObjectByType<GridManager>();
            if (gm != null)
            {
                transform.position = new Vector3(
                    gridPosition.x * gm.gridConfig.nodeSize, 
                    transform.position.y, 
                    gridPosition.y * gm.gridConfig.nodeSize
                );
            }

            RackManager rm = FindFirstObjectByType<RackManager>();
            if (rm != null)
            {
                rm.RegisterRack(this); 
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
        /// Zpracovává příchozí fyzickou jednotku materiálu a provede přírůstek v interním poli.
        /// </summary>
        public bool StoreItem(Item newItem)
        {
            if (IsFull) return false;

            _storedItems.Add(newItem);
            return true;
        }

        /// <summary>
        /// Zprostředkovává LIFO (Last-In-First-Out) odbavení uskladněného zboží.
        /// </summary>
        public Item RetrieveItem()
        {
            if (IsEmpty) return null;

            int lastIndex = _storedItems.Count - 1;
            Item itemToRetrieve = _storedItems[lastIndex];
            _storedItems.RemoveAt(lastIndex); 

            return itemToRetrieve;
        }

        /// <summary>
        /// Vrátí volnou transformaci indexovanou aktuální kapacitou pro vizuální umístění 3D modelu Itemu.
        /// </summary>
        public Transform GetNextVisualSlot()
        {
            if (visualSlots == null || visualSlots.Count == 0) return null;
            
            int index = _storedItems.Count - 1; 
            
            if (index >= 0 && index < visualSlots.Count) return visualSlots[index];

            return visualSlots[visualSlots.Count - 1]; 
        }

        /// <summary>
        /// Vrátí dvourozměrný půdorys překrytí uzlů, na kterých existují fyzické bariéry objektu.
        /// </summary>
        public List<Vector2Int> GetFootprint()
        {
            List<Vector2Int> footprint = new List<Vector2Int>();
            for(int i = 0; i < 4; i++) footprint.Add(new Vector2Int(gridPosition.x + i, gridPosition.y));
            return footprint;
        }
    }
}
