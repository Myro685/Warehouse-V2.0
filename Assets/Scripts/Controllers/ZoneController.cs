using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    /// <summary>
    /// Logická oblast ležící na herní mapě zastupující dok / rampu. 
    /// Může představovat Příjem (Kamiony s novým zbožím) i Výdej (Odesílání objednávek zákazníkům).
    /// </summary>
    public class ZoneController : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("Určuje logickou barvu a pathfinding vlastnost. Inbound (Zelená), Outbound (Červená).")]
        public NodeType zoneType = NodeType.InboundZone;
        
        [Tooltip("Kde v gridu má být rampe postavena? (X, Y)")]
        public Vector2Int gridPosition;

        [Header("Runtime State")]
        [System.NonSerialized] 
        public Item currentItem = null; // Pokud je null, zóna je prázdná a může přijmout kamion

        private void Awake()
        {
            TaskSystem ts = FindFirstObjectByType<TaskSystem>();
            if (ts != null)
            {
                if (zoneType == NodeType.InboundZone && !ts.inboundZones.Contains(this)) ts.inboundZones.Add(this);
                if (zoneType == NodeType.OutboundZone && !ts.outboundZones.Contains(this)) ts.outboundZones.Add(this);
                if (zoneType == NodeType.RestingZone && !ts.restingZones.Contains(this)) ts.restingZones.Add(this);
            }
        }

        private void OnDestroy()
        {
            TaskSystem ts = FindFirstObjectByType<TaskSystem>();
            if (ts != null)
            {
                if (zoneType == NodeType.InboundZone && ts.inboundZones.Contains(this)) ts.inboundZones.Remove(this);
                if (zoneType == NodeType.OutboundZone && ts.outboundZones.Contains(this)) ts.outboundZones.Remove(this);
                if (zoneType == NodeType.RestingZone && ts.restingZones.Contains(this)) ts.restingZones.Remove(this);
            }
        }

        private void Start()
        {
            // Centrování modelu (procedurální zarovnání na milimetry dle gridu)
            GridManager gm = FindFirstObjectByType<GridManager>();
            if (gm != null)
            {
                transform.position = new Vector3(
                    gridPosition.x * gm.gridConfig.nodeSize, 
                    transform.position.y,
                    gridPosition.y * gm.gridConfig.nodeSize
                );
                
                // Přepsání uzlu na PŘÍJEM/VÝDEJ v Pathfinding 2D síti. 
                // Na rozdíl od Racků dáváme pozor na to, že AGV sem MŮŽE najet.
                Node node = gm.GetNode(gridPosition.x, gridPosition.y);
                if (node != null)
                {
                    node.Type = zoneType;
                }
            }
        }
    }
}
