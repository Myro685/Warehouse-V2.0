using System.Collections.Generic;
using WarehouseSim.Data;

namespace WarehouseSim.Core
{
    /// <summary>
    /// Společný kontrakt (rozhraní) pro implementaci mapových vyhledávacích algoritmů.
    /// Umožňuje bezproblémové přepínání strategií (např. Dijsktra vs. A*) za běhu,
    /// čímž demonstruje uplatnění návrhového vzoru Strategy (Strategy Pattern).
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Vypočítá nejoptimálnější trasu mezi výchozím a cílovým uzlem na aktuálně předané logické síti.
        /// </summary>
        /// <param name="startNode">Počáteční uzel</param>
        /// <param name="targetNode">Požadovaný cílový uzel</param>
        /// <param name="grid">Dvourozměrné pole reprezentující aktuální mapu sítě</param>
        /// <param name="expandedNodesHistory">OUT parametr vracející detailní historii POSTUPU algoritmu (posloupnost expandovaných uzlů).</param>
        /// <returns>Sekvenční seznam uzlů tvořících souvislou cestu, případně null při selhání navigace.</returns>
        List<Node> FindPath(Node startNode, Node targetNode, Node[,] grid, out List<Node> expandedNodesHistory);
    }
}
