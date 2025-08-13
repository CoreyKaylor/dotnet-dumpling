using System.Text.RegularExpressions;

namespace Dumpling.Core;

public class TypeNameFormatter
{
    private readonly Dictionary<string, bool> typeNameAmbiguity = new();
    private readonly Dictionary<string, string> typeNameCache = new();
    private readonly HashSet<string> specialNodes = new()
    {
        "[.NET Roots]", "[static vars]", "[pinned handles]", "[finalizer handles]",
        "[strong handles]", "[weak handles]", "[other roots]"
    };

    public TypeNameFormatter(IEnumerable<string> allTypeNames)
    {
        BuildAmbiguityIndex(allTypeNames);
    }

    private void BuildAmbiguityIndex(IEnumerable<string> allTypeNames)
    {
        var simpleNameGroups = new Dictionary<string, List<string>>();
        
        foreach (var typeName in allTypeNames)
        {
            // Skip special nodes
            if (IsSpecialNode(typeName))
                continue;
                
            var simpleName = GetSimpleName(typeName);
            if (!simpleNameGroups.ContainsKey(simpleName))
                simpleNameGroups[simpleName] = new List<string>();
            simpleNameGroups[simpleName].Add(typeName);
        }
        
        // Mark which types need disambiguation
        foreach (var group in simpleNameGroups.Values)
        {
            bool needsDisambiguation = group.Count > 1;
            foreach (var fullName in group)
            {
                typeNameAmbiguity[fullName] = needsDisambiguation;
            }
        }
    }

    public string FormatTypeName(string typeName)
    {
        // Cache formatted names for performance
        if (typeNameCache.TryGetValue(typeName, out var cached))
            return cached;
            
        var formatted = FormatTypeNameInternal(typeName);
        typeNameCache[typeName] = formatted;
        return formatted;
    }

    private string FormatTypeNameInternal(string typeName)
    {
        // Handle special GC root nodes
        if (typeName.StartsWith("[static var "))
            return ExtractStaticVarName(typeName);
        if (typeName == "[static vars]")
            return "[Static Fields]";
        if (typeName == "[.NET Roots]")
            return "[GC Root]";
        if (typeName == "[pinned handles]")
            return "[Pinned]";
        if (typeName == "[finalizer handles]")
            return "[Finalizer Queue]";
        if (typeName == "[strong handles]")
            return "[Strong Handles]";
        if (typeName == "[weak handles]")
            return "[Weak References]";
        if (typeName == "[other roots]")
            return "[Other Roots]";
        
        // Check if this type name needs disambiguation
        if (typeNameAmbiguity.TryGetValue(typeName, out bool needsDisambiguation))
        {
            if (!needsDisambiguation)
            {
                // Unambiguous - just use simple name
                return GetSimpleName(typeName);
            }
            else
            {
                // Ambiguous - use minimal distinguishing context
                return GetMinimalDistinguishingName(typeName);
            }
        }
        
        // For types not in our index, use minimal distinguishing name
        return GetMinimalDistinguishingName(typeName);
    }

    private string GetSimpleName(string typeName)
    {
        // Handle generic types
        var genericIndex = typeName.IndexOf('<');
        string baseName;
        string genericPart = "";
        
        if (genericIndex > 0)
        {
            baseName = typeName.Substring(0, genericIndex);
            genericPart = typeName.Substring(genericIndex);
            
            // Recursively simplify generic parameters
            genericPart = SimplifyGenericParameters(genericPart);
        }
        else
        {
            baseName = typeName;
        }
        
        // Extract just the type name without namespace
        var lastDot = baseName.LastIndexOf('.');
        if (lastDot >= 0)
            baseName = baseName.Substring(lastDot + 1);
        
        return baseName + genericPart;
    }

    private string GetMinimalDistinguishingName(string typeName)
    {
        // Handle generic types
        var genericIndex = typeName.IndexOf('<');
        string baseName;
        string genericPart = "";
        
        if (genericIndex > 0)
        {
            baseName = typeName.Substring(0, genericIndex);
            genericPart = typeName.Substring(genericIndex);
            genericPart = SimplifyGenericParameters(genericPart);
        }
        else
        {
            baseName = typeName;
        }
        
        // Use last two namespace parts for context
        var parts = baseName.Split('.');
        if (parts.Length > 2)
        {
            baseName = string.Join(".", parts.Skip(parts.Length - 2));
        }
        else if (parts.Length == 2 && parts[0] == "System")
        {
            // For System types, often just the type name is enough
            baseName = parts[1];
        }
        
        return baseName + genericPart;
    }

    private string SimplifyGenericParameters(string genericPart)
    {
        // Recursively format type names within generic parameters
        return Regex.Replace(genericPart, @"[\w\.]+(?:\[[^\]]*\])?(?=,|>)", match =>
        {
            var paramType = match.Value.Trim();
            // Check if this parameter type needs disambiguation
            if (typeNameAmbiguity.TryGetValue(paramType, out bool needsDisambiguation))
            {
                return needsDisambiguation ? GetMinimalDistinguishingName(paramType) : GetSimpleName(paramType);
            }
            // Default to simple name for generic parameters
            var lastDot = paramType.LastIndexOf('.');
            return lastDot >= 0 ? paramType.Substring(lastDot + 1) : paramType;
        });
    }

    private string ExtractStaticVarName(string staticVarText)
    {
        // "[static var ObjCRuntime.Runtime.Registrar]" → "Runtime.Registrar (static)"
        var match = Regex.Match(staticVarText, @"\[static var ([^\]]+)\]");
        if (match.Success)
        {
            var fullName = match.Groups[1].Value;
            var parts = fullName.Split('.');
            if (parts.Length >= 2)
                return $"{parts[^2]}.{parts[^1]} (static)";
            return $"{fullName} (static)";
        }
        return staticVarText;
    }

    public string FormatReferencePath(string path, bool includeAddresses = false)
    {
        // Remove [ROOT] <- prefix if present
        if (path.StartsWith("[ROOT] <- "))
            path = path.Substring(10);
        
        // Split the path and process each segment
        var segments = path.Split(" <- ");
        var formatted = new List<string>();
        
        foreach (var segment in segments.Take(5)) // Limit depth for readability
        {
            // Extract type name and address
            var match = Regex.Match(segment, @"^([^\(]+)(?:\s*\(0x([0-9A-Fa-f]+)\))?");
            if (match.Success)
            {
                var typeName = match.Groups[1].Value.Trim();
                var address = match.Groups[2].Value;
                
                // Format the type name
                var formattedType = FormatTypeName(typeName);
                
                // Skip addresses for special nodes (they're always 0x0)
                if (includeAddresses && !IsSpecialNode(typeName) && !string.IsNullOrEmpty(address) && address != "0")
                {
                    formatted.Add($"{formattedType} (@{address})");
                }
                else
                {
                    formatted.Add(formattedType);
                }
            }
        }
        
        if (segments.Length > 5)
            formatted.Add("...");
        
        return string.Join(" ← ", formatted);
    }

    private bool IsSpecialNode(string typeName)
    {
        return typeName.StartsWith("[") && 
               (specialNodes.Contains(typeName) || typeName.StartsWith("[static var "));
    }
}