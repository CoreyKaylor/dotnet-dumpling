using Graphs;

namespace Dumpling.Core;

public class HeapComparer
{
    public ComparisonResult Compare(HeapSnapshot baseline, HeapSnapshot current)
    {
        var result = new ComparisonResult();
        
        // Get heap statistics for both snapshots
        var baselineAnalyzer = new HeapAnalyzer(baseline);
        var currentAnalyzer = new HeapAnalyzer(current);
        
        result.BaselineStats = baselineAnalyzer.GetHeapStatistics();
        result.CurrentStats = currentAnalyzer.GetHeapStatistics();
        
        // Calculate overall deltas
        result.ObjectCountDelta = result.CurrentStats.TotalObjects - result.BaselineStats.TotalObjects;
        result.TotalSizeDelta = (long)result.CurrentStats.TotalSize - (long)result.BaselineStats.TotalSize;
        result.RetainedSizeDelta = (long)result.CurrentStats.TotalRetainedSize - (long)result.BaselineStats.TotalRetainedSize;
        
        // Get type statistics for both snapshots
        var baselineTypes = baselineAnalyzer.GetTypeStatistics();
        var currentTypes = currentAnalyzer.GetTypeStatistics();
        
        // Create lookup dictionaries - group by TypeName to handle potential duplicates
        var baselineTypesDict = baselineTypes
            .GroupBy(t => t.TypeName)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate((a, b) => new TypeStatistics
                {
                    TypeName = a.TypeName,
                    Count = a.Count + b.Count,
                    TotalSize = a.TotalSize + b.TotalSize,
                    RetainedSize = a.RetainedSize + b.RetainedSize,
                    Instances = a.Instances.Concat(b.Instances).ToList()
                })
            );
        
