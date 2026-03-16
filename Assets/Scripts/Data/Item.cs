namespace WarehouseSim.Data
{
    /// <summary>
    /// Reprezentuje fyzickou přepravní jednotku (zboží, krabici, paletu) uvnitř simulace.
    /// Koncipováno jako striktní datová třída bez závislosti na MonoBehaviour,
    /// což umožňuje paměťově nenáročné instancování tisíců položek v reálném čase.
    /// </summary>
    [System.Serializable]
    public class Item
    {
        public string ItemID { get; private set; }
        public string Name { get; private set; }
        public float Weight { get; private set; }
        
        /// <summary> Volitelná reference zprostředkovávající vazbu na konkrétní 3D grafický prefabrikát ve scéně. </summary>
        public UnityEngine.GameObject VisualModel { get; set; }

        public Item(string id, string name, float weight)
        {
            ItemID = id;
            Name = name;
            Weight = weight;
        }
    }
}
