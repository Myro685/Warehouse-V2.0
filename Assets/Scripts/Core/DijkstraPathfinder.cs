using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;

namespace WarehouseSim.Core
{
    /// <summary>
    /// Implementace Dijkstrova algoritmu pro hledání nejkratší cesty.
    /// Abstrahuje heuristiku cíle (HCost = 0) a expanduje kruhově do dosažení cílového uzlu.
    /// Poskytuje absolutní garanci nalezení optimální cesty na úkor rozsáhlejší expanze stavového prostoru.
    /// Prioritní fronta (MinHeap) zajišťuje O(log n) extrakci minima.
    /// </summary>
    public class DijkstraPathfinder : IPathfinder
    {
        public List<Node> FindPath(Node startNode, Node targetNode, Node[,] grid, out List<Node> expandedNodesHistory)
        {
            expandedNodesHistory = new List<Node>();
            MinHeap<Node> openSet = new MinHeap<Node>();
            HashSet<Node> closedSet = new HashSet<Node>();
            
            startNode.GCost = 0;
            startNode.HCost = 0;
            openSet.Insert(startNode);

            while (openSet.Count > 0)
            {
                Node currentNode = openSet.ExtractMin();
                closedSet.Add(currentNode);
                expandedNodesHistory.Add(currentNode);

                if (currentNode == targetNode)
                {
                    return RetracePath(startNode, targetNode);
                }

                foreach (Node neighbour in GetNeighbours(currentNode, grid))
                {
                    if (!neighbour.IsWalkable || closedSet.Contains(neighbour)) continue;

                    int staticPenalty = 0;
                    if (neighbour.Type == NodeType.RestingZone) staticPenalty = 50;
                    else if (neighbour.Type == NodeType.InboundZone || neighbour.Type == NodeType.OutboundZone) staticPenalty = 30;

                    int moveCost = currentNode.GCost + 10 + staticPenalty + neighbour.TemporaryPenalty;
                    
                    if (moveCost < neighbour.GCost)
                    {
                        neighbour.GCost = moveCost;
                        neighbour.HCost = 0; 
                        neighbour.Parent = currentNode;

                        if (!openSet.Contains(neighbour))
                        {
                            openSet.Insert(neighbour);
                        }
                        else
                        {
                            openSet.UpdateItem(neighbour);
                        }
                    }
                }
            }

            return new List<Node>();
        }

        /// <summary>
        /// Zpětná rekonstrukce finální dráhy přes prekurzorové vazby.
        /// </summary>
        private List<Node> RetracePath(Node startNode, Node endNode)
        {
            List<Node> path = new List<Node>();
            Node currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode);
                currentNode = currentNode.Parent;
            }
            
            path.Reverse();
            return path;
        }

        private List<Node> GetNeighbours(Node node, Node[,] grid)
        {
            List<Node> neighbours = new List<Node>();
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);

            if (node.GridY + 1 < height) neighbours.Add(grid[node.GridX, node.GridY + 1]);
            if (node.GridY - 1 >= 0) neighbours.Add(grid[node.GridX, node.GridY - 1]);
            if (node.GridX + 1 < width) neighbours.Add(grid[node.GridX + 1, node.GridY]);
            if (node.GridX - 1 >= 0) neighbours.Add(grid[node.GridX - 1, node.GridY]);

            return neighbours;
        }
    }
}
