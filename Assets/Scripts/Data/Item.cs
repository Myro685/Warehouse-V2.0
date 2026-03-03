namespace WarehouseSim.Data
{
    /// <summary>
    /// Reprezentuje fyzické zboží, krabici nebo paletu.
    /// Je to čistá datová třída (bez MonoBehaviour). Díky tomu může 
    /// sklad obsahovat tisíce těchto instancí v paměti zcela bez zasekávání.
    /// </summary>
    [System.Serializable]
    public class Item
    {
        public string ItemID { get; private set; }
        public string Name { get; private set; }
        public float Weight { get; private set; }

        public Item(string id, string name, float weight)
        {
            ItemID = id;
            Name = name;
            Weight = weight;
        }
    }
}
