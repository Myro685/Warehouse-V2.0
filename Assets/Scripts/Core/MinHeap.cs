using System;
using System.Collections.Generic;

namespace WarehouseSim.Core
{
    /// <summary>
    /// Generická implementace binární minimální haldy (Min-Heap) optimalizovaná
    /// pro potřeby pathfinding algoritmů. Garantuje logaritmickou 
    /// časovou složitost pro operace Insert, ExtractMin a UpdateItem.
    /// Interní HashSet zajišťuje O(1) kontrolu přítomnosti prvku.
    /// </summary>
    /// <typeparam name="T">Typ prvku implementující IComparable pro řazení.</typeparam>
    public class MinHeap<T> where T : IComparable<T>
    {
        private readonly List<T> _items = new List<T>();
        private readonly HashSet<T> _lookup = new HashSet<T>();

        /// <summary> Aktuální počet prvků v haldě. </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Vloží nový prvek do haldy a provede "bubble-up" pro zachování heap invariantu.
        /// Časová složitost: O(log n).
        /// </summary>
        public void Insert(T item)
        {
            _items.Add(item);
            _lookup.Add(item);
            BubbleUp(_items.Count - 1);
        }

        /// <summary>
        /// Extrahuje a odstraní prvek s nejnižší prioritou (kořen haldy).
        /// Časová složitost: O(log n).
        /// </summary>
        public T ExtractMin()
        {
            if (_items.Count == 0) throw new InvalidOperationException("Heap is empty.");

            T min = _items[0];
            int lastIndex = _items.Count - 1;

            _items[0] = _items[lastIndex];
            _items.RemoveAt(lastIndex);
            _lookup.Remove(min);

            if (_items.Count > 0) BubbleDown(0);

            return min;
        }

        /// <summary>
        /// O(1) kontrola přítomnosti prvku díky internímu HashSetu.
        /// </summary>
        public bool Contains(T item)
        {
            return _lookup.Contains(item);
        }

        /// <summary>
        /// Signalizuje, že se priorita daného prvku snížila (decrease-key).
        /// Vyvolá přeřazení v haldě směrem nahoru.
        /// Časová složitost: O(n) vyhledání + O(log n) bubble-up.
        /// </summary>
        public void UpdateItem(T item)
        {
            int index = _items.IndexOf(item);
            if (index >= 0)
            {
                BubbleUp(index);
            }
        }

        /// <summary>
        /// Posun prvku směrem ke kořeni haldy, dokud je menší než jeho rodič.
        /// </summary>
        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_items[index].CompareTo(_items[parentIndex]) < 0)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else break;
            }
        }

        /// <summary>
        /// Posun prvku směrem ke spodku haldy, dokud je větší než jeho potomci.
        /// </summary>
        private void BubbleDown(int index)
        {
            int count = _items.Count;
            while (true)
            {
                int smallest = index;
                int left = 2 * index + 1;
                int right = 2 * index + 2;

                if (left < count && _items[left].CompareTo(_items[smallest]) < 0)
                    smallest = left;
                if (right < count && _items[right].CompareTo(_items[smallest]) < 0)
                    smallest = right;

                if (smallest == index) break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            T temp = _items[a];
            _items[a] = _items[b];
            _items[b] = temp;
        }
    }
}
