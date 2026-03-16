using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    public enum AGVState
    {
        Idle,
        MovingToPickup,
        MovingToDropoff,
        Charging
    }

    /// <summary>
    /// Řídící jednotka autonomního vozíku (AGV). 
    /// Zajišťuje fyzický pohyb po navigační mřížce, spotřebu energie a antikolizní logiku.
    /// Operace jsou asynchronně řízeny instancí TaskSystem.
    /// </summary>
    public class AGVController : MonoBehaviour
    {
        [Header("References")]
        public PathfindingManager pathfindingManager;
        public GridManager gridManager;

        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public Vector2Int startCoords = new Vector2Int(0, 0);

        [Header("State Data")]
        public AGVState currentState = AGVState.Idle;
        public Item loadedItem = null;
        public bool IsIdle => currentState == AGVState.Idle;

        [Header("Battery System")]
        public Slider batterySlider;
        public float maxBattery = 100f;
        public float currentBattery = 100f;
        public float dischargeRate = 2f;
        public float chargeRate = 15f;
        
        /// <summary> Uzel aktuálního cíle cesty, rezervovaný pro probíhající frame krok. </summary>
        public Vector2Int CurrentTargetNode { get; private set; } = new Vector2Int(-1, -1);
        
        /// <summary> Uzel, který vozík momentálně fyzicky opouští (mutex zámek). </summary>
        public Vector2Int PreviousTargetNode { get; private set; } = new Vector2Int(-1, -1);
        
        /// <summary> Křížový cíl celé cesty pro plánování delších objížděk ostatními vozidly. </summary>
        public Vector2Int FinalTargetNode { get; private set; } = new Vector2Int(-1, -1);

        private List<Node> _currentPath;
        private int _targetPathIndex;
        private bool _isMoving;
        private TaskSystem _taskSystem;

        private void Awake()
        {
            // Dynamické navázání závislostí při instancování z Prefabu
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (pathfindingManager == null) pathfindingManager = FindFirstObjectByType<PathfindingManager>();

            _taskSystem = FindFirstObjectByType<TaskSystem>();
            if (_taskSystem != null && !_taskSystem.fleet.Contains(this))
            {
                _taskSystem.fleet.Add(this);
            }
        }

        private void Update()
        {
            // Fyzikální model úbytku a obnovy energie
            if (Time.timeScale > 0f)
            {
                if (currentState == AGVState.Charging && !_isMoving)
                {
                    currentBattery += chargeRate * Time.deltaTime;
                    if (currentBattery > maxBattery) currentBattery = maxBattery;
                }
                else if (currentState != AGVState.Idle)
                {
                    currentBattery -= dischargeRate * Time.deltaTime;
                    if (currentBattery < 0f) currentBattery = 0f;
                }

                if (batterySlider != null)
                {
                    batterySlider.value = currentBattery / maxBattery;
                }
            }
        }

        private void Start()
        {
            if (gridManager != null && gridManager.Grid != null)
            {
                Node startNode = gridManager.GetNode(startCoords.x, startCoords.y);
                if (startNode != null)
                {
                    Vector3 idealPos = startNode.GetWorldPosition(gridManager.gridConfig.nodeSize);
                    transform.position = new Vector3(idealPos.x, transform.position.y, idealPos.z);
                }
            }
        }

        private void OnDestroy()
        {
            TaskSystem ts = FindFirstObjectByType<TaskSystem>();
            if (ts != null && ts.fleet.Contains(this))
            {
                ts.fleet.Remove(this);
            }
        }

        public void LoadItem(Item item)
        {
            loadedItem = item;
        }

        public Item UnloadItem()
        {
            Item temp = loadedItem;
            loadedItem = null;
            return temp;
        }

        /// <summary>
        /// Vyhledá optimální přístupový bod z půdorysu regálu a zahájí přesun.
        /// </summary>
        public void MoveToAndNotify(RackController rack, System.Action onComplete)
        {
            Vector2Int bestCoord = FindWalkableNeighbor(rack.GetFootprint());
            MoveToAndNotify(bestCoord, onComplete);
        }

        /// <summary>
        /// Vypočítá trasu k cílovým souřadnicím a asynchronně zahájí pohybovou rutinu.
        /// </summary>
        public void MoveToAndNotify(Vector2Int targetCoords, System.Action onComplete)
        {
            StopAllCoroutines();
            _isMoving = false;

            int currentX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int currentY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);

            Node targetNode = gridManager.GetNode(targetCoords.x, targetCoords.y);
            if (targetNode != null && !targetNode.IsWalkable)
            {
                targetCoords = FindWalkableNeighbor(new List<Vector2Int>(){ targetCoords });
            }

            FinalTargetNode = targetCoords;
            _currentPath = pathfindingManager.RequestPath(new Vector2Int(currentX, currentY), targetCoords);

            if (_currentPath != null && _currentPath.Count > 0)
            {
                _targetPathIndex = 0;
                StartCoroutine(FollowPathRoutine(onComplete));
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Analyzuje okolí požadovaných uzlů a vrací nejkratší neblokovaný přístupový bod (Node).
        /// </summary>
        private Vector2Int FindWalkableNeighbor(List<Vector2Int> targetFootprint)
        {
            Vector2Int[] dirs = { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };
            
            int currentX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int currentY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);

            Vector2Int bestNode = targetFootprint[0];
            float minDistance = float.MaxValue;
            Vector2Int bestOccupiedNode = targetFootprint[0];
            float minOccupiedDistance = float.MaxValue;

            foreach (var target in targetFootprint)
            {
                foreach (var d in dirs)
                {
                    Node n = gridManager.GetNode(target.x + d.x, target.y + d.y);
                    if (n != null && n.IsWalkable) 
                    {
                        float dist = Vector2.Distance(new Vector2(currentX, currentY), new Vector2(n.GridX, n.GridY));
                        
                        if (!IsNodeOccupiedByOtherAGV(new Vector2Int(n.GridX, n.GridY), true))
                        {
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestNode = new Vector2Int(n.GridX, n.GridY);
                            }
                        }
                        else
                        {
                            if (dist < minOccupiedDistance)
                            {
                                minOccupiedDistance = dist;
                                bestOccupiedNode = new Vector2Int(n.GridX, n.GridY);
                            }
                        }
                    }
                }
            }
            
            if (minDistance == float.MaxValue) return bestOccupiedNode;
            
            return bestNode;
        }

        private IEnumerator FollowPathRoutine(System.Action onComplete)
        {
            _isMoving = true;

            while (_targetPathIndex < _currentPath.Count)
            {
                Node targetNode = _currentPath[_targetPathIndex];
                Vector2Int nextGridPos = new Vector2Int(targetNode.GridX, targetNode.GridY);
                
                float waitTimer = 0f;
                bool _pathRecalculated = false;

                // Flow-Control systém pro předcházení Deadlocků vozidel na jedné ose
                while (IsNodeOccupiedByOtherAGV(nextGridPos, false))
                {
                    yield return null;
                    waitTimer += Time.deltaTime;
                    
                    if (waitTimer > 2f)
                    {
                        waitTimer = 0f; 
                        
                        Node obstacle = gridManager.GetNode(nextGridPos.x, nextGridPos.y);
                        Node finalTgt = _currentPath[_currentPath.Count - 1]; 
                        
                        if (obstacle != null && nextGridPos != new Vector2Int(finalTgt.GridX, finalTgt.GridY))
                        {
                            NodeType oldType = obstacle.Type;
                            obstacle.Type = NodeType.Wall;
                            
                            int actX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                            int actY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                            
                            List<Node> newPath = pathfindingManager.RequestPath(new Vector2Int(actX, actY), new Vector2Int(finalTgt.GridX, finalTgt.GridY));
                            obstacle.Type = oldType;  
                            
                            if (newPath != null && newPath.Count > 0)
                            {
                                NotificationManager.LogInfo($"AGV Detekována překážka u {nextGridPos}. Tvorba objízdné trasy.");
                                _currentPath = newPath;
                                _targetPathIndex = 0;
                                _pathRecalculated = true;
                                break;
                            }
                        }
                    }
                }

                if (_pathRecalculated) continue;

                int curX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                int curY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                PreviousTargetNode = new Vector2Int(curX, curY); 
                CurrentTargetNode = nextGridPos; 
                
                Vector3 targetWorldPos = targetNode.GetWorldPosition(gridManager.gridConfig.nodeSize);

                if (AnalyticsManager.Instance != null && _targetPathIndex > 0)
                {
                    AnalyticsManager.Instance.RegisterNodeVisited(targetNode.GridX, targetNode.GridY);
                }

                while (Vector3.Distance(transform.position, targetWorldPos) > 0.05f)
                {
                    Vector3 prevPos = transform.position;
                    transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
                    
                    if (AnalyticsManager.Instance != null) 
                        AnalyticsManager.Instance.AddDistance(Vector3.Distance(prevPos, transform.position));

                    Vector3 direction = (targetWorldPos - transform.position).normalized;
                    if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
                    yield return null;
                }

                transform.position = targetWorldPos;
                PreviousTargetNode = new Vector2Int(-1, -1);
                _targetPathIndex++;
            }

            CurrentTargetNode = new Vector2Int(-1, -1);
            PreviousTargetNode = new Vector2Int(-1, -1);
            FinalTargetNode = new Vector2Int(-1, -1);
            _isMoving = false;
            _currentPath = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Validuje propustnost zadaného uzlu v závislosti na ostatních aktivních AGV z TaskSystemu.
        /// </summary>
        /// <param name="nodePos">Požadovaný uzel.</param>
        /// <param name="checkFinalTarget">Vyloučí uzel, pokud slouží jako koncový bod pro jiné vozidlo.</param>
        private bool IsNodeOccupiedByOtherAGV(Vector2Int nodePos, bool checkFinalTarget = false)
        {
            if (_taskSystem == null) return false;

            foreach (var agv in _taskSystem.fleet)
            {
                if (agv == this) continue;

                int agvX = Mathf.RoundToInt(agv.transform.position.x / gridManager.gridConfig.nodeSize);
                int agvY = Mathf.RoundToInt(agv.transform.position.z / gridManager.gridConfig.nodeSize);

                if (agvX == nodePos.x && agvY == nodePos.y) return true;
                if (agv.PreviousTargetNode == nodePos) return true;
                if (agv.CurrentTargetNode == nodePos) return true;
                if (checkFinalTarget && agv.FinalTargetNode == nodePos) return true;
            }
            return false;
        }
    }
}
