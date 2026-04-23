using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) 
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default;

    public int Count { get; protected set; }
    
    public bool IsReadOnly => false;

    // Временные заглушки, переопределенные ниже
    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList().AsReadOnly();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList().AsReadOnly();
    
    
    public virtual void Add(TKey key, TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        // Если дерево пустое
        if (Root == null)
        {
            Root = CreateNode(key, value);
            Count++;
            OnNodeAdded(Root);
            return;
        }

        TNode? current = Root;
        TNode? parent = null;

        // Ищем место для вставки или существующий ключ
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            
            if (cmp == 0)
            {
                // Ключ уже существует - ОБНОВЛЯЕМ значение
                current.Value = value;
                return;
            }
            
            parent = current;
            current = cmp < 0 ? current.Left : current.Right;
        }

        // Если дошли сюда, значит ключа нет, создаем новый узел
        TNode newNode = CreateNode(key, value);
        newNode.Parent = parent;
        
        if (Comparer.Compare(key, parent!.Key) < 0)
            parent.Left = newNode;
        else
            parent.Right = newNode;

        Count++;
        OnNodeAdded(newNode);
    }

    
    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null) { return false; }

        RemoveNode(node);
        this.Count--;
        return true;
    }
    
    
    protected virtual void RemoveNode(TNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        // Случай 1: Узел не имеет левого ребенка
        if (node.Left == null)
        {
            Transplant(node, node.Right);
        }
        // Случай 2: Узел имеет только левого ребенка
        else if (node.Right == null)
        {
            Transplant(node, node.Left);
        }
        // Случай 3: Узел имеет двух детей
        else
        {
            // Находим преемника (минимальный узел в правом поддереве)
            TNode successor = FindMinimum(node.Right!);
            
            // Если преемник не является прямым правым ребенком
            if (successor.Parent != node)
            {
                // Заменяем преемника его правым ребенком (если есть)
                Transplant(successor, successor.Right);
                // Правый ребенок преемника становится правым ребенком удаляемого узла
                successor.Right = node.Right;
                if (successor.Right != null)
                    successor.Right.Parent = successor;
            }
            
            // Заменяем удаляемый узел преемником
            Transplant(node, successor);
            successor.Left = node.Left;
            if (successor.Left != null)
                successor.Left.Parent = successor;
        }
        
        OnNodeRemoved(node.Parent, node.Left ?? node.Right);
    }
    
    private TNode FindMinimum(TNode node)
    {
        while (node.Left != null)
        {
            node = node.Left;
        }
        return node;
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? val) ? val : throw new KeyNotFoundException();
        set => Add(key, value); // Теперь Add обновляет значение, если ключ есть
    }

    
    #region Hooks
    
    /// <summary>
    /// Вызывается после успешной вставки
    /// 
    /// <param name="newNode">Узел, который встал на место</param>
    protected virtual void OnNodeAdded(TNode newNode) { }
    
    /// <summary>
    /// Вызывается после удаления. 
    /// </summary>
    /// <param name="parent">Узел, чей ребенок изменился</param>
    /// <param name="child">Узел, который встал на место удаленного</param>
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }
    
    #endregion
    
    
    #region Helpers
    protected abstract TNode CreateNode(TKey key, TValue value);
    
    
    
    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) { return current; }
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected void RotateLeft(TNode x)
    {
        TNode? y = x.Right;
        if (y == null) return;
        
        // Перемещаем левое поддерево y к правому поддереву x
        x.Right = y.Left;
        if (y.Left != null)
            y.Left.Parent = x;
        
        // Устанавливаем родителя y
        y.Parent = x.Parent;
        
        if (x.Parent == null)
        {
            Root = y;
        }
        else if (x.IsLeftChild)
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }
        
        // x становится левым ребенком y
        y.Left = x;
        x.Parent = y;
    }

    protected void RotateRight(TNode y)
    {
        TNode? x = y.Left;
        if (x == null) return;
        
        // Перемещаем правое поддерево x к левому поддереву y
        y.Left = x.Right;
        if (x.Right != null)
            x.Right.Parent = y;
        
        // Устанавливаем родителя x
        x.Parent = y.Parent;
        
        if (y.Parent == null)
        {
            Root = x;
        }
        else if (y.IsLeftChild)
        {
            y.Parent.Left = x;
        }
        else
        {
            y.Parent.Right = x;
        }
        
        // y становится правым ребенком x
        x.Right = y;
        y.Parent = x;
    }
    
    protected void RotateBigLeft(TNode x)
    {
        // Большой левый поворот: сначала правый поворот на правом ребенке, затем левый на x
        if (x.Right != null)
            RotateRight(x.Right);
        RotateLeft(x);
    }
    
    protected void RotateBigRight(TNode y)
    {
        // Большой правый поворот: сначала левый поворот на левом ребенке, затем правый на y
        if (y.Left != null)
            RotateLeft(y.Left);
        RotateRight(y);
    }
    
    protected void RotateDoubleLeft(TNode x)
    {
        // 1. Проверяем возможность первого поворота
        if (x.Left == null) return;
        // Сохраняем ссылку на будущего нового корня (левый ребенок) ДО поворота, 
        // но будьте осторожны: после поворота связи изменятся.
        // На самом деле, после RotateLeft(x), узел x.Left (назовем его Y) станет родителем X.
        // Значит, для второго поворота нам нужно крутить Y.
        // Выполняем первый левый поворот
        RotateLeft(x);
        // После RotateLeft(x):
        // - Старый x.Left (назовем Y) теперь стоит на месте x.
        // - Старый x теперь является Right-ребенком Y.
        // Нам нужно сделать левый поворот вокруг Y.
        // Где взять Y? Y теперь родитель x (так как x опустился вправо).
        TNode? y = x.Parent;
        // Если x был корнем, то Parent не обновился (остался null), но Root изменился.
        // В этом случае Y - это новый Root.
        if (y == null)
        {
            y = Root;
        }
        // 2. Проверяем возможность второго поворота
        if (y != null && y.Left != null)
        {
            RotateLeft(y);
        }
    }
    
    protected void RotateDoubleRight(TNode y)
    {
        // 1. Проверяем возможность первого поворота
        if (y.Right == null) return;
        // Выполняем первый правый поворот
        RotateRight(y);
        // После RotateRight(y):
        // - Старый y.Right (назовем X) теперь стоит на месте y.
        // - Старый y теперь является Left-ребенком X.
        // Нам нужно сделать правый поворот вокруг X.
        // X теперь родитель y.
        TNode? x = y.Parent;
        // Если y был корнем, Parent null, значит берем Root
        if (x == null)
        {
            x = Root;
        }
        // 2. Проверяем возможность второго поворота
        if (x != null && x.Right != null)
        {
            RotateRight(x);
        }
    }
    
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
        {
            Root = v;
        }
        else if (u.IsLeftChild)
        {
            u.Parent.Left = v;
        }
        else
        {
            u.Parent.Right = v;
        }
        v?.Parent = u.Parent;
    }
    #endregion
    
    #region Iterators

    // Публичные методы возвращают обертку IEnumerable
    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => new TreeTraversal(Root, TraversalStrategy.InOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() => new TreeTraversal(Root, TraversalStrategy.PreOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() => new TreeTraversal(Root, TraversalStrategy.PostOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() => new TreeTraversal(Root, TraversalStrategy.InOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() => new TreeTraversal(Root, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => new TreeTraversal(Root, TraversalStrategy.PostOrderReverse);

    /// <summary>
    /// Обертка для создания итератора. Нужна, так как IEnumerator нельзя создать напрямую в методе return без yield.
    /// </summary>
    private sealed class TreeTraversal : IEnumerable<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly TraversalStrategy _strategy;

        public TreeTraversal(TNode? root, TraversalStrategy strategy)
        {
            _root = root;
            _strategy = strategy;
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => new TreeIterator(_root, _strategy);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Ручной итератор. Использует только указатели (Parent) для навигации.
    /// Никакого стека и рекурсии.
    /// </summary>
    private sealed class TreeIterator : IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly TNode? _root;
        private readonly int _targetStage; // 0=Pre, 1=In, 2=Post
        private readonly bool _reverse;    // false=L->R, true=R->L
        
        private TNode? _currentNode;       // Где мы сейчас находимся
        private TNode? _previousNode;      // Откуда мы пришли (родитель или ребенок)
        private TreeEntry<TKey, TValue> _currentEntry;
        
        // Кэш глубины, чтобы не пересчитывать её каждый раз (O(N) вместо O(N*H))
        private readonly Dictionary<TNode, int> _depthCache = new();

        public TreeIterator(TNode? root, TraversalStrategy strategy)
        {
            _root = root;
            _currentNode = root;
            _previousNode = null;
            _currentEntry = default;
            
            // Настройка стратегии
            ConvertStrategy(strategy, out _targetStage, out _reverse);
        }

        public TreeEntry<TKey, TValue> Current => _currentEntry;
        object IEnumerator.Current => _currentEntry!;

        public bool MoveNext()
        {
            while (_currentNode != null)
            {
                TNode node = _currentNode;
                
                // Определяем детей в зависимости от направления
                TNode? firstChild = _reverse ? node.Right : node.Left;
                TNode? secondChild = _reverse ? node.Left : node.Right;

                // Сценарий 1: Мы пришли СВЕРХУ (от родителя)
                if (_previousNode == node.Parent)
                {
                    // PRE-ORDER: Обрабатываем узел сразу при первом заходе
                    if (_targetStage == 0)
                    {
                        _currentEntry = MakeEntry(node);
                        _previousNode = node;
                        // Идем к первому ребенку, если есть, иначе вверх
                        _currentNode = firstChild ?? secondChild ?? node.Parent;
                        return true;
                    }

                    // Если есть первый ребенок, идем вниз
                    if (firstChild != null)
                    {
                        _previousNode = node;
                        _currentNode = firstChild;
                        continue;
                    }

                    // IN-ORDER: Обрабатываем, если левого ребенка нет (или мы его прошли)
                    if (_targetStage == 1)
                    {
                        _currentEntry = MakeEntry(node);
                        _previousNode = node;
                        _currentNode = secondChild ?? node.Parent;
                        return true;
                    }

                    // POST-ORDER: Если детей нет вообще, обрабатываем лист
                    if (secondChild == null)
                    {
                        _currentEntry = MakeEntry(node);
                        _previousNode = node;
                        _currentNode = node.Parent;
                        return true;
                    }

                    // Иначе идем ко второму ребенку
                    _previousNode = node;
                    _currentNode = secondChild;
                    continue;
                }

                // Сценарий 2: Мы вернулись ОТ ПЕРВОГО РЕБЕНКА
                if (_previousNode == firstChild)
                {
                    // IN-ORDER: Время обрабатывать текущий узел
                    if (_targetStage == 1)
                    {
                        _currentEntry = MakeEntry(node);
                        _previousNode = node;
                        _currentNode = secondChild ?? node.Parent;
                        return true;
                    }

                    // Если есть второй ребенок, идем туда
                    if (secondChild != null)
                    {
                        _previousNode = node;
                        _currentNode = secondChild;
                        continue;
                    }

                    // POST-ORDER: Если второго нет, обрабатываем и идем вверх
                    if (_targetStage == 2)
                    {
                        _currentEntry = MakeEntry(node);
                        _previousNode = node;
                        _currentNode = node.Parent;
                        return true;
                    }

                    // Иначе просто вверх
                    _previousNode = node;
                    _currentNode = node.Parent;
                    continue;
                }

                // Сценарий 3: Мы вернулись ОТ ВТОРОГО РЕБЕНКА
                // Здесь остается только POST-ORDER обработка и подъем
                if (_targetStage == 2)
                {
                    _currentEntry = MakeEntry(node);
                    _previousNode = node;
                    _currentNode = node.Parent;
                    return true;
                }

                // Если ничего не подошло, просто поднимаемся выше
                _previousNode = node;
                _currentNode = node.Parent;
            }

            return false; // Дерево пройдено
        }

        public void Reset()
        {
            _currentNode = _root;
            _previousNode = null;
            _currentEntry = default;
            _depthCache.Clear();
        }

        public void Dispose() { }

        private TreeEntry<TKey, TValue> MakeEntry(TNode node)
        {
            return new TreeEntry<TKey, TValue>(node.Key, node.Value, GetDepthCached(node));
        }

        // Итеративный расчет глубины с кэшированием
        private int GetDepthCached(TNode node)
        {
            if (_depthCache.TryGetValue(node, out int depth))
                return depth;

            // Если не в кэше, считаем через Parent (итеративно)
            int d = 0;
            TNode? curr = node;
            
            // Оптимизация: если родитель уже посчитан, берем его глубину + 1
            while (curr != null && !_depthCache.ContainsKey(curr))
            {
                d++;
                curr = curr.Parent;
            }

            int baseDepth = curr != null ? _depthCache[curr] : 0;
            int totalDepth = baseDepth + d;

            // Заполняем кэш для пути, который мы прошли
            curr = node;
            int currentD = totalDepth;
            while (curr != null && !_depthCache.ContainsKey(curr))
            {
                _depthCache[curr] = currentD;
                currentD--;
                curr = curr.Parent;
            }

            return totalDepth;
        }

        private static void ConvertStrategy(TraversalStrategy s, out int stage, out bool rev)
        {
            switch (s)
            {
                case TraversalStrategy.PreOrder: stage = 0; rev = false; break;
                case TraversalStrategy.InOrder: stage = 1; rev = false; break;
                case TraversalStrategy.PostOrder: stage = 2; rev = false; break;
                case TraversalStrategy.PreOrderReverse: stage = 0; rev = true; break;
                case TraversalStrategy.InOrderReverse: stage = 1; rev = true; break;
                case TraversalStrategy.PostOrderReverse: stage = 2; rev = true; break;
                default: stage = 1; rev = false; break;
            }
        }
    }

    private enum TraversalStrategy { InOrder, PreOrder, PostOrder, InOrderReverse, PreOrderReverse, PostOrderReverse }

    #endregion

    #region Interface Implementations

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return InOrder().Select(e => new KeyValuePair<TKey, TValue>(e.Key, e.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);
    
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex >= array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        int i = arrayIndex;
        foreach (var kvp in this)
        {
            if (i >= array.Length) throw new ArgumentException("Array is not large enough");
            array[i++] = kvp;
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    #endregion
}