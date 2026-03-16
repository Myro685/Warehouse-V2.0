using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Controllers;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Centrální registr všech alokovaných regálových prostor v simulaci.
    /// Zajišťuje O(1) referenční přístup pro dispečerské algoritmy hledající dostupné skladovací kapacity.
    /// </summary>
    public class RackManager : MonoBehaviour
    {
        [Header("System References")]
        public GridManager gridManager;

        [Header("Runtime State")]
        [SerializeField] private List<RackController> _activeRacks = new List<RackController>();

        public List<RackController> AllRacks => _activeRacks;

        /// <summary>
        /// Zprostředkovává Inversion of Control chování. Nová entita sama deklaruje 
        /// svou existenci globálnímu manažerovi a propíše svou přítomnost do navigační sítě.
        /// </summary>
        public void RegisterRack(RackController rack)
        {
            if (!_activeRacks.Contains(rack))
            {
                _activeRacks.Add(rack);
                
                Node node = gridManager.GetNode(rack.gridPosition.x, rack.gridPosition.y);
                if (node != null)
                {
                    node.Type = NodeType.Rack;
                }
            }
        }

        /// <summary>
        /// Odebírá referenci na regálový systém ze seznamu platných úložných prostor.
        /// Uvolnění logického gridu řeší příslušná destrukční rutina v BuildManageru.
        /// </summary>
        public void UnregisterRack(RackController rack)
        {
            if (_activeRacks.Contains(rack))
            {
                _activeRacks.Remove(rack);
            }
        }

        /// <summary>
        /// Vyhledá pomocí LINQ metodiky první dostupný úložný prostor disponující volnou kapacitou.
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
