using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using WarehouseSim.Core;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    public enum PathfindingAlgorithm
    {
        AStar,
        Dijkstra
    }

    /// <summary>
    /// Správce navigačních algoritmů. Provádí proxy volání pro abstraktní algoritmy 
    /// na poskytnuté datové mřížce. Aplikuje dynamické vážení uzlů pro řešení hustoty provozu.
    /// Zdrojem pro záchyt a sběr statistických analytik výkonu (Ticks).
    /// </summary>
    public class PathfindingManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;

        [Header("Settings")]
        public PathfindingAlgorithm activeAlgorithm = PathfindingAlgorithm.AStar;
        
        [Header("Visualization")]
        [Tooltip("Pokud je zapnuto, ukáže na podlaze skladu, jak algoritmus prohledával prostor.")]
        public bool enableLiveVisualization = true;
        public PathfindingVisualizer visualizer;

        private IPathfinder _aStar;
        private IPathfinder _dijkstra;

        private void Awake()
        {
            _aStar = new AStarPathfinder();
            _dijkstra = new DijkstraPathfinder();
        }

        /// <summary>
        /// Vyhledává trasu mezi dvěma souřadnicemi pomocí zvoleného algoritmu.
        /// Aplikuje heuristiku vyhýbání se obsazeným koridorům.
        /// </summary>
        /// <param name="startCoords">Počáteční pozice na mřížce.</param>
        /// <param name="targetCoords">Cílová pozice na mřížce.</param>
        /// <returns>Kolekce uzlů definujících platnou trasu, případně default.</returns>
        public List<Node> RequestPath(Vector2Int startCoords, Vector2Int targetCoords)
        {
            if (gridManager == null || gridManager.Grid == null)
            {
                NotificationManager.LogError("Závažná chyba: GridManager není dostupný nebo Grid není inicializován.");
                return null;
            }

            Node startNode = gridManager.GetNode(startCoords.x, startCoords.y);
            Node targetNode = gridManager.GetNode(targetCoords.x, targetCoords.y);

            if (startNode == null || targetNode == null)
            {
                NotificationManager.LogWarning("Varování: Neplatný start nebo cíl pro kalkulaci trasy.");
                return null;
            }

            ResetGridCosts();

            IPathfinder pathfinder = activeAlgorithm == PathfindingAlgorithm.AStar ? _aStar : _dijkstra;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            List<Node> path = pathfinder.FindPath(startNode, targetNode, gridManager.Grid, out List<Node> history);
            int expandedCount = history.Count;

            sw.Stop();
            
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.RegisterPathfindingMetrics(sw.ElapsedTicks, expandedCount);
            }
            
            // Spuštění animované vizualizace pro bakalářskou obhajobu
            if (enableLiveVisualization && visualizer != null && history != null)
            {
                visualizer.VisualizeSearch(history, path, activeAlgorithm, gridManager, targetNode);
            }
            
            // Analytický log přeskočen pro běžné notifikace, tiskne se do skryté konzole.
            UnityEngine.Debug.Log($"[{activeAlgorithm}] Cesta nalezena. Ticks: {sw.ElapsedTicks}. Expandováno: {expandedCount}. Trasa: {path?.Count ?? 0} uzlů.");

            return path;
        }

        /// <summary>
        /// Invalidační rutina. Nuluje heuristické a navigační parametry (G, H) sítě uzlů.
        /// </summary>
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
