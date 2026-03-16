using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    /// <summary>
    /// Logická deklarace specializované zóny na ploše simulace (Nakládací dok, Expediční rampa, atd.).
    /// </summary>
    public class ZoneController : MonoBehaviour
    {
        [Header("Zone Configuration")]
        public NodeType zoneType = NodeType.InboundZone;
        public Vector2Int gridPosition;

        [Header("Runtime Operations")]
        [System.NonSerialized] 
        public Item currentItem = null; 

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
            GridManager gm = FindFirstObjectByType<GridManager>();
            if (gm != null)
            {
                transform.position = new Vector3(
                    gridPosition.x * gm.gridConfig.nodeSize, 
                    transform.position.y,
                    gridPosition.y * gm.gridConfig.nodeSize
                );
                
                Node node = gm.GetNode(gridPosition.x, gridPosition.y);
                if (node != null)
                {
                    node.Type = zoneType;
                }
            }
        }
    }
}
