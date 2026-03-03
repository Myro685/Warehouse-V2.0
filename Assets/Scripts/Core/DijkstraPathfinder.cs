using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;

namespace WarehouseSim.Core
{
    /// <summary>
    /// Dijkstra algoritmus nezná vzdálenost k cíli (heuristiku).
    /// Prohledává prostor rovnoměrně na všechny strany, dokud na cíl nenarazí.
    /// Vrací vždy nejkratší cestu, ale za cenu vyššího výpočetního výkonu a prohledaných buněk než A*.
    /// </summary>
    public class DijkstraPathfinder : IPathfinder
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
                
                // Dijkstra hledí pouze na GCost (vzdálenost od startu), nemá HCost
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].GCost < currentNode.GCost)
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

                    int moveCost = currentNode.GCost + 10;
                    
                    if (moveCost < neighbour.GCost || !openSet.Contains(neighbour))
                    {
                        neighbour.GCost = moveCost;
                        // Dijkstra vůbec nepoužívá heuristiku a H-Cost zůstává ignorována
                        neighbour.HCost = 0; 
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
