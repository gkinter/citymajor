#if NETSTANDARD2_1
namespace System.Collections.Generic;

/// <summary>Minimal min-heap priority queue polyfill for Unity netstandard2.1.</summary>
public sealed class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
{
    private readonly List<(TElement Element, TPriority Priority)> _heap = new();

    public int Count => _heap.Count;

    public void Enqueue(TElement element, TPriority priority)
    {
        _heap.Add((element, priority));
        SiftUp(_heap.Count - 1);
    }

    public TElement Dequeue()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("The priority queue is empty.");

        var root = _heap[0].Element;
        var last = _heap[_heap.Count - 1];
        _heap.RemoveAt(_heap.Count - 1);
        if (_heap.Count > 0)
        {
            _heap[0] = last;
            SiftDown(0);
        }

        return root;
    }

    public TElement Peek()
    {
        if (_heap.Count == 0)
            throw new InvalidOperationException("The priority queue is empty.");
        return _heap[0].Element;
    }

    public void Clear() => _heap.Clear();

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_heap[index].Priority.CompareTo(_heap[parent].Priority) >= 0)
                break;

            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= _heap.Count)
                break;

            int smallest = left;
            int right = left + 1;
            if (right < _heap.Count &&
                _heap[right].Priority.CompareTo(_heap[left].Priority) < 0)
            {
                smallest = right;
            }

            if (_heap[index].Priority.CompareTo(_heap[smallest].Priority) <= 0)
                break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        var tmp = _heap[a];
        _heap[a] = _heap[b];
        _heap[b] = tmp;
    }
}
#endif
