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
        public int maxCapacity = 5;

        [Tooltip("Na jakou X, Y dlaždici Gridu chceme tento regál postavit?")]
        public Vector2Int gridPosition;

        [Header("State (Debug pouhým okem)")]
        [SerializeField] private List<Item> _storedItems = new List<Item>();

        public int CurrentItemCount => _storedItems.Count;
        public bool IsFull => CurrentItemCount >= maxCapacity;
        public bool IsEmpty => CurrentItemCount == 0;

        private void Start()
        {
            // Profesionální auto-align: Regál se posune přesně doprostřed určené buňky,
            // takže ho při návrhu levelu nemusíš ve 3D okně ručně posouvat na milimetry přesně!
            GridManager gm = FindObjectOfType<GridManager>();
            if (gm != null)
            {
                transform.position = new Vector3(
                    gridPosition.x * gm.gridConfig.nodeSize, 
                    transform.position.y, // výšku necháme, abys měl regál buď na zemi nebo levitující (podle původu modelu)
                    gridPosition.y * gm.gridConfig.nodeSize
                );
            }

            // Automatické zaregistrování do RackManageru ihned na startu (Observer Pattern)
            RackManager rm = FindObjectOfType<RackManager>();
            if (rm != null)
            {
                rm.RegisterRack(this); // Pošlu manažerovi zprávu "Ahoj, já tu stojím"
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
    }
}
