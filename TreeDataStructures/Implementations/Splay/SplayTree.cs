﻿using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);
    
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        Splay(newNode);
    }
    
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        var splaying = parent ?? child;
        if (splaying != null)
        {
            Splay(splaying);
        }
    }

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            Splay(node);
            return true;
        }

        value = default;
        return false;
    }

    /*
    При стандартном выполнении задания
    последний тест не проходит, ибо вызывается
    неожиданно containsKey(), который сразу и не
    понятно, что нужно переопределять.
    Переопределю. [FD]
    */
    public override bool ContainsKey(TKey key)
    {
        var node = FindNode(key);
        if (node != null)
        {
            Splay(node);
            return true;
        }

        return false;
    }


    private void Splay(BstNode<TKey, TValue> x)
    {
        while (x.Parent != null)
        {
            var parent = x.Parent;
            var grandparent = parent.Parent;

            if (grandparent == null)
            {
                if (x.IsLeftChild)
                {
                    RotateRight(parent);
                }
                else
                {
                    RotateLeft(parent);
                }

                continue;
            }

            if (x.IsLeftChild && parent.IsLeftChild)
            {
                RotateBigRight(grandparent);
            }
            else if (!x.IsLeftChild && !parent.IsLeftChild)
            {
                RotateBigLeft(grandparent);
            }
            else if (!x.IsLeftChild && parent.IsLeftChild)
            {
                RotateDoubleLeft(grandparent);
            }
            else
            {
                RotateDoubleRight(grandparent);
            }
        }
    }
}

// Запустить тесты только для этого типа v v v
// dotnet test TreeDataStructures.Tests/ --filter TestCategory=Splay -r linux-x64 -v normal   