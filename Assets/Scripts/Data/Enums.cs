namespace WarehouseSim.Data
{
    /// <summary>
    /// Enumerativní identifikátory logických typů buněk 2D Gridu.
    /// Určují kolizní sémantiku navigačních algoritmů a chování manipulační techniky.
    /// </summary>
    public enum NodeType
    {
        /// <summary> Průchozí komunikace volná pro pojezd AGV. </summary>
        Empty,          
        
        /// <summary> Statická překážka neprostupná pro jakoukoliv entitu. </summary>
        Wall,           
        
        /// <summary> Alokovaný regál. AGV neprojíždí skrz, ale obsluhuje jej z přilehlého uzlu. </summary>
        Rack,           
        
        /// <summary> Zóna pro naskladňování (Inbound) externího materiálu. </summary>
        InboundZone,    
        
        /// <summary> Zóna expedičního výdeje (Outbound) pro expedici zákazníkům. </summary>
        OutboundZone,   
        
        /// <summary> Dedikovaná plocha pro nabíjení a vyčkávání nečinných vozidel flotily. </summary>
        RestingZone,    
        
        /// <summary> Rezervovaná kolizní bariéra ukrytá pod modely rozměrných regálů. </summary>
        RackPart        
    }
}
