# Uživatelský Manuál: Warehouse Simulator V2.0

## 1. Úvod
**Warehouse Simulator V2.0** je logistický nástroj pro návrh, testování a datovou analýzu virtuálních skladovacích hal. Cílem simulace je koordinace flotily autonomních vozíků (AGV - Autonomous Guided Vehicles), které obsluhují importní a exportní procesy (příjem a výdej palet z/do regálů). 

Aplikace funguje ve dvou základních režimech:
1. **Režim stavby (Zastavený čas):** Návrh plochy, umisťování infrastruktury.
2. **Režim simulace (Spuštěný čas):** Samotný běh procesů a testování algoritmů.

---

## 2. Navigace a Zobrazení dat (Horní panel)
V horní části okna uvidíte panel se statistikami (dashboard), který v reálném čase reaguje na dění v simulaci:
* **Zaplněnost:** Zobrazuje zaplněnou kapacitu vůči celkové teoretické kapacitě skladu definovanou umístěnými regály.
* **Aktivní mise:** Ukazuje, kolik vozíků právě transportuje paletu či míří k regálu, vůči celkovému množství vozíků ve skladu.
* **Algoritmus:** Informuje o aktuálně zvoleném způsobu vyhledávání nejkratší trasy (zda je aktivní **A\*** nebo **Dijkstra**).
* **Analytika:** Zobrazuje živě aktualizovanou ujetou vzdálenost (v metrech) a počet úspěšně expedovaných zásilek.

Vedle textových informací jsou k dispozici ovládací prvky času simulace:
* **Start / Pozastavit:** Při stavbě nového skladu musí být simulace *Pozastavena*. *Start* oživí vozíky a začne dynamicky přidělovat úkoly.
* **Rychlost simulace (Slider):** Ovlivňuje běh času enginu – rychlost logiky.
* **Rychlost objednávek (Slider):** Pokud je aktivováno generování náhodných objednávek, určuje tento posuvník časový interval mezi novými objednávkami.

---

## 3. Režim Stavby a Infrastruktura (Pravé Menu)
Z pravého ovládacího sloupce vybíráte konkrétní stavební nástroj. Pro umisťování objektů do sítě (gridu) následně klikejte levým tlačítkem myši do prostoru simulace.

* **Regál:** Stavební kámen skladu. Má 4 pole. Vozíky do něj budou ukládat zboží.
* **Zeď (Wall):** Slouží k vytváření bariér – vozíky tudy neprojedou.
* **Zóna Příjmu (Inbound):** Místo, kam přicházejí objednávky (nové zboží). Vozíky ho odsud nabírají a posílají do regálů.
* **Zóna Výdeje (Outbound):** Expediční bod. Vozík sem přiváží zboží vybrané z regálu.
* **Zóna Dobíjení (Resting):** Parkovací a dobíjecí místo pro nevyužité vozíky.
* **Vložit Vozík (AGV):** Vloží novou jednotku autonomního vozíku do mapy.
* **Delete:** Mazací nástroj. Kliknutím na prvek v ploše ho odstraníte.

---

## 4. Akce a Manuální Spouštění Objednávek
Dokud je čas Pozastaven, vozíky nic nedělají. Po stisku tlačítka **Start**, můžete řídit logistiku manuálním způsobem, nebo nechat probíhat zátěžový test:

1. **Příjem:** Tlačítko okamžitě manuálně vygeneruje novou paletu na zóně příjmu a najde volný AGV k jejímu odvozu.
2. **Výdej:** Najde regál se zbožím a pošle AGV vozík k naložení a doručení do *Zóny výdeje*.
3. **Změnit Algoritmus:** Tlačítko přepíná způsob hledání tras pro vozíky. Vyzkoušejte rozdíly (A* vs Dijkstra) v různých návrzích skladového bludiště.

---

## 5. Další Nástroje (Správa, Heatmapa, Data)
* **Heatmap / Analytics UI:** Detailní pohled na využití skladu (frekvence průjezdu atp). *Poznámka: Ve hře bývá v pop-upu Pokročilá Analýza Skladu.*
* **Uložit & Načíst:** Umožňuje uložit aktuální rozložení infrastruktury i vozíků do lokálního souboru na disk a později se k návrhu vrátit.
* **Statistiky & Export Dat:** Generuje a ukládá CSV report z proběhlého testování do výchozí složky instalace. Report obsahuje finální měřené statistiky testu (délku doručení, kapacity atp.). Lze takto exportovat a přímo v datech srovnávat různá zadání.

---

## 6. Krátký návod pro první krok (Quickstart):
1. V horním panelu mějte čas nastavený na **Pozastavena**.
2. Klikněte v pravém menu na **Zeď** a nakreslete zdi do plochy.
3. Postavte řady pomocí nástroje **Regál**.
4. Označte na jednom konci skladu **Zónu příjmu** a na druhém **Zónu výdeje**.
5. Vytvořte rohovou garáž pomocí pár políček **Zóny dobíjení**.
6. Do hlavních koridorů umístěte několik modelů přes **Vložit Vozík**.
7. Nyní stiskněte horní tlačítko **Start**.
8. Klikejte na **Příjem** a **Výdej** a sledujte plně autonomní flotilu v akci!
