using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null)
        {
            return (null, null);
        }

        if (Comparer.Compare(root.Key, key) <= 0)
        {
            var (middleLeft, right) = Split(root.Right, key);

            root.Right = middleLeft;
            if (middleLeft != null)
                middleLeft.Parent = root;

            root.Parent = null;
            return (root, right);
        }
        else
        {
            var (left, middleRight) = Split(root.Left, key);

            root.Left = middleRight;
            if (middleRight != null)
                middleRight.Parent = root;

            root.Parent = null;
            return (left, root);
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// Приоритет отдаем левой куче: если приоритеты равны, левое дерево становится корнем.
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null)
        {
            if (right != null)
                right.Parent = null;
            return right;
        }

        if (right == null)
        {
            left.Parent = null;
            return left;
        }

        // Приоритет левой куче: используем >= чтобы при равных приоритетах левое wins
        if (left.Priority >= right.Priority)
        {
            left.Right = Merge(left.Right, right);
            if (left.Right != null)
                left.Right.Parent = left;

            left.Parent = null;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            if (right.Left != null)
                right.Left.Parent = right;

            right.Parent = null;
            return right;
        }
    }

    public override void Add(TKey key, TValue value)
    {
        var existing = FindNode(key);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        var newNode = CreateNode(key, value);

        var (left, right) = Split(Root, key);

        Root = Merge(Merge(left, newNode), right);
        if (Root != null)
            Root.Parent = null;

        Count++;
    }

    public override bool Remove(TKey key)
    {
        var node = FindNode(key);
        if (node == null)
        {
            return false;
        }

        var merged = Merge(node.Left, node.Right);

        if (node.Parent == null)
        {
            Root = merged;
            if (Root != null)
                Root.Parent = null;
        }
        else if (node.IsLeftChild)
        {
            node.Parent.Left = merged;
            if (merged != null)
                merged.Parent = node.Parent;
        }
        else
        {
            node.Parent.Right = merged;
            if (merged != null)
                merged.Parent = node.Parent;
        }

        Count--;
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new TreapNode<TKey, TValue>(key, value);
    }

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        // Не требуется для Treap - балансировка через Split/Merge
    }

    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
        // Не требуется для Treap - балансировка через Split/Merge
    }
}