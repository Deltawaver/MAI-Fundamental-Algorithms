﻿using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        Change(newNode);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        Change(parent ?? child);
    }

    private void Change(AvlNode<TKey, TValue>? node)
    {
        while (node != null)
        {
            UpdateHeight(node);

            int balance = Difference(node);

            if (balance > 1)
            {
                if (Difference(node.Left) < 0)
                {
                    RotateBigLeft(node);
                    UpdateHeight(node.Left);
                    UpdateHeight(node);
                    if (node.Parent != null)
                        UpdateHeight(node.Parent);
                }
                else
                {
                    RotateRight(node);
                    UpdateHeight(node);
                    if (node.Parent != null)
                        UpdateHeight(node.Parent);
                }
            }
            else if (balance < -1)
            {
                if (Difference(node.Right) > 0)
                {
                    RotateBigRight(node);
                    UpdateHeight(node.Right);
                    UpdateHeight(node);
                    if (node.Parent != null)
                        UpdateHeight(node.Parent);
                }
                else
                {
                    RotateLeft(node);
                    UpdateHeight(node);
                    if (node.Parent != null)
                        UpdateHeight(node.Parent);
                }
            }

            node = node.Parent;
        }
    }

    private static int Height(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private static void UpdateHeight(AvlNode<TKey, TValue>? node)
    {
        node?.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
    }

    private static int Difference(AvlNode<TKey, TValue>? node) => node == null ? 0 : Height(node.Left) - Height(node.Right);


}