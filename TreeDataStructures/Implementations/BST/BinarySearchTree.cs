using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.BST;

public class BinarySearchTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, BstNode<TKey, TValue>>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new BstNode<TKey, TValue>(key, value);
    }
    
    // Для обычного BST балансировка не требуется - методы пустые
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        // BST не требует балансировки после вставки
    }
    
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        // BST не требует балансировки после удаления
    }
    
}