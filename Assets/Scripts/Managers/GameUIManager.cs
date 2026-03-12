using UnityEngine;
using UnityEngine.UI;
using WarehouseSim.Managers;
using TMPro;

namespace WarehouseSim.UI
{
    /// <summary>
    /// Slouží jako bezpečný prostředník mezi 2D Canvasem (Tlačítky, Texty) 
    /// a hlubokými 3D systémy skladu. Cílem je zrušit nutnost používat Inspector 
    /// pro ovládání logistiky během spuštěné hry.
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("Systémy skladu (Backend)")]
        public TaskSystem taskSystem;
        public PathfindingManager pathfindingManager;
        public RackManager rackManager;

        [Header("UI Texty na Obrazovce (Frontend)")]
        public TextMeshProUGUI txtCapacity;
        public TextMeshProUGUI txtJobsInfo;
        public TextMeshProUGUI txtAlgorithmInfo;
        public TextMeshProUGUI txtAnalytics; // NEW: Kolonka pro analytiku

        private void Update()
        {
            RefreshDashboard();
        }

        /// <summary>
        /// Čte živá data ze skladu a propisuje je do textů na obrazovce.
        /// </summary>
        private void RefreshDashboard()
        {
            if (rackManager != null && txtCapacity != null)
            {
                int maxPotential = 0;
                int storedCount = 0;
                foreach (var r in rackManager.AllRacks) 
                {
                    maxPotential += r.maxCapacity;
                    storedCount += r.CurrentItemCount;
                }

                txtCapacity.text = $"Zaplněnost skladu: {storedCount} / {maxPotential}";
            }

            if (taskSystem != null && txtJobsInfo != null)
            {
                int workingCount = taskSystem.fleet.FindAll(a => !a.IsIdle).Count;
                txtJobsInfo.text = $"Aktivní mise AGV: {workingCount} / {taskSystem.fleet.Count}";
            }

            if (pathfindingManager != null && txtAlgorithmInfo != null)
            {
                txtAlgorithmInfo.text = $"Aktivní mozek tras: {pathfindingManager.activeAlgorithm}";
            }

            if (AnalyticsManager.Instance != null && txtAnalytics != null)
            {
                txtAnalytics.text = $"Ujeta vzdálenost: {Mathf.RoundToInt(AnalyticsManager.Instance.TotalDistanceTraveled)}m | Expedováno: {AnalyticsManager.Instance.TotalItemsDelivered}";
            }
        }

        // ==========================================
        // Tlačítka z plochy (voláno přes OnClick v Editoru)
        // ==========================================

        public void BtnAction_OrderInbound()
        {
            if (taskSystem != null) taskSystem.CreateInboundTask();
        }

        public void BtnAction_OrderOutbound()
        {
            if (taskSystem != null) taskSystem.CreateOutboundTask();
        }

        public void BtnAction_SwitchAlgorithm()
        {
            if (pathfindingManager != null)
            {
                // Překlapávání mezi A* a Dijkstrou pouhým stiskem tlačítka
                if (pathfindingManager.activeAlgorithm == PathfindingAlgorithm.AStar)
                    pathfindingManager.activeAlgorithm = PathfindingAlgorithm.Dijkstra;
                else
                    pathfindingManager.activeAlgorithm = PathfindingAlgorithm.AStar;
            }
        }
    }
}
