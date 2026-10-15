using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Conservative metadata inventory. References are not proof that an entry point executes a method.</summary>
public static class SurfaceSurvey
{
    public static string[] BinaryReferences(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
            string ns = metadata.GetString(type.Namespace);
            if (ns.StartsWith("System.IO", StringComparison.Ordinal) || ns.StartsWith("System.Net", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft.Win32", StringComparison.Ordinal) || ns == "System.Diagnostics" || ns == "System")
                names.Add(ns + "." + metadata.GetString(type.Name) + "." + metadata.GetString(member.Name));
        }
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            if (!method.Attributes.HasFlag(System.Reflection.MethodAttributes.PinvokeImpl)) continue;
            var import = method.GetImport();
            names.Add("native:" + metadata.GetString(metadata.GetModuleReference(import.Module).Name) + ":" + metadata.GetString(import.Name));
        }
        return names.ToArray();
    }
}
