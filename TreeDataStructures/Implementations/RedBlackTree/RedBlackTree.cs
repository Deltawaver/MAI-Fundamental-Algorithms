using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new RbNode<TKey, TValue>(key, value);
    }

    private bool IsRed(RbNode<TKey, TValue>? node)
    {
        return node != null && node.Color == RbColor.Red;
    }
    
    private RbNode<TKey, TValue> FindMinimum(RbNode<TKey, TValue> node)
    {
        while (node.Left != null)
        {
            node = node.Left;
        }
        return node;
    }
    
    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        while (newNode.Parent != null && IsRed(newNode.Parent))
        {
            var parent = newNode.Parent;
            var grand = parent.Parent;

            // Добавили к корню (точно черный) красный узел => все окей
            if (grand == null) { break; }

            if (parent == grand.Left)
            {
                var uncle = grand.Right;

                // Красный дядя
                if (IsRed(uncle))
                {
                    // Опускаем черноту
                    parent.Color = RbColor.Black;
                    uncle!.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                    
                    // Переходим к балансировке выше
                    newNode = grand;
                }

                // Черный дядя (или null)
                else
                {   
                    // Делаем большой правый или малый правый поворот (проверка на зиг-заг)
                    if (newNode == parent.Right)
                    {
                        newNode = parent;
                        RotateLeft(newNode);
                        parent = newNode.Parent;
                    }

                    parent!.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                    RotateRight(grand);
                }
            }

            // Случай, если родитель - правый сын деда
            else
            {
                var uncle = grand.Left;

                // Красный дядя (аналогично)
                if (IsRed(uncle))
                {
                    parent.Color = RbColor.Black;
                    uncle!.Color = RbColor.Black;
                    grand.Color = RbColor.Red;

                    newNode = grand;
                }

                // Черный дядя
                else
                {   
                    // Делаем большой левый или малый левый поворот (проверка на зиг-заг)
                    if (newNode == parent.Left)
                    {
                        newNode = parent;
                        RotateRight(newNode);
                        parent = newNode.Parent;
                    }

                    parent!.Color = RbColor.Black;
                    grand.Color = RbColor.Red;
                    RotateLeft(grand);
                }
            }
        }
        // Принудительно красим корень в черный, чтобы св-во 2) всегда выполнялось
        if (Root != null) { Root.Color = RbColor.Black; }
    }

    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        RbNode<TKey, TValue>? x = null;
        RbNode<TKey, TValue>? xParent = null;

        RbNode<TKey, TValue>? y = node;
        RbColor yOriginalColor = y.Color;

        // Выполняем обычное BST удаление с запоминанием цвета удаляемого узла, его сына(successor) и родителя
        if (node.Left == null)
        {
            x = node.Right;
            xParent = node.Parent;
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            x = node.Left;
            xParent = node.Parent;
            Transplant(node, node.Left);
        }
        else
        {
            y = FindMinimum(node.Right);
            // Запоминаем именно оригинальный цвет
            yOriginalColor = y.Color;
            x = y.Right;
            
            if (y.Parent == node) { xParent = y; }
            else
            {
                xParent = y.Parent;
                Transplant(y, y.Right);
                y.Right = node.Right;
                y.Right.Parent = y;
            }

            Transplant(node, y);
            y.Left = node.Left;
            y.Left.Parent = y;
            y.Color = node.Color;
        }

        // Переходим к hook, чтобы восстановить черную высоту, если был удален черный узел
        if (yOriginalColor == RbColor.Black) { OnNodeRemoved(xParent, x); }
    }

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
        var x = child;
        var xParent = parent;

        while (x != Root && !IsRed(x))
        {   
            // Просто безопасность 
            if (xParent == null) { break; }

            if (x == xParent.Left)
            {
                var brother = xParent.Right;

                // Брат красный (II) - делаем из этого случай (I)
                if (IsRed(brother))
                {
                    brother!.Color = RbColor.Black;
                    xParent.Color = RbColor.Red;
                    RotateLeft(xParent);
                    brother = xParent.Right;
                }

                // Черный брат, черный nephew, черный far nephew
                if (!IsRed(brother?.Left) && !IsRed(brother?.Right))
                {
                    if (brother != null) { brother.Color = RbColor.Red; }

                    // Идем вверх по дереву для балансировки
                    x = xParent;
                    xParent = x.Parent;
                }
                else
                {   
                    // Черный брат, красный nephew, черный far nephew
                    if (!IsRed(brother?.Right))
                    {   
                        // Обрабатываем случай зиг-зага - делаем far nephew красным, чтоб перейти к следующему пункту
                        if (brother != null)
                        {
                            if (brother?.Left != null) { brother.Left.Color = RbColor.Black; }
                            brother!.Color = RbColor.Red;
                            RotateRight(brother);
                        }
                        brother = xParent.Right;
                    }

                    // Черный брат, красный far nephew
                    if (brother != null)
                    {
                        brother.Color = xParent.Color;
                        xParent.Color = RbColor.Black;
                        if (brother.Right != null) { brother.Right.Color = RbColor.Black; }
                        RotateLeft(xParent);
                    }

                    x = Root;
                }
            }

            // Все то же самое, но зеркально
            else
            {
                var brother = xParent.Left;
                
                if (IsRed(brother))
                {
                    brother!.Color = RbColor.Black;
                    xParent.Color = RbColor.Red;
                    RotateRight(xParent);
                    brother = xParent.Left;
                }

                if (!IsRed(brother?.Left) && !IsRed(brother?.Right))
                {
                    if (brother != null) brother.Color = RbColor.Red;
                    x = xParent;
                    xParent = x.Parent;
                }
                else
                {
                    if (!IsRed(brother?.Left))
                    {
                        if (brother != null)
                        {
                            if (brother.Right != null) { brother.Right.Color = RbColor.Black; }
                            brother.Color = RbColor.Red;
                            RotateLeft(brother);
                        }
                        brother = xParent.Left;
                    }
                    
                    if (brother != null)
                    {
                        brother.Color = xParent.Color;
                        xParent.Color = RbColor.Black;
                        if (brother.Left != null) { brother.Left.Color = RbColor.Black; }
                        RotateRight(xParent);
                    }

                    x = Root;
                }
            }
        }

        if (x != null) x.Color = RbColor.Black;
    }
}

// dotnet test TreeDataStructures.Tests/ --filter TestCategory=RB