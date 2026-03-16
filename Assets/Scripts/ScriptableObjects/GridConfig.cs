using UnityEngine;

namespace WarehouseSim.ScriptableObjects
{
    /// <summary>
    /// Serializovatelný datový kontejner nastavení haly.
    /// Účelem je eliminace tzv. Magic Numbers (magických čísel) ze zdrojových kódů a 
    /// delegování architektonických parametrů sítě přímo do editoru Unity metodikou Data-Driven Design.
    /// </summary>
    [CreateAssetMenu(fileName = "New Grid Config", menuName = "Warehouse Sim/Grid Config")]
    public class GridConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        [Tooltip("Počet buněk na ose X (Horizontální rozsah mapy)")]
        public int gridX = 20;

        [Tooltip("Počet buněk na ose Y (Vertikální hloubka mapy)")]
        public int gridY = 20;

        [Header("Interpolation Properties")]
        [Tooltip("Skutečná metrická výška i šířka jedné buňky v globálním souřadnicovém systému (Základ = 1 metr).")]
        public float nodeSize = 1f;

        [Tooltip("Mezerovitost mezi renderovanými Gizmo obrysy v Editoru (pomáhá s čitelností vývojářské sítě).")]
        public float gizmoGap = 0.1f;
    }
}
