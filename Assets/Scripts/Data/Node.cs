using UnityEngine;

namespace WarehouseSim.Data
{
    /// <summary>
    /// Reprezentuje jedinou logickou buňku plochy skladu. 
    /// Není odvozena od MonoBehaviour za účelem striktního oddělení dat, 
    /// čímž poskytuje O(1) přístupy do stavové paměti logického Gridu.
    /// </summary>
    public class Node
    {
        // --- Identifikace prostoru ---
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public NodeType Type { get; set; }

        // --- Stavové proměnné (Pathfinding state) ---
        
        /// <summary> Indikuje propustnost uzlu pro mobilní infrastrukturu (AGV). </summary>
        public bool IsWalkable => Type == NodeType.Empty || Type == NodeType.InboundZone || 
                                  Type == NodeType.OutboundZone || Type == NodeType.RestingZone;

        public int GCost { get; set; } 
        public int HCost { get; set; } 
        
        /// <summary> Soft-kolidní modifikátor aplikovaný systémy pro řízení dopravy (Traffic Congestion). </summary>
        public int TemporaryPenalty { get; set; } = 0;
        
        public int FCost => GCost + HCost + TemporaryPenalty; 

        public Node Parent { get; set; }

        public Node(int gridX, int gridY, NodeType type = NodeType.Empty)
        {
            GridX = gridX;
            GridY = gridY;
            Type = type;
        }

        public Vector3 GetWorldPosition(float nodeSize)
        {
            return new Vector3(GridX * nodeSize, 0f, GridY * nodeSize);
        }

        /// <summary>
        /// Invalidační rutina. Vymaže dočasná navigační data před iterací nového stromu cest.
        /// </summary>
        public void ResetPathfinding()
        {
            GCost = int.MaxValue;
            HCost = 0;
            TemporaryPenalty = 0;
            Parent = null;
        }
    }
}
