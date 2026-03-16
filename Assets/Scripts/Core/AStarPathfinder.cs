using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;

namespace WarehouseSim.Core
{
    /// <summary>
    /// Implementace algoritmu A* (A-Star) pro hledání nejkratší cesty na mřížce.
    /// Využívá heuristiku (H-Cost) pro směrové upřednostnění uzlů, čímž minimalizuje
    /// počet iterativně prohledávaných stavů oproti Dijkstrově algoritmu.
    /// </summary>
    public class AStarPathfinder : IPathfinder
    {
        public List<Node> FindPath(Node startNode, Node targetNode, Node[,] grid)
        {
            List<Node> openSet = new List<Node>();
            HashSet<Node> closedSet = new HashSet<Node>();
            
            openSet.Add(startNode);
            startNode.GCost = 0;

            while (openSet.Count > 0)
            {
                Node currentNode = openSet[0];
                
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].FCost < currentNode.FCost || 
                        (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                    {
                        currentNode = openSet[i];
                    }
                }

                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

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
                    
                    if (moveCost < neighbour.GCost || !openSet.Contains(neighbour))
                    {
                        neighbour.GCost = moveCost;
                        neighbour.HCost = GetDistance(neighbour, targetNode);
                        neighbour.Parent = currentNode;

                        if (!openSet.Contains(neighbour))
                        {
                            openSet.Add(neighbour);
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

        /// <summary>
        /// Výpočet Manhattan distance, adekvátní pro čtvercový grid bez asymetrických úhlopříček.
        /// </summary>
        private int GetDistance(Node nodeA, Node nodeB)
        {
            int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
            int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);
            return 10 * (dstX + dstY);
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
