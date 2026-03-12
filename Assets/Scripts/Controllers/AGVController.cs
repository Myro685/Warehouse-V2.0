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
        
        // Zásadní mutuační zámek - uzel, ze kterého auto právě odjíždí, ale zatím ho fyzicky 100% neopustilo
        public Vector2Int PreviousTargetNode { get; private set; } = new Vector2Int(-1, -1);
        
        // Kde má auto úplně finální cíl své cesty?
        public Vector2Int FinalTargetNode { get; private set; } = new Vector2Int(-1, -1);

        private List<Node> _currentPath;
        private int _targetPathIndex;
        private bool _isMoving;
        private TaskSystem _taskSystem;
        private void Awake()
        {
            // Záchrana Prefabů: Když se z auta stal Prefab projektového souboru,
            // smazaly se mu ukazatele do scény pro Grid a Pathfinding. Najdeme je proto kódem!
            if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
            if (pathfindingManager == null) pathfindingManager = FindFirstObjectByType<PathfindingManager>();

            // Automatická self-registrace IHNED při zrodu (Awake je rychlejší než Start)
            _taskSystem = FindFirstObjectByType<TaskSystem>();
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
            TaskSystem ts = FindFirstObjectByType<TaskSystem>();
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
        /// <summary>
        /// Přetížení pro inteligentní obsluhu celého komplexního regálu (obvod)
        /// </summary>
        public void MoveToAndNotify(RackController rack, System.Action onComplete)
        {
            Vector2Int bestCoord = FindWalkableNeighbor(rack.GetFootprint());
            MoveToAndNotify(bestCoord, onComplete);
        }

        public void MoveToAndNotify(Vector2Int targetCoords, System.Action onComplete)
        {
            StopAllCoroutines();
            _isMoving = false;

            int currentX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int currentY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);

            // Klasické jednobodové bariéry
            Node targetNode = gridManager.GetNode(targetCoords.x, targetCoords.y);
            if (targetNode != null && !targetNode.IsWalkable)
            {
                targetCoords = FindWalkableNeighbor(new List<Vector2Int>(){ targetCoords });
            }

            FinalTargetNode = targetCoords; // Zapíšeme finální cíl cesty pro ostatní radarující auta
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
        /// Vyhodnotí okolí všech předaných uzlů a najde nejkratší přístupový bod (Ideální pro 4x1 modely)
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
                        
                        // Zkontrolujeme, zda tam už nestojí cizí auto!
                        // DŮLEŽITÉ: U regálů CHCEME kontrolovat i finalTarget, aby si dvě auta nevybrala stejnou dlaždici k obsluze
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
                            // Uložíme si toto místo jako "Smolařskou zálohu" pro případ, že regál obstoupila jiná auta zcela ze všech stran
                            if (dist < minOccupiedDistance)
                            {
                                minOccupiedDistance = dist;
                                bestOccupiedNode = new Vector2Int(n.GridX, n.GridY);
                            }
                        }
                    }
                }
            }
            
            // Pojistka: Pokud jsme nenašli VŮBEC ŽÁDNÝ volný přístupový bod (všechny hrany blokují auta), 
            // vrátíme ten zablokovaný a necháme naše auto narazit na Anti-Deadlock časovač, ať si s tím poradí.
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
                
                // ==== ANTI-KOLIZNÍ RADAR ====
                float waitTimer = 0f;
                bool _pathRecalculated = false;

                // Dokud je před námi překážka v podobě cizího auta (fyzicky nebo plánovaně)
                // Pro běžnou jízdu IGNORUJEME finalTarget. Když přes políčko auto jen projíždí, nesmí ho blokovat cizí FinalTarget!
                while (IsNodeOccupiedByOtherAGV(nextGridPos, false))
                {
                    yield return null; // Zastavíme a propustíme smyčku (Čekáme)
                    waitTimer += Time.deltaTime;
                    
                    if (waitTimer > 2f)
                    {
                        // DEADLOCK RESOLUTION (Pojistka proti záseku)
                        waitTimer = 0f; 
                        
                        Node obstacle = gridManager.GetNode(nextGridPos.x, nextGridPos.y);
                        Node finalTgt = _currentPath[_currentPath.Count - 1]; // Kam jsme měli celkově dojet?
                        
                        // Zásadní oprava pro 2 vozíky potkávající se u regálu: 
                        // Pokud je bariéra SAMOTNÝ NÁŠ CÍL (Auto 1 zrovna nakládá), NESMÍME zkoušet objížďku, 
                        // protože vytvořením zdi z našeho cíle pathfinding logicky zhavaruje. Musíme si prostě počkat!
                        if (obstacle != null && nextGridPos != new Vector2Int(finalTgt.GridX, finalTgt.GridY))
                        {
                            // Dočasně zalžeme algoritmu, že zamýšlený cizí uzel na trase je tvrdá neoblomná Zeď
                            NodeType oldType = obstacle.Type;
                            obstacle.Type = NodeType.Wall;
                            
                            int actX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                            int actY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                            
                            // Dynamický přepočet A* z aktuální pozice
                            List<Node> newPath = pathfindingManager.RequestPath(new Vector2Int(actX, actY), new Vector2Int(finalTgt.GridX, finalTgt.GridY));
                            
                            // Vrátíme světidla uzlu do reality, abychom nenabourali simulaci
                            obstacle.Type = oldType;  
                            
                            if (newPath != null && newPath.Count > 0)
                            {
                                Debug.Log($"[AGV Deadlock] Zablokováno u {nextGridPos}. Tvoříme Objížďku!");
                                _currentPath = newPath;
                                _targetPathIndex = 0;
                                _pathRecalculated = true;
                                break; // UPUŠTĚNÍ RADARU: Skončilo čekání na uzel, máme novou trasu, prorazit vnitřní while!
                            }
                        }
                    }
                }

                if (_pathRecalculated) continue; // Přejít na další Frame vnější smyčky a nařídit pohyb podle zcela Nové Trasy (_currentPath[0])!

                // Zamluvení prostoru pro přejezd
                int curX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
                int curY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);
                PreviousTargetNode = new Vector2Int(curX, curY); // Zamkneme dlaždici pod kufrem (tu, kterou opouštíme)
                
                CurrentTargetNode = nextGridPos; // Zamluvíme si uzel pro sebe (přední nárazník)!
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
                PreviousTargetNode = new Vector2Int(-1, -1); // Kufr opustil předchozí uzel. Může tam najet kdokoli další.
                _targetPathIndex++;
            }

            CurrentTargetNode = new Vector2Int(-1, -1); // Uvolnění rezervace po dojetí do cíle
            PreviousTargetNode = new Vector2Int(-1, -1);
            FinalTargetNode = new Vector2Int(-1, -1);
            _isMoving = false;
            _currentPath = null;
            onComplete?.Invoke(); // UPOZORNĚNÍ -> Haló, dojel jsem do cíle! (Zavolá TaskSystem)
        }

        private bool IsNodeOccupiedByOtherAGV(Vector2Int nodePos, bool checkFinalTarget = false)
        {
            if (_taskSystem == null) return false;

            foreach (var agv in _taskSystem.fleet)
            {
                if (agv == this) continue; // Sami sebe ignorujeme

                // 1) Kontrola fyzicky aktuálního bloku, kde překážející vozidlo zrovna stojí
                int agvX = Mathf.RoundToInt(agv.transform.position.x / gridManager.gridConfig.nodeSize);
                int agvY = Mathf.RoundToInt(agv.transform.position.z / gridManager.gridConfig.nodeSize);

                if (agvX == nodePos.x && agvY == nodePos.y) return true;
                
                // MULTI-NODE MUTEX: Pokud auto A opouští uzel Z, má ho zamčený v PreviousTargetNode do té doby, dokud celým tělem není v novém uzlu Y.
                if (agv.PreviousTargetNode == nodePos) return true;
                
                // 2) Kontrola rezervace - místo, kam má cizí auto namířeno vklouznout v tomto zlomku vteřiny
                if (agv.CurrentTargetNode == nodePos) return true;
                
                // 3) Kontrola koncového zaparkování - je vůbec smysluplné jet tam, kam někdo jiný už pluje zaparkovat nadobro?
                // Děláme to POUZE pokud to je výslovně vyžádáno (při hledání volné zóny), ne po cestě uličkou!
                if (checkFinalTarget && agv.FinalTargetNode == nodePos) return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            if (_currentPath != null && _currentPath.Count > 0)
            {
                Gizmos.color = Color.cyan;
                Vector3 startPos = transform.position;
                if (gridManager != null && gridManager.gridConfig != null)
                {
                    Vector3 firstNodePos = _currentPath[0].GetWorldPosition(gridManager.gridConfig.nodeSize);
                    Gizmos.DrawLine(startPos, firstNodePos);

                    for (int i = 0; i < _currentPath.Count - 1; i++)
                    {
                        Vector3 a = _currentPath[i].GetWorldPosition(gridManager.gridConfig.nodeSize);
                        Vector3 b = _currentPath[i + 1].GetWorldPosition(gridManager.gridConfig.nodeSize);
                        Gizmos.DrawLine(a, b);
                    }
                    
                    // Vykreslení cíle
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(_currentPath[_currentPath.Count - 1].GetWorldPosition(gridManager.gridConfig.nodeSize), new Vector3(0.5f, 0.5f, 0.5f));
                }
            }
        }
    }
}
