using UnityEngine;
using UnityEngine.UI;
using WarehouseSim.Managers;
using TMPro;

namespace WarehouseSim.UI
{
    /// <summary>
    /// Řídící třída uživatelského rozhraní. Zprostředkovává obousměrnou komunikaci 
    /// mezi interaktivními 2D prvky (Canvas) a backend systémy simulace.
    /// Zajišťuje vizualizaci dat v reálném čase.
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("Backend Systems")]
        public TaskSystem taskSystem;
        public PathfindingManager pathfindingManager;
        public RackManager rackManager;

        [Header("UI Dashboard Text")]
        public TextMeshProUGUI txtCapacity;
        public TextMeshProUGUI txtJobsInfo;
        public TextMeshProUGUI txtAlgorithmInfo;
        public TextMeshProUGUI txtAnalytics;

        [Header("UI Controls")]
        public Button btnPlay;
        public Button btnPause;

        private float _currentSimulationSpeed = 1f;

        private void Start()
        {
            Time.timeScale = 0f;
            
            if (btnPlay != null) btnPlay.interactable = true;
            if (btnPause != null) btnPause.interactable = false; 
        }

        private void Update()
        {
            RefreshDashboard();
        }

        /// <summary>
        /// Agreguje živá data ze simulačních vrstev a synchronizuje je s textovými prvky rozhraní.
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
                txtAlgorithmInfo.text = $"Aktivní trasovací algoritmus: {pathfindingManager.activeAlgorithm}";
            }

            if (AnalyticsManager.Instance != null && txtAnalytics != null)
            {
                txtAnalytics.text = $"Ujetá vzdálenost: {Mathf.RoundToInt(AnalyticsManager.Instance.TotalDistanceTraveled)} m | Expedováno: {AnalyticsManager.Instance.TotalItemsDelivered}";
            }
        }

        // ==========================================
        // UI Action Handlers
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
                if (pathfindingManager.activeAlgorithm == PathfindingAlgorithm.AStar)
                    pathfindingManager.activeAlgorithm = PathfindingAlgorithm.Dijkstra;
                else
                    pathfindingManager.activeAlgorithm = PathfindingAlgorithm.AStar;
            }
        }

        // ==========================================
        // Řízení času a zátěžových testů
        // ==========================================

        public void BtnAction_PlaySimulation()
        {
            if (_currentSimulationSpeed <= 0f) _currentSimulationSpeed = 1f;
            Time.timeScale = _currentSimulationSpeed;
            
            if (taskSystem != null) taskSystem.stressTestMixed = true; 
            
            if (btnPlay != null) btnPlay.interactable = false; 
            if (btnPause != null) btnPause.interactable = true;
        }

        public void BtnAction_PauseSimulation()
        {
            Time.timeScale = 0f;
            
            if (taskSystem != null) taskSystem.stressTestMixed = false; 
            
            if (btnPlay != null) btnPlay.interactable = true;
            if (btnPause != null) btnPause.interactable = false;
        }

        public void SliderAction_SetSimulationSpeed(float speed)
        {
            _currentSimulationSpeed = Mathf.Max(0.1f, speed);
            
            if (Time.timeScale != 0f)
            {
                Time.timeScale = _currentSimulationSpeed;
            }
        }

        public void SliderAction_SetOrderInterval(float interval)
        {
            if (taskSystem != null)
            {
                taskSystem.stressTestInterval = Mathf.Max(0.2f, interval);
            }
        }

        // ==========================================
        // Datový Export
        // ==========================================
        
        public void BtnAction_ExportReport()
        {
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.ExportToCSV();
                
                if (txtAnalytics != null) txtAnalytics.text += "\n<color=green>✓ Export uložen ve standardu na disk (.csv)</color>";
            }
        }
    }
}
