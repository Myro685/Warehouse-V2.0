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
        [Header("Identity")]
        public int AgvId = 0;

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
        
        /// <summary> Čas strávený ve stavu Idle — chrání před okamžitým parkováním. </summary>
        [System.NonSerialized] public float IdleTimer = 0f;

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
                AgvId = _taskSystem.fleet.Count;
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
                
                // Počítadlo doby nečinnosti pro ochranu před okamžitým parkováním
                if (currentState == AGVState.Idle)
                    IdleTimer += Time.deltaTime;
                else
                    IdleTimer = 0f;
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
                        Vector2Int candidatePos = new Vector2Int(n.GridX, n.GridY);
                        float dist = Vector2.Distance(new Vector2(currentX, currentY), new Vector2(n.GridX, n.GridY));
                        
                        // Kontrola: uzel nesmí být obsazen ANI nesmí být FinalTarget jiného AGV
                        bool isPhysicallyOccupied = IsNodeOccupiedByOtherAGV(candidatePos, false);
                        bool isReservedAsTarget = IsNodeReservedByOtherAGV(candidatePos);
                        
                        if (!isPhysicallyOccupied && !isReservedAsTarget)
                        {
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestNode = candidatePos;
                            }
                        }
                        else
                        {
                            if (dist < minOccupiedDistance)
                            {
                                minOccupiedDistance = dist;
                                bestOccupiedNode = candidatePos;
                            }
                        }
                    }
                }
            }
            
            if (minDistance == float.MaxValue) return bestOccupiedNode;
            
            return bestNode;
        }

        /// <summary>
        /// Kontroluje, zda je uzel rezervován jako FinalTargetNode jiného AGV.
        /// Brání více vozíkům mířit na stejný přístupový bod.
        /// </summary>
        private bool IsNodeReservedByOtherAGV(Vector2Int nodePos)
        {
            if (_taskSystem == null) return false;
            foreach (var agv in _taskSystem.fleet)
            {
                if (agv == this) continue;
                if (agv.FinalTargetNode == nodePos) return true;
            }
            return false;
        }

        private IEnumerator FollowPathRoutine(System.Action onComplete)
        {
            _isMoving = true;

            while (_targetPathIndex < _currentPath.Count)
            {
                Node targetNode = _currentPath[_targetPathIndex];
                Vector2Int nextGridPos = new Vector2Int(targetNode.GridX, targetNode.GridY);
                
                float waitTimer = 0f;
                float totalWaitTimer = 0f;
                int repathAttempts = 0;
                bool _pathRecalculated = false;

                // Flow-Control systém s prioritním symmetry-breaking pro řešení deadlocků
                while (IsNodeOccupiedByOtherAGV(nextGridPos, false))
                {
                    yield return null;
                    waitTimer += Time.deltaTime;
                    totalWaitTimer += Time.deltaTime;
                    
                    // Vozík s nižší prioritou (vyšší AgvId) ustupuje dříve
                    float waitThreshold = ShouldYield(nextGridPos) ? 1.0f : 2.5f;
                    
                    if (waitTimer > waitThreshold)
                    {
                        waitTimer = 0f;
                        repathAttempts++;
                        
                        Node finalTgt = _currentPath[_currentPath.Count - 1];
                        Vector2Int finalTargetPos = new Vector2Int(finalTgt.GridX, finalTgt.GridY);
                        
                        // === PŘÍPAD A: Blokující uzel JE náš cílový bod ===
                        // Jiný AGV stojí přímo na našem cíli — musíme najít alternativní cíl
                        if (nextGridPos == finalTargetPos)
                        {
                            if (repathAttempts >= 2)
                            {
                                // Couvnout a najít jiný přístupový bod
                                bool retreated = false;
                                yield return StartCoroutine(RetreatOneNode((success) => retreated = success));
                                
                                // Najít alternativní walkable neighbor (s vyloučením obsazeného)
                                Vector2Int altTarget = FindAlternativeDestination(finalTargetPos);
                                if (altTarget != finalTargetPos)
                                {
                                    NotificationManager.LogInfo($"AGV#{AgvId} Cíl {finalTargetPos} obsazen. Přesměrování na {altTarget}.");
                                    FinalTargetNode = altTarget;
                                    int actX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                                    int actY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                                    List<Node> newPath = pathfindingManager.RequestPath(new Vector2Int(actX, actY), altTarget);
                                    if (newPath != null && newPath.Count > 0)
                                    {
                                        _currentPath = newPath;
                                        _targetPathIndex = 0;
                                        _pathRecalculated = true;
                                        break;
                                    }
                                }
                                repathAttempts = 0;
                            }
                            continue;
                        }
                        
                        // === PŘÍPAD B: Blokující uzel NENÍ náš cíl — repath kolem něj ===
                        
                        // Po 3 neúspěšných repatech — couvnutí o jeden uzel
                        if (repathAttempts > 3)
                        {
                            bool retreated = false;
                            yield return StartCoroutine(RetreatOneNode((success) => retreated = success));
                            
                            if (retreated)
                            {
                                NotificationManager.LogInfo($"AGV#{AgvId} Couvnutí o 1 uzel pro uvolnění koridoru.");
                                int actX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                                int actY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                                List<Node> newPath = pathfindingManager.RequestPath(new Vector2Int(actX, actY), finalTargetPos);
                                if (newPath != null && newPath.Count > 0)
                                {
                                    _currentPath = newPath;
                                    _targetPathIndex = 0;
                                    _pathRecalculated = true;
                                    break;
                                }
                            }
                            repathAttempts = 0;
                        }
                        
                        // Vylepšený repath — blokuje všechny uzly blokujícího AGV
                        {
                            List<NodeType> savedTypes = new List<NodeType>();
                            List<Node> blockedNodes = new List<Node>();
                            
                            Node obstacleNode = gridManager.GetNode(nextGridPos.x, nextGridPos.y);
                            if (obstacleNode != null)
                            {
                                savedTypes.Add(obstacleNode.Type);
                                blockedNodes.Add(obstacleNode);
                                obstacleNode.Type = NodeType.Wall;
                            }
                            
                            AGVController blocker = GetBlockingAGV(nextGridPos);
                            if (blocker != null)
                            {
                                int bx = Mathf.RoundToInt(blocker.transform.position.x / gridManager.gridConfig.nodeSize);
                                int by = Mathf.RoundToInt(blocker.transform.position.z / gridManager.gridConfig.nodeSize);
                                Vector2Int blockerPos = new Vector2Int(bx, by);
                                
                                if (blockerPos != nextGridPos)
                                {
                                    Node blockerNode = gridManager.GetNode(bx, by);
                                    if (blockerNode != null && blockerNode.IsWalkable)
                                    {
                                        savedTypes.Add(blockerNode.Type);
                                        blockedNodes.Add(blockerNode);
                                        blockerNode.Type = NodeType.Wall;
                                    }
                                }
                                
                                if (blocker.CurrentTargetNode.x >= 0)
                                {
                                    Node blockerTarget = gridManager.GetNode(blocker.CurrentTargetNode.x, blocker.CurrentTargetNode.y);
                                    if (blockerTarget != null && blockerTarget.IsWalkable && !blockedNodes.Contains(blockerTarget))
                                    {
                                        savedTypes.Add(blockerTarget.Type);
                                        blockedNodes.Add(blockerTarget);
                                        blockerTarget.Type = NodeType.Wall;
                                    }
                                }
                            }
                            
                            int actX2 = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                            int actY2 = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                            
                            List<Node> newPath = pathfindingManager.RequestPath(new Vector2Int(actX2, actY2), finalTargetPos);
                            
                            for (int i = 0; i < blockedNodes.Count; i++)
                            {
                                blockedNodes[i].Type = savedTypes[i];
                            }
                            
                            if (newPath != null && newPath.Count > 0)
                            {
                                NotificationManager.LogInfo($"AGV#{AgvId} Detekována překážka u {nextGridPos}. Tvorba objízdné trasy.");
                                _currentPath = newPath;
                                _targetPathIndex = 0;
                                _pathRecalculated = true;
                                break;
                            }
                        }
                    }
                    
                    // Absolutní timeout — couvnutí + přepočet celé cesty + alternativní cíl
                    if (totalWaitTimer > 10f)
                    {
                        NotificationManager.LogWarning($"AGV#{AgvId} Timeout čekání ({totalWaitTimer:F1}s). Nouzový manévr.");
                        
                        // Couvnout
                        bool retreated = false;
                        yield return StartCoroutine(RetreatOneNode((success) => retreated = success));
                        
                        int actX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                        int actY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                        Node finalTgt2 = _currentPath[_currentPath.Count - 1];
                        Vector2Int currentFinalPos = new Vector2Int(finalTgt2.GridX, finalTgt2.GridY);
                        
                        // Zkusit alternativní cíl pokud je stávající obsazen
                        Vector2Int targetForRepath = currentFinalPos;
                        if (IsNodeOccupiedByOtherAGV(currentFinalPos, false))
                        {
                            Vector2Int altTarget = FindAlternativeDestination(currentFinalPos);
                            if (altTarget != currentFinalPos)
                            {
                                targetForRepath = altTarget;
                                FinalTargetNode = altTarget;
                            }
                        }
                        
                        List<Node> emergencyPath = pathfindingManager.RequestPath(
                            new Vector2Int(actX, actY), targetForRepath);
                        if (emergencyPath != null && emergencyPath.Count > 0)
                        {
                            _currentPath = emergencyPath;
                            _targetPathIndex = 0;
                            _pathRecalculated = true;
                        }
                        break;
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
        /// Určí, zda by tento vozík měl ustoupit blokujícímu AGV (symmetry breaking).
        /// Vozík s vyšším AgvId ustupuje, čímž se rozbije symetrický deadlock.
        /// </summary>
        private bool ShouldYield(Vector2Int blockedNodePos)
        {
            AGVController blocker = GetBlockingAGV(blockedNodePos);
            if (blocker == null) return false;
            return AgvId > blocker.AgvId;
        }

        /// <summary>
        /// Najde AGV, které blokuje zadaný uzel.
        /// </summary>
        private AGVController GetBlockingAGV(Vector2Int nodePos)
        {
            if (_taskSystem == null) return null;

            foreach (var agv in _taskSystem.fleet)
            {
                if (agv == this) continue;

                int agvX = Mathf.RoundToInt(agv.transform.position.x / gridManager.gridConfig.nodeSize);
                int agvY = Mathf.RoundToInt(agv.transform.position.z / gridManager.gridConfig.nodeSize);

                if (agvX == nodePos.x && agvY == nodePos.y) return agv;
                if (agv.PreviousTargetNode == nodePos) return agv;
                if (agv.CurrentTargetNode == nodePos) return agv;
            }
            return null;
        }

        /// <summary>
        /// Najde nejbližší volnou pozici kolem obsazeného cíle (kruhově do vzdálenosti 3).
        /// </summary>
        private Vector2Int FindAlternativeDestination(Vector2Int blockedTarget)
        {
            Vector2Int bestAlt = blockedTarget;
            float minAltDist = float.MaxValue;
            
            for (int r = 1; r <= 3; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                        
                        Vector2Int candidate = new Vector2Int(blockedTarget.x + x, blockedTarget.y + y);
                        Node n = gridManager.GetNode(candidate.x, candidate.y);
                        
                        if (n != null && n.IsWalkable)
                        {
                            bool isPhysOccupied = IsNodeOccupiedByOtherAGV(candidate, false);
                            bool isReserved = IsNodeReservedByOtherAGV(candidate);
                            
                            if (!isPhysOccupied && !isReserved)
                            {
                                int cx = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                                int cy = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                                float dist = Vector2.Distance(new Vector2(cx, cy), candidate);
                                
                                if (dist < minAltDist)
                                {
                                    minAltDist = dist;
                                    bestAlt = candidate;
                                }
                            }
                        }
                    }
                }
                if (minAltDist != float.MaxValue) break; // Nalezeno v tomto poloměru
            }
            return bestAlt;
        }

        /// <summary>
        /// Couvne o jeden uzel zpět na trase, aby uvolnil prostor pro protijedoucí vozík.
        /// </summary>
        private IEnumerator RetreatOneNode(System.Action<bool> onComplete)
        {
            int curX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int curY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
            
            // Zkusit ustoupit do všech 4 směrů, preferovat volné uzly
            Vector2Int[] dirs = { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };
            
            foreach (var d in dirs)
            {
                Vector2Int retreatPos = new Vector2Int(curX + d.x, curY + d.y);
                Node retreatNode = gridManager.GetNode(retreatPos.x, retreatPos.y);
                
                if (retreatNode != null && retreatNode.IsWalkable && !IsNodeOccupiedByOtherAGV(retreatPos, false))
                {
                    Vector3 retreatWorldPos = retreatNode.GetWorldPosition(gridManager.gridConfig.nodeSize);
                    PreviousTargetNode = new Vector2Int(curX, curY);
                    CurrentTargetNode = retreatPos;
                    
                    while (Vector3.Distance(transform.position, retreatWorldPos) > 0.05f)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, retreatWorldPos, moveSpeed * Time.deltaTime);
                        Vector3 direction = (retreatWorldPos - transform.position).normalized;
                        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
                        yield return null;
                    }
                    
                    transform.position = retreatWorldPos;
                    PreviousTargetNode = new Vector2Int(-1, -1);
                    CurrentTargetNode = new Vector2Int(-1, -1);
                    onComplete?.Invoke(true);
                    yield break;
                }
            }
            
            onComplete?.Invoke(false);
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
