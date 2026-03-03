using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;

namespace WarehouseSim.Core
{
    /// <summary>
    /// A* algoritmus používá k hledání cíle heuristiku (H-Cost).
    /// Díky tomu je mnohem rychlejší a zkoumá menší počet uzlů než Dijkstra,
    /// plynule tak míří směrem k cíli.
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
                
                // Najdeme uzel s nejmenším FCost (případně HCost při shodě) v seznamu ke zpracování
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

                // Našli jsme cíl
                if (currentNode == targetNode)
                {
                    return RetracePath(startNode, targetNode);
                }

                // Projdeme sousedy (nahoru, dolů, vlevo, vpravo - bez diagonál vzhledem k regálům)
                foreach (Node neighbour in GetNeighbours(currentNode, grid))
                {
                    if (!neighbour.IsWalkable || closedSet.Contains(neighbour)) continue;

                    // A* Cost = 10 (rovně)
                    int moveCost = currentNode.GCost + 10;
                    
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

            // Cesta nenalezena (např. zcela zablokováno překážkami)
            return new List<Node>();
        }

        private List<Node> RetracePath(Node startNode, Node endNode)
        {
            List<Node> path = new List<Node>();
            Node currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode);
                currentNode = currentNode.Parent;
            }
            // Získáme cestu ze startu do cíle otočením
            path.Reverse();
            return path;
        }

        private int GetDistance(Node nodeA, Node nodeB)
        {
            // Manhattan distance (pro pravoúhlý grid bez diagonál)
            int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
            int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);
            return 10 * (dstX + dstY);
        }

        private List<Node> GetNeighbours(Node node, Node[,] grid)
        {
            List<Node> neighbours = new List<Node>();
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);

            // Nahoru
            if (node.GridY + 1 < height) neighbours.Add(grid[node.GridX, node.GridY + 1]);
            // Dolů
            if (node.GridY - 1 >= 0) neighbours.Add(grid[node.GridX, node.GridY - 1]);
            // Vpravo
            if (node.GridX + 1 < width) neighbours.Add(grid[node.GridX + 1, node.GridY]);
            // Vlevo
            if (node.GridX - 1 >= 0) neighbours.Add(grid[node.GridX - 1, node.GridY]);

            return neighbours;
        }
    }
}
