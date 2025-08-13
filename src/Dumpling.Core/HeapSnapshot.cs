using Graphs;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Dumpling.Core;

public class HeapSnapshot
{
    private readonly MemoryGraph graph;
    private readonly RefGraph refGraph;
    private readonly Dictionary<string, double>? counters;

    private int[] postOrderIndex2NodeIndex = [];
    private int[] nodeIndex2Depth = [];
    private ulong[] retainedSizes = [];

    public MemoryGraph MemoryGraph => graph;
    public RefGraph RefGraph => refGraph;
    public Dictionary<string, double>? Counters => counters;
    
    public ulong GetRetainedSize(NodeIndex nodeIndex) => retainedSizes[(int)nodeIndex];

    public HeapSnapshot(GCHeapDump heapDump)
        : this(heapDump.MemoryGraph)
    {
    }

    public HeapSnapshot(MemoryGraph memoryGraph, Dictionary<string, double>? counters = null)
    {
        graph = memoryGraph;
        refGraph = new RefGraph(memoryGraph);
        this.counters = counters;
        BuildPostOrderIndex();
        BuildDepthIndex();
        CalculateRetainedSizes();
        Debug.Assert(postOrderIndex2NodeIndex != null);
        Debug.Assert(retainedSizes != null);
        Debug.Assert(nodeIndex2Depth != null);
    }

    private void BuildPostOrderIndex()
    {
        postOrderIndex2NodeIndex = new int[(int)graph.NodeIndexLimit];
        var visited = new BitArray((int)graph.NodeIndexLimit);
        var nodeStack = new Stack<Node>();
        int postOrderIndex = 0;

        var rootNode = graph.GetNode(graph.RootIndex, graph.AllocNodeStorage());
        rootNode.ResetChildrenEnumeration();
        nodeStack.Push(rootNode);

        while (nodeStack.Count > 0)
        {
            var currentNode = nodeStack.Peek();
            NodeIndex nextChild = currentNode.GetNextChildIndex();
            if (nextChild != NodeIndex.Invalid)
            {
                if (visited.Get((int)nextChild))
                    continue;
                var childNode = graph.GetNode(nextChild, graph.AllocNodeStorage());
                childNode.ResetChildrenEnumeration();
                nodeStack.Push(childNode);
                visited.Set((int)nextChild, true);
            }
            else
            {
                postOrderIndex2NodeIndex[postOrderIndex] = (int)currentNode.Index;
                postOrderIndex++;
                nodeStack.Pop();
            }
        }
    }

    [MemberNotNull(nameof(nodeIndex2Depth))]
    private void BuildDepthIndex()
    {
        nodeIndex2Depth = new int[(int)graph.NodeIndexLimit];
        var visited = new BitArray((int)graph.NodeIndexLimit);
        var nodeStack = new Stack<NodeIndex>();
        var nodeStack2 = new Stack<NodeIndex>();
        var nodeStorage = graph.AllocNodeStorage();
        int depth = 0;

        nodeStack.Push(graph.RootIndex);
        visited.Set((int)graph.RootIndex, true);
        while (nodeStack.Count > 0)
        {
            while (nodeStack.Count > 0)
            {
                var currentNode = graph.GetNode(nodeStack.Pop(), nodeStorage);
                nodeIndex2Depth[(int)currentNode.Index] = depth;
                for (NodeIndex childIndex = currentNode.GetFirstChildIndex();
                     childIndex != NodeIndex.Invalid;
                     childIndex = currentNode.GetNextChildIndex())
                {
                    if (!visited.Get((int)childIndex))
                    {
                        nodeStack2.Push(childIndex);
                        visited.Set((int)childIndex, true);
                    }
                }
            }

            (nodeStack2, nodeStack) = (nodeStack, nodeStack2);
            depth++;
        }
    }

    private void CalculateRetainedSizes()
    {
        retainedSizes = new ulong[(int)graph.NodeIndexLimit];

        var nodeStorage = graph.AllocNodeStorage();
        for (NodeIndex nodeIndex = 0; nodeIndex < graph.NodeIndexLimit; nodeIndex++)
        {
            Node node = graph.GetNode(nodeIndex, nodeStorage);
            retainedSizes[(int)nodeIndex] = (ulong)node.Size;
        }

        var spanningTree = new SpanningTree(graph, TextWriter.Null);
        spanningTree.ForEach(null!);

        // Propagate retained sizes for each node excluding root.
        int nodeCount = (int)graph.NodeIndexLimit;
        for (int postOrderIndex = 0; postOrderIndex < nodeCount - 1; ++postOrderIndex)
        {
            int nodeIndex = postOrderIndex2NodeIndex[postOrderIndex];
            int dominatorOrdinal = (int)spanningTree.Parent((NodeIndex)nodeIndex);
            if (dominatorOrdinal >= 0)
                retainedSizes[dominatorOrdinal] += retainedSizes[nodeIndex];
        }
    }
}