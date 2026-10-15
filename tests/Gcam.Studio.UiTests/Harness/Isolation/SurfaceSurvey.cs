using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
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
            if (Name(metadata, handle) is { } name) names.Add(name);
        foreach (var handle in metadata.MethodDefinitions)
        {
            var method = metadata.GetMethodDefinition(handle);
            if (!method.Attributes.HasFlag(System.Reflection.MethodAttributes.PinvokeImpl)) continue;
            var import = method.GetImport();
            names.Add("native:" + metadata.GetString(metadata.GetModuleReference(import.Module).Name) + ":" + metadata.GetString(import.Name));
        }
        return names.ToArray();
    }

    /// <summary>A reference that can create, change or delete a file or directory (write-capable stream constructors
    /// included, whatever access a call site asks for).</summary>
    public static bool IsFileWrite(string reference) =>
        new[] { "System.IO.File.Write", "System.IO.File.Append", "System.IO.File.Create", "System.IO.File.Delete",
                "System.IO.File.Move", "System.IO.File.Copy", "System.IO.File.Replace", "System.IO.Directory.Create",
                "System.IO.Directory.Delete", "System.IO.Directory.Move" }
            .Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal))
        || reference is "System.IO.File.Open" or "System.IO.File.OpenWrite" or "System.IO.FileStream..ctor" or "System.IO.StreamWriter..ctor";

    /// <summary>For every surveyed reference, the top-level types whose method bodies use it (compiler-generated state
    /// machines, closures and lambdas count as their enclosing type). Read from IL only; nothing is executed.</summary>
    public static IReadOnlyDictionary<string, string[]> ReferencingTypes(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var users = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            string owner = TopLevelName(metadata, typeHandle);
            foreach (var methodHandle in metadata.GetTypeDefinition(typeHandle).GetMethods())
            {
                int rva = metadata.GetMethodDefinition(methodHandle).RelativeVirtualAddress;
                if (rva == 0) continue;
                var il = pe.GetMethodBody(rva).GetILReader();
                while (il.RemainingBytes > 0)
                {
                    int value = il.ReadByte();
                    if (value == 0xFE) value = 0xFE00 | il.ReadByte();
                    if (!OperandTypes.TryGetValue(value, out var operand)) throw new InvalidDataException($"Unknown IL opcode 0x{value:X} at {il.Offset - 1} in {owner}::{metadata.GetString(metadata.GetMethodDefinition(methodHandle).Name)}");
                    switch (operand)
                    {
                        case OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineField:
                            var token = MetadataTokens.EntityHandle(il.ReadInt32());
                            if (token.Kind == HandleKind.MethodSpecification)
                                token = metadata.GetMethodSpecification((MethodSpecificationHandle)token).Method;
                            if (token.Kind == HandleKind.MemberReference && Name(metadata, (MemberReferenceHandle)token) is { } name)
                            {
                                if (!users.TryGetValue(name, out var set)) users[name] = set = new(StringComparer.Ordinal);
                                set.Add(owner);
                            }
                            break;
                        case OperandType.InlineSwitch:
                            int targets = il.ReadInt32(); // read before Offset: `Offset += Read…` would load Offset first
                            il.Offset += 4 * targets;
                            break;
                        default: il.Offset += Size(operand); break;
                    }
                }
            }
        }
        return users.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
    }

    private static string? Name(MetadataReader metadata, MemberReferenceHandle handle)
    {
        var member = metadata.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference) return null;
        var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        string ns = metadata.GetString(type.Namespace);
        return ns.StartsWith("System.IO", StringComparison.Ordinal) || ns.StartsWith("System.Net", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.Win32", StringComparison.Ordinal) || ns == "System.Diagnostics" || ns == "System"
            ? ns + "." + metadata.GetString(type.Name) + "." + metadata.GetString(member.Name) : null;
    }

    private static string TopLevelName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        while (type.GetDeclaringType() is { IsNil: false } outer) type = metadata.GetTypeDefinition(outer);
        string ns = metadata.GetString(type.Namespace);
        return (ns.Length == 0 ? "" : ns + ".") + metadata.GetString(type.Name);
    }

    private static readonly Dictionary<int, OperandType> OperandTypes = typeof(OpCodes).GetFields()
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => (int)(ushort)o.Value, o => o.OperandType);

    private static int Size(OperandType operand) => operand switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => 4,
    };
}
