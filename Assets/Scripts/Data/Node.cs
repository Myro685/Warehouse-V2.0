using UnityEngine;

namespace WarehouseSim.Data
{
    /// <summary>
    /// Reprezentuje jednu logickou buňku plochy skladu. 
    /// Není odvozena od MonoBehaviour kvůli striktnímu oddělení dat a vizualizace.
    /// Třída bude žít výhradně v paměti C# a poskytovat O(1) přístupy.
    /// </summary>
    public class Node
    {
        // --- Základní identifikace ---
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public NodeType Type { get; set; }

        // --- Stavové proměnné pathfindingu ---
        // Vlastnost zjistí, zda se na danou buňku dá fyzicky najet vozíkem.
        public bool IsWalkable => Type == NodeType.Empty || Type == NodeType.InboundZone || 
                                  Type == NodeType.OutboundZone || Type == NodeType.RestingZone;

        // Vzdálenost od startovního uzlu (G-Cost pomáhá pro A* a Dijkstru)
        public int GCost { get; set; } 
        // Odhadovaná vzdálenost k cíli (Heuristika H-Cost je pouze pro A*)
        public int HCost { get; set; } 
        // Skrytá dynamická penalta pro zácpy a vykrytí stojících vozů
        public int TemporaryPenalty { get; set; } = 0;
        // Celková váha/cena uzlu (F = G + H + dynamické vlivy)
        public int FCost => GCost + HCost + TemporaryPenalty; 

        // Rodičovský uzel (Přes co jsme sem dojeli). 
        // Používá se pro poskládání trasy zpět po nalezení cíle.
        public Node Parent { get; set; }

        /// <summary>
        /// Konstruktor vytvoří datový uzel na přesných souřadnicích v Gridu.
        /// </summary>
        public Node(int gridX, int gridY, NodeType type = NodeType.Empty)
        {
            GridX = gridX;
            GridY = gridY;
            Type = type;
        }

        /// <summary>
        /// Metoda pro přepočet [X, Y] 2D pole na reálnou pozici do 3D pohledu v Unity Editoru.
        /// Předpokládá Y (výšku) = 0.
        /// </summary>
        /// <param name="nodeSize">Metrická šířka a výška buňky (např. 1 jednotka = 1 metr)</param>
        public Vector3 GetWorldPosition(float nodeSize)
        {
            return new Vector3(GridX * nodeSize, 0f, GridY * nodeSize);
        }

        /// <summary>
        /// Čistí stará dočasná data před dalším spuštěním A* nebo Dijkstry,
        /// jinak by nová trasa byla ovlivněna tou předchozí!
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
