using UnityEngine;

namespace WarehouseSim.ScriptableObjects
{
    /// <summary>
    /// Konfigurační soubor pro generování rozměrů skladu.
    /// Zabraňuje používání magických čísel a umožňuje rychle měnit velikost haly z Editoru.
    /// </summary>
    [CreateAssetMenu(fileName = "New Grid Config", menuName = "Warehouse Sim/Grid Config")]
    public class GridConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        [Tooltip("Počet buněk na ose X (šířka skladu)")]
        public int gridX = 20;

        [Tooltip("Počet buněk na ose Y (hloubka/délka skladu)")]
        public int gridY = 20;

        [Header("Visual Settings")]
        [Tooltip("Fyzická velikost jedné buňky v metrech (Unity jednotkách)")]
        public float nodeSize = 1f;

        [Tooltip("Mezera mezi vykreslovanými buňkami pro lepší přehlednost v Editoru")]
        public float gizmoGap = 0.1f;
    }
}
