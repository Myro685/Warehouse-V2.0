using System.Collections.Generic;
using WarehouseSim.Data;

namespace WarehouseSim.Core
{
    /// <summary>
    /// Společné rozhraní pro všechny vyhledávací algoritmy.
    /// Umožňuje snadné přepínání algoritmů (Dijkstra vs A*) v PathfindingManageru.
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Najde nejkratší cestu z bodu A do bodu B na daném Gridu.
        /// </summary>
        /// <returns>Seznam uzlů tvořících cestu. Pokud cesta neexistuje, vrací prázdný list nebo null.</returns>
        List<Node> FindPath(Node startNode, Node targetNode, Node[,] grid);
    }
}
