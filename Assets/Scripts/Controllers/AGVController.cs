using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    // Celočíselný Stavový automat (Finite State Machine)
    public enum AGVState
    {
        Idle,
        MovingToPickup,
        MovingToDropoff,
        Charging
    }

    /// <summary>
    /// Mozek samotného vozíku. Přehodili jsme jeho logiku přes callbacky (události),
    /// aby byl pod plnou nadvládou centrálního TaskSystemu.
    /// </summary>
    public class AGVController : MonoBehaviour
    {
        [Header("References")]
        public PathfindingManager pathfindingManager;
        public GridManager gridManager;

        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public Vector2Int startCoords = new Vector2Int(0, 0);

        [Header("State Data (Pro bakalářku a monitorování)")]
        public AGVState currentState = AGVState.Idle;
        public Item loadedItem = null;
        public bool IsIdle => currentState == AGVState.Idle;
        
        // Dynamická paměť pro antikolizní systém (Kde se bude auto nacházet za milisekundu)
        public Vector2Int CurrentTargetNode { get; private set; } = new Vector2Int(-1, -1);

        private List<Node> _currentPath;
        private int _targetPathIndex;
        private bool _isMoving;
        private TaskSystem _taskSystem;
        private void Awake()
        {
            // Záchrana Prefabů: Když se z auta stal Prefab projektového souboru,
            // smazaly se mu ukazatele do scény pro Grid a Pathfinding. Najdeme je proto kódem!
            if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
            if (pathfindingManager == null) pathfindingManager = FindObjectOfType<PathfindingManager>();

            // Automatická self-registrace IHNED při zrodu (Awake je rychlejší než Start)
            _taskSystem = FindObjectOfType<TaskSystem>();
            if (_taskSystem != null && !_taskSystem.fleet.Contains(this))
            {
                _taskSystem.fleet.Add(this);
            }
        }

        private void Start()
        {
            if (gridManager != null && gridManager.Grid != null)
            {
                Node startNode = gridManager.GetNode(startCoords.x, startCoords.y);
                if (startNode != null)
                {
                    // Zachování výšky modelu z BuildManageru (Y hodnota), mění se jen X a Z pro Grid-Align
                    Vector3 idealPos = startNode.GetWorldPosition(gridManager.gridConfig.nodeSize);
                    transform.position = new Vector3(idealPos.x, transform.position.y, idealPos.z);
                }
            }
        }

        private void OnDestroy()
        {
            // Odhlášení ze směny při zničení auta
            TaskSystem ts = FindObjectOfType<TaskSystem>();
            if (ts != null && ts.fleet.Contains(this))
            {
                ts.fleet.Remove(this);
            }
        }

        public void LoadItem(Item item)
        {
            loadedItem = item;
            // TODO: Zde přidáme aktivaci vizuální kostky krabice na vozíku
        }

        public Item UnloadItem()
        {
            Item temp = loadedItem;
            loadedItem = null;
            return temp;
        }

        /// <summary>
        /// Srdce chytrosti AGV - dojede na místo a samo pošle TaskSystemu zprávu (Action onComplete), že tam je.
        /// </summary>
        public void MoveToAndNotify(Vector2Int targetCoords, System.Action onComplete)
        {
            StopAllCoroutines();
            _isMoving = false;

            int currentX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int currentY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);

            // !! EXTRÉMNĚ CHYTRÝ DETAIL !!
            // Pokud jede k RACKU (Regálu), tak by Pathfinding vyhodil chybu, protože Regál svítí jako Zeď.
            // Naučíme vozík zastavit Z BOKU/ZEPŘEDU u regálu!
            Node targetNode = gridManager.GetNode(targetCoords.x, targetCoords.y);
            if (targetNode != null && !targetNode.IsWalkable)
            {
                targetCoords = FindWalkableNeighbor(targetCoords.x, targetCoords.y);
            }

            _currentPath = pathfindingManager.RequestPath(new Vector2Int(currentX, currentY), targetCoords);

            if (_currentPath != null && _currentPath.Count > 0)
            {
                _targetPathIndex = 0;
                StartCoroutine(FollowPathRoutine(onComplete));
            }
            else
            {
                // Jakmile narazí na chybu nebo tam už jsme, rovnou zahlásíme "skončil jsem cestu".
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Najde volné políčko vedle regálu. Inteligentně vybere to políčko,
        /// které je ze stran regálu NEJBLÍŽE aktuální poloze přijíždějícího vozidla!
        /// Zabraňuje nelogickému objíždění regálu zbytečně na druhou stranu.
        /// </summary>
        private Vector2Int FindWalkableNeighbor(int targetX, int targetY)
        {
            Vector2Int[] dirs = { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };
            
            int currentX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int currentY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);

            Vector2Int bestNode = new Vector2Int(targetX, targetY);
            float minDistance = float.MaxValue;

            foreach (var d in dirs)
            {
                Node n = gridManager.GetNode(targetX + d.x, targetY + d.y);
                if (n != null && n.IsWalkable) 
                {
                    float dist = Vector2.Distance(new Vector2(currentX, currentY), new Vector2(n.GridX, n.GridY));
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestNode = new Vector2Int(n.GridX, n.GridY);
                    }
                }
            }
            return bestNode;
        }

        private IEnumerator FollowPathRoutine(System.Action onComplete)
        {
            _isMoving = true;

            while (_targetPathIndex < _currentPath.Count)
            {
                Node targetNode = _currentPath[_targetPathIndex];
                Vector2Int nextGridPos = new Vector2Int(targetNode.GridX, targetNode.GridY);
                
                // ==== ANTI-KOLIZNÍ RADAR ====
                float waitTimer = 0f;
                // Dokud je před námi překážka v podobě cizího auta (fyzicky nebo plánovaně)
                while (IsNodeOccupiedByOtherAGV(nextGridPos))
                {
                    yield return null; // Zastavíme a propustíme smyčku (Čekáme)
                    waitTimer += Time.deltaTime;
                    
                    if (waitTimer > 2f)
                    {
                        // Deadlock pojistka - po 2 sekundách troubení resetneme timer
                        // V plně produkčním WMS (Warehouse Management) by se zde aktivoval
                        // dynamický A* přepočet s přidáním cizího auta jako dočasné Stěny do Gridu.
                        // Pro účely simulace stačí trpělivost.
                        waitTimer = 0f; 
                    }
                }

                CurrentTargetNode = nextGridPos; // Zamluvíme si uzel pro sebe!
                Vector3 targetWorldPos = targetNode.GetWorldPosition(gridManager.gridConfig.nodeSize);

                // Záznam stopy pro budoucí vybarvování tepelné mapy
                if (AnalyticsManager.Instance != null && _targetPathIndex > 0)
                {
                    AnalyticsManager.Instance.RegisterNodeVisited(targetNode.GridX, targetNode.GridY);
                }

                while (Vector3.Distance(transform.position, targetWorldPos) > 0.05f)
                {
                    Vector3 prevPos = transform.position;
                    transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
                    
                    // Exaktní sběr ujetých decimetrů pro statistiku!
                    if (AnalyticsManager.Instance != null) 
                        AnalyticsManager.Instance.AddDistance(Vector3.Distance(prevPos, transform.position));

                    Vector3 direction = (targetWorldPos - transform.position).normalized;
                    if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
                    yield return null;
                }

                transform.position = targetWorldPos;
                _targetPathIndex++;
            }

            CurrentTargetNode = new Vector2Int(-1, -1); // Uvolnění rezervace po dojetí do cíle
            _isMoving = false;
            _currentPath = null;
            onComplete?.Invoke(); // UPOZORNĚNÍ -> Haló, dojel jsem do cíle! (Zavolá TaskSystem)
        }

        private bool IsNodeOccupiedByOtherAGV(Vector2Int nodePos)
        {
            if (_taskSystem == null) return false;

            foreach (var agv in _taskSystem.fleet)
            {
                if (agv == this) continue; // Sami sebe ignorujeme

                // 1) Kontrola fyzicky aktuálního bloku, kde překážející vozidlo zrovna stojí
                int agvX = Mathf.RoundToInt(agv.transform.position.x / gridManager.gridConfig.nodeSize);
                int agvY = Mathf.RoundToInt(agv.transform.position.z / gridManager.gridConfig.nodeSize);

                if (agvX == nodePos.x && agvY == nodePos.y) return true;
                
                // 2) Kontrola rezervace - místo, kam má cizí auto namířeno vklouznout v tomto zlomku vteřiny
                if (agv.CurrentTargetNode == nodePos) return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            if (_currentPath != null && gridManager != null && _isMoving)
            {
                Gizmos.color = Color.magenta;
                if (_targetPathIndex < _currentPath.Count)
                    Gizmos.DrawLine(transform.position, _currentPath[_targetPathIndex].GetWorldPosition(gridManager.gridConfig.nodeSize));

                for (int i = _targetPathIndex; i < _currentPath.Count - 1; i++)
                {
                    Vector3 p1 = _currentPath[i].GetWorldPosition(gridManager.gridConfig.nodeSize);
                    Vector3 p2 = _currentPath[i + 1].GetWorldPosition(gridManager.gridConfig.nodeSize);
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }
    }
}
