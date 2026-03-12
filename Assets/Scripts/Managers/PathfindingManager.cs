using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using WarehouseSim.Core;
using WarehouseSim.Data;
using Debug = UnityEngine.Debug;

namespace WarehouseSim.Managers
{
    public enum PathfindingAlgorithm
    {
        AStar,
        Dijkstra
    }

    /// <summary>
    /// Komunikuje s GridManagerem, volá algoritmy a měří jejich výkon,
    /// což se hodí jako zdroj dat a statistik do bakalářské práce.
    /// </summary>
    public class PathfindingManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;

        [Header("Settings")]
        public PathfindingAlgorithm activeAlgorithm = PathfindingAlgorithm.AStar;

        private IPathfinder _aStar;
        private IPathfinder _dijkstra;

        private void Awake()
        {
            _aStar = new AStarPathfinder();
            _dijkstra = new DijkstraPathfinder();
        }

        /// <summary>
        /// Vyhledá cestu a změří čas běhu algoritmu.
        /// </summary>
        public List<Node> RequestPath(Vector2Int startCoords, Vector2Int targetCoords)
        {
            if (gridManager == null || gridManager.Grid == null)
            {
                Debug.LogError("PathfindingManager: GridManager není dostupný nebo Grid není inicializován!");
                return null;
            }

            Node startNode = gridManager.GetNode(startCoords.x, startCoords.y);
            Node targetNode = gridManager.GetNode(targetCoords.x, targetCoords.y);

            if (startNode == null || targetNode == null)
            {
                Debug.LogWarning("PathfindingManager: Neplatný start nebo cíl pro Pathfinding!");
                return null;
            }

            // DŮLEŽITÉ: Před každým hledáním vyčistíme staré G a H hodnoty paměti v Gridu.
            ResetGridCosts();

            // PŘIDÁNÍ DYNAMICKÝCH ZÁCP (Traffic Congestion Data) 
            // - abychom zabránili Včelímu Roji, kdy všechny AGV jedou po identické nejrychlejší pixeli
            var taskSystem = FindFirstObjectByType<TaskSystem>();
            if (taskSystem != null)
            {
                foreach (var agv in taskSystem.fleet)
                {
                    // Propíšeme těžkou penalizaci hmoty pod aktuálním i plánovaným umístěním každého auta
                    int curX = Mathf.RoundToInt(agv.transform.position.x / gridManager.gridConfig.nodeSize);
                    int curY = Mathf.RoundToInt(agv.transform.position.z / gridManager.gridConfig.nodeSize);
                    
                    Node n1 = gridManager.GetNode(curX, curY);
                    if (n1 != null) n1.TemporaryPenalty += 80;

                    if (agv.CurrentTargetNode.x != -1)
                    {
                        Node n2 = gridManager.GetNode(agv.CurrentTargetNode.x, agv.CurrentTargetNode.y);
                        if (n2 != null) n2.TemporaryPenalty += 80;
                    }
                    if (agv.PreviousTargetNode.x != -1)
                    {
                        Node n3 = gridManager.GetNode(agv.PreviousTargetNode.x, agv.PreviousTargetNode.y);
                        if (n3 != null) n3.TemporaryPenalty += 80;
                    }
                    if (agv.FinalTargetNode.x != -1)
                    {
                        // Extrémní penalta na místo, kde auto finálně bude manipulovat zbožím. Cizí auto si tak na 99 % najde jinou hranu regálu!
                        Node n4 = gridManager.GetNode(agv.FinalTargetNode.x, agv.FinalTargetNode.y);
                        if (n4 != null) n4.TemporaryPenalty += 500;
                    }
                }
            }

            IPathfinder pathfinder = activeAlgorithm == PathfindingAlgorithm.AStar ? _aStar : _dijkstra;

            // Stopky (C# knihovna Diagnostics) pro měření času do obhajoby
            Stopwatch sw = new Stopwatch();
            sw.Start();

            List<Node> path = pathfinder.FindPath(startNode, targetNode, gridManager.Grid);

            sw.Stop();
            Debug.Log($"[{activeAlgorithm}] Cesta nalezena. Ticks: {sw.ElapsedTicks}. Počet kroků trasy: {path.Count}");

            return path;
        }

        private void ResetGridCosts()
        {
            int w = gridManager.Grid.GetLength(0);
            int h = gridManager.Grid.GetLength(1);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    gridManager.Grid[x, y].ResetPathfinding();
                }
            }
        }
    }
}