        var currentTypesDict = currentTypes
            .GroupBy(t => t.TypeName)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate((a, b) => new TypeStatistics
                {
                    TypeName = a.TypeName,
                    Count = a.Count + b.Count,
                    TotalSize = a.TotalSize + b.TotalSize,
                    RetainedSize = a.RetainedSize + b.RetainedSize,
                    Instances = a.Instances.Concat(b.Instances).ToList()
                })
            );
        
        // Find all unique type names
        var allTypeNames = new HashSet<string>();
        allTypeNames.UnionWith(baselineTypesDict.Keys);
        allTypeNames.UnionWith(currentTypesDict.Keys);
        
        // Calculate deltas for each type
        foreach (var typeName in allTypeNames)
        {
            var delta = new TypeDelta { TypeName = typeName };
            
            if (baselineTypesDict.TryGetValue(typeName, out var baselineType))
            {
                delta.BaselineCount = baselineType.Count;
                delta.BaselineTotalSize = baselineType.TotalSize;
                delta.BaselineRetainedSize = baselineType.RetainedSize;
            }
            
            if (currentTypesDict.TryGetValue(typeName, out var currentType))
            {
                delta.CurrentCount = currentType.Count;
                delta.CurrentTotalSize = currentType.TotalSize;
                delta.CurrentRetainedSize = currentType.RetainedSize;
            }
            
            // Calculate deltas
            delta.CountDelta = delta.CurrentCount - delta.BaselineCount;
            delta.TotalSizeDelta = (long)delta.CurrentTotalSize - (long)delta.BaselineTotalSize;
            delta.RetainedSizeDelta = (long)delta.CurrentRetainedSize - (long)delta.BaselineRetainedSize;
            
            // Determine status
            if (delta.BaselineCount == 0 && delta.CurrentCount > 0)
            {
                delta.Status = TypeChangeStatus.Added;
                result.NewTypes.Add(typeName);
            }
            else if (delta.BaselineCount > 0 && delta.CurrentCount == 0)
            {
                delta.Status = TypeChangeStatus.Removed;
                result.RemovedTypes.Add(typeName);
            }
            else if (delta.CountDelta != 0 || delta.RetainedSizeDelta != 0)
            {
                delta.Status = TypeChangeStatus.Changed;
            }
            else
            {
                delta.Status = TypeChangeStatus.Unchanged;
            }
            
            result.TypeDeltas.Add(delta);
        }
        
        return result;
    }
    
    public List<ComparisonResult> CompareMultiple(List<HeapSnapshot> snapshots)
    {
        var results = new List<ComparisonResult>();
        
        for (int i = 0; i < snapshots.Count - 1; i++)
        {
            var comparison = Compare(snapshots[i], snapshots[i + 1]);
            results.Add(comparison);
        }
        
        return results;
    }
    
    public ComparisonResult CompareInstances(
        HeapSnapshot baseline, 
        HeapSnapshot current, 
        string typeName,
        int maxInstances = 10)
    {
        var result = Compare(baseline, current);
        
        // Get detailed instance information for the specific type
        var baselineAnalyzer = new HeapAnalyzer(baseline);
        var currentAnalyzer = new HeapAnalyzer(current);
        
        var baselineTypes = baselineAnalyzer.GetTypeStatistics();
        var currentTypes = currentAnalyzer.GetTypeStatistics();
        
        var baselineType = baselineTypes.FirstOrDefault(t => t.TypeName == typeName);
        var currentType = currentTypes.FirstOrDefault(t => t.TypeName == typeName);
        
        if (baselineType != null)
        {
            result.BaselineInstances = GetInstanceDetails(baseline, baselineType.Instances.Take(maxInstances));
        }
        
        if (currentType != null)
        {
            result.CurrentInstances = GetInstanceDetails(current, currentType.Instances.Take(maxInstances));
        }
        
        return result;
    }
    
    private List<InstanceDetail> GetInstanceDetails(HeapSnapshot snapshot, IEnumerable<NodeIndex> instances)
    {
        var details = new List<InstanceDetail>();
        var nodeStorage = snapshot.MemoryGraph.AllocNodeStorage();
        var typeStorage = snapshot.MemoryGraph.AllocTypeNodeStorage();
        
        foreach (var nodeIndex in instances)
        {
            var node = snapshot.MemoryGraph.GetNode(nodeIndex, nodeStorage);
            var type = snapshot.MemoryGraph.GetType(node.TypeIndex, typeStorage);
            var address = snapshot.MemoryGraph.GetAddress(nodeIndex);
            var retainedSize = snapshot.GetRetainedSize(nodeIndex);
            
            details.Add(new InstanceDetail
            {
                Address = address,
                TypeName = type.Name,
                Size = (ulong)node.Size,
                RetainedSize = retainedSize,
                NodeIndex = nodeIndex
            });
        }
        
        return details;
    }
}

public class ComparisonResult
{
    public HeapStatistics BaselineStats { get; set; } = new();
    public HeapStatistics CurrentStats { get; set; } = new();
    
    public int ObjectCountDelta { get; set; }
    public long TotalSizeDelta { get; set; }
    public long RetainedSizeDelta { get; set; }
    
    public List<TypeDelta> TypeDeltas { get; set; } = new();
    public List<string> NewTypes { get; set; } = new();
    public List<string> RemovedTypes { get; set; } = new();
    
    public List<InstanceDetail> BaselineInstances { get; set; } = new();
    public List<InstanceDetail> CurrentInstances { get; set; } = new();
}

public class TypeDelta
{
    public string TypeName { get; set; } = string.Empty;
    
    public int BaselineCount { get; set; }
    public ulong BaselineTotalSize { get; set; }
    public ulong BaselineRetainedSize { get; set; }
    
    public int CurrentCount { get; set; }
    public ulong CurrentTotalSize { get; set; }
    public ulong CurrentRetainedSize { get; set; }
    
    public int CountDelta { get; set; }
    public long TotalSizeDelta { get; set; }
    public long RetainedSizeDelta { get; set; }
    
    public TypeChangeStatus Status { get; set; }
}

public enum TypeChangeStatus
{
    Unchanged,
    Changed,
    Added,
    Removed
}

public class InstanceDetail
{
    public ulong Address { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public ulong Size { get; set; }
    public ulong RetainedSize { get; set; }
    public NodeIndex NodeIndex { get; set; }
}