namespace WarehouseSim.Data
{
    /// <summary>
    /// Definuje typy buněk, ze kterých se skládá náš 2D Grid skladu.
    /// Pomáhá pro pathfinding algoritmy a herní logiku.
    /// </summary>
    public enum NodeType
    {
        Empty,          // Volná cesta (AGV může projet)
        Wall,           // Neprostupná zeď (pevná překážka)
        Rack,           // Regál se zbožím (AGV sem nevjede, ale obsluhuje ze sousední buňky)
        InboundZone,    // Příjem (místo, kde se objevuje nové zboží k naskladnění)
        OutboundZone,   // Výdej (expedice, sem AGV vozí věci z regálů pro objednávky)
        RestingZone     // Odpočinková a nabíjecí zóna pro volné vozíky
    }
}
