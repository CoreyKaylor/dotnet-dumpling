using Graphs;

namespace Dumpling.Core;

public class TypeStatistics
{
    public string TypeName { get; set; } = string.Empty;
    public int Count { get; set; }
    public ulong TotalSize { get; set; }
    public ulong RetainedSize { get; set; }
    public List<NodeIndex> Instances { get; set; } = new();
}

public class HeapAnalyzer(HeapSnapshot snapshot)
{
    private TypeNameFormatter? formatter;

    public TypeNameFormatter? TypeNameFormatter => formatter;

    public List<TypeStatistics> GetTypeStatistics(int topCount = int.MaxValue)
    {
        var typeStats = new Dictionary<NodeTypeIndex, TypeStatistics>();
        var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
        var typeStorage = snapshot.MemoryGraph.AllocTypeNodeStorage();

        for (NodeIndex nodeIndex = 0; nodeIndex < snapshot.MemoryGraph.NodeIndexLimit; nodeIndex++)
        {
            var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
            
            if (node.Size > 0)
            {
                if (!typeStats.TryGetValue(node.TypeIndex, out var stats))
                {
                    var nodeType = snapshot.MemoryGraph.GetType(node.TypeIndex, typeStorage);
                    stats = new TypeStatistics
                    {
                        TypeName = nodeType.Name
                    };
                    typeStats[node.TypeIndex] = stats;
                }

                stats.Count++;
                stats.TotalSize += (ulong)node.Size;
                stats.RetainedSize += snapshot.GetRetainedSize(nodeIndex);
                stats.Instances.Add(nodeIndex);
            }
        }

        var result = typeStats.Values
            .OrderByDescending(t => t.RetainedSize)
            .Take(topCount)
            .ToList();
            
        // Create formatter after collecting all types
        formatter ??= new TypeNameFormatter(typeStats.Values.Select(t => t.TypeName));
        
        return result;
    }

    public List<string> FindReferencePaths(NodeIndex targetNode, int maxPaths = 5)
    {
        var paths = new List<string>();
        var visited = new HashSet<NodeIndex>();
        var currentPath = new Stack<string>();
        var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
        var typeStorage = snapshot.MemoryGraph.AllocTypeNodeStorage();

        void FindPathsRecursive(NodeIndex current, int depth)
        {
            if (paths.Count >= maxPaths || depth > 50) // Limit depth to prevent stack overflow
                return;

            if (current == snapshot.MemoryGraph.RootIndex)
            {
                var pathStr = string.Join(" <- ", currentPath);
                var fullPath = $"[ROOT] <- {pathStr}";
                
                // Format the path if formatter is available
                if (formatter != null)
                {
                    paths.Add(formatter.FormatReferencePath(fullPath, includeAddresses: false));
                }
                else
                {
                    paths.Add(fullPath);
                }
                return;
            }

            if (!visited.Add(current))
                return;

            // Get retainers (objects that reference this object) using RefGraph
            var refNode = snapshot.RefGraph.GetNode(current);
            for (var retainerIndex = refNode.GetFirstChildIndex();
                 retainerIndex != NodeIndex.Invalid;
                 retainerIndex = refNode.GetNextChildIndex())
            {
                var retainerNode = snapshot.MemoryGraph.GetNode(retainerIndex, nodeStorage);
                var retainerType = snapshot.MemoryGraph.GetType(retainerNode.TypeIndex, typeStorage);
                var address = snapshot.MemoryGraph.GetAddress(retainerIndex);
                
                currentPath.Push($"{retainerType.Name} (0x{address:X})");
                FindPathsRecursive(retainerIndex, depth + 1);
                currentPath.Pop();
            }

            visited.Remove(current);
        }

        var targetNodeObj = snapshot.MemoryGraph.GetNode(targetNode, nodeStorage);
        var targetType = snapshot.MemoryGraph.GetType(targetNodeObj.TypeIndex, typeStorage);
        var targetAddress = snapshot.MemoryGraph.GetAddress(targetNode);
        currentPath.Push($"{targetType.Name} (0x{targetAddress:X})");
        
        FindPathsRecursive(targetNode, 0);

        if (paths.Count == 0)
        {
            paths.Add($"{targetType.Name} (0x{targetAddress:X}) - No path to root found");
        }

        return paths;
    }

    public HeapStatistics GetHeapStatistics()
    {
        var stats = new HeapStatistics();
        var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();

        for (NodeIndex nodeIndex = 0; nodeIndex < snapshot.MemoryGraph.NodeIndexLimit; nodeIndex++)
        {
            var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
            if (node.Size > 0)
            {
                stats.TotalObjects++;
                stats.TotalSize += (ulong)node.Size;
            }
        }

        stats.TotalRetainedSize = snapshot.GetRetainedSize(snapshot.MemoryGraph.RootIndex);
        stats.Counters = snapshot.Counters;

        return stats;
    }
}

public class HeapStatistics
{
    public int TotalObjects { get; set; }
    public ulong TotalSize { get; set; }
    public ulong TotalRetainedSize { get; set; }
    public Dictionary<string, double>? Counters { get; set; }
}