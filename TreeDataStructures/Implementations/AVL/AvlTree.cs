using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        Rebalance(newNode);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        Rebalance(parent ?? child);
    }

    private void Rebalance(AvlNode<TKey, TValue>? node)
    {
        while (node != null)
        {
            UpdateHeight(node);

            int balance = GetBalanceFactor(node);

            // Левое перевешивание (Balance > 1)
            if (balance > 1)
            {
                // LR случай: левый ребенок имеет отрицательный баланс (тяжелее справа)
                // Нужен "Большой левый" поворот (сначала Right на ребенке, потом Left на узле)
                if (GetBalanceFactor(node.Left) < 0)
                {
                    RotateBigLeft(node);
                }
                else
                {
                    // LL случай: обычный правый поворот
                    RotateRight(node);
                }

                FixHeights(node);
            }
            // Правое перевешивание (Balance < -1)
            else if (balance < -1)
            {
                // RL случай: правый ребенок имеет положительный баланс (тяжелее слева)
                // Нужен "Большой правый" поворот (сначала Left на ребенке, потом Right на узле)
                if (GetBalanceFactor(node.Right) > 0)
                {
                    RotateBigRight(node);
                }
                else
                {
                    // RR случай: обычный левый поворот
                    RotateLeft(node);
                }

                FixHeights(node);
            }

            node = node.Parent;
        }
    }

    private static int GetHeight(AvlNode<TKey, TValue>? node) 
        => node?.Height ?? 0;

    private static int GetBalanceFactor(AvlNode<TKey, TValue>? node) 
        => node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);

    private static void UpdateHeight(AvlNode<TKey, TValue>? node)
    {
        if (node != null)
        {
            node.Height = 1 + Math.Max(GetHeight(node.Left), GetHeight(node.Right));
        }
    }

    /// <summary>
    /// Обновляет высоты узлов после поворота.
    /// </summary>
    private static void FixHeights(AvlNode<TKey, TValue>? node)
    {
        UpdateHeight(node);

        if (node?.Parent != null)
        {
            UpdateHeight(node.Parent);
            if (node.Parent.Parent != null)
            {
                UpdateHeight(node.Parent.Parent);
            }
        }
    }
}