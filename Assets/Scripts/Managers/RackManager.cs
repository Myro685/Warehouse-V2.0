using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Controllers;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Globální "katalog" nad všemi regály. Ví o každém RackControlleru ve scéně,
    /// a dokáže řídícím systémům rychle najít volný regál s kapacity pro nové palety.
    /// </summary>
    public class RackManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;

        // Již nezadáváme souřadnice krkolomně ručně! Tento list se vyplní zcela automaticky,
        // protože každý fyzický vytvořený RackController se sem na startu sám nahlásí.
        [Header("Runtime State")]
        [SerializeField] private List<RackController> _activeRacks = new List<RackController>();

        public List<RackController> AllRacks => _activeRacks;

        /// <summary>
        /// Tato funkce je volána z RackControlleru jako součást samo-nastavovacího enginu.
        /// </summary>
        public void RegisterRack(RackController rack)
        {
            if (!_activeRacks.Contains(rack))
            {
                _activeRacks.Add(rack);
                
                // Překlopíme naši buňku na PŘEKÁŽKU, aby o tom AStar a Dijkstra hned věděli!
                Node node = gridManager.GetNode(rack.gridPosition.x, rack.gridPosition.y);
                if (node != null)
                {
                    node.Type = NodeType.Rack;
                }
            }
        }

        public void UnregisterRack(RackController rack)
        {
            if (_activeRacks.Contains(rack))
            {
                _activeRacks.Remove(rack);
                // Poznámka: O re-změnu NodeType samotného pole v gridu se stará Buldozer v BuildManageru
            }
        }

        /// <summary>
        /// Systém příjmu se nás takto naprogramovaně zeptá: "Kde ještě máš prázdno?"
        /// (Používá LINQ framework k nalezení první vyhovující kostky)
        /// </summary>
        public RackController GetAvailableRackForStorage()
        {
            return _activeRacks.Find(r => r.HasSpaceForNewItem);
        }

        public int GetTotalRacksCount()
        {
            return _activeRacks.Count;
        }
    }
}
