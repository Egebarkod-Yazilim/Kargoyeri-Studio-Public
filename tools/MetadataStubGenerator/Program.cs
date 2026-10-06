using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: MetadataStubGenerator <assemblyPath> <projectDir> [generatedDirName]");
    return 1;
}

var assemblyPath = Path.GetFullPath(args[0]);
var projectDir = Path.GetFullPath(args[1]);
var generatedDirName = args.Length > 2 ? args[2] : "Generated";
var generatedDir = Path.Combine(projectDir, generatedDirName);

Directory.CreateDirectory(generatedDir);

var existingTypes = DiscoverExistingTypes(projectDir, generatedDir);
var loadContext = new RecoveryLoadContext(Path.GetDirectoryName(assemblyPath)!);
var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    if (type.IsNested || string.IsNullOrWhiteSpace(type.FullName))
    {
        continue;
    }

    if (existingTypes.Contains(type.FullName))
    {
        continue;
    }

    var source = RenderType(type);
    if (string.IsNullOrWhiteSpace(source))
    {
        continue;
    }

    var safeName = string.Join("_", type.FullName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
        .Replace('.', '_')
        .Replace('+', '_');
    var outputPath = Path.Combine(generatedDir, $"{safeName}.g.cs");
    File.WriteAllText(outputPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

return 0;

static HashSet<string> DiscoverExistingTypes(string projectDir, string generatedDir)
{
    var results = new HashSet<string>(StringComparer.Ordinal);
    var typeRegex = new Regex(@"\b(class|interface|enum|struct|record)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    var namespaceRegex = new Regex(@"namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);

    foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
    {
        if (file.StartsWith(generatedDir, StringComparison.OrdinalIgnoreCase) ||
            file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var text = File.ReadAllText(file);
        var nsMatch = namespaceRegex.Match(text);
        var ns = nsMatch.Success ? nsMatch.Groups[1].Value : string.Empty;

        foreach (Match match in typeRegex.Matches(text))
        {
            var name = match.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            results.Add(string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}");
        }
    }

    return results;
}

static string RenderType(Type type)
{
    var sb = new StringBuilder();
    sb.AppendLine("// Generated from assembly metadata during repository recovery.");
    sb.AppendLine("#nullable enable");
    if (!string.IsNullOrWhiteSpace(type.Namespace))
    {
        sb.Append("namespace ").Append(type.Namespace).AppendLine(";");
        sb.AppendLine();
    }

    if (type.IsEnum)
    {
        RenderEnum(sb, type);
        return sb.ToString();
    }

    if (type.IsInterface)
    {
        RenderInterface(sb, type);
        return sb.ToString();
    }

    RenderClassLike(sb, type);
    return sb.ToString();
}

static void RenderEnum(StringBuilder sb, Type type)
{
    sb.Append("public enum ").Append(GetTypeDeclarationName(type));
    var underlying = Enum.GetUnderlyingType(type);
    if (underlying != typeof(int))
    {
        sb.Append(" : ").Append(RenderTypeName(underlying));
    }

    sb.AppendLine();
    sb.AppendLine("{");

    var names = Enum.GetNames(type);
    for (var i = 0; i < names.Length; i++)
    {
        var name = names[i];
        var value = Convert.ChangeType(Enum.Parse(type, name), underlying);
        sb.Append("    ").Append(name).Append(" = ").Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine(i == names.Length - 1 ? string.Empty : ",");
    }

    sb.AppendLine("}");
}

static void RenderInterface(StringBuilder sb, Type type)
{
    sb.Append("public interface ").Append(GetTypeDeclarationName(type));
    AppendGenericConstraints(sb, type);
    sb.AppendLine();
    sb.AppendLine("{");

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (property.GetIndexParameters().Length > 0)
        {
            continue;
        }

        sb.Append("    ").Append(RenderTypeName(property.PropertyType)).Append(' ').Append(property.Name).Append(" { ");
        if (property.CanRead) sb.Append("get; ");
        if (property.CanWrite) sb.Append("set; ");
        sb.AppendLine("}");
    }

    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (method.IsSpecialName)
        {
            continue;
        }

        sb.Append("    ").Append(RenderMethodSignature(method)).AppendLine(";");
    }

    sb.AppendLine("}");
}

static void RenderClassLike(StringBuilder sb, Type type)
{
    var modifiers = new List<string> { "public" };
    if (type.IsAbstract && type.IsSealed)
    {
        modifiers.Add("static");
    }
    else
    {
        if (type.IsAbstract) modifiers.Add("abstract");
        if (type.IsSealed) modifiers.Add("sealed");
        if (!type.IsValueType) modifiers.Add("partial");
    }

    var kind = type.IsValueType && !type.IsPrimitive ? "struct" : "class";
    sb.Append(string.Join(' ', modifiers)).Append(' ').Append(kind).Append(' ').Append(GetTypeDeclarationName(type));

    var bases = new List<string>();
    if (type.BaseType is { } baseType && baseType != typeof(object) && !type.IsValueType)
    {
        bases.Add(RenderTypeName(baseType));
    }

    foreach (var iface in type.GetInterfaces().Where(i => i.IsPublic))
    {
        bases.Add(RenderTypeName(iface));
    }

    if (bases.Count > 0)
    {
        sb.Append(" : ").Append(string.Join(", ", bases.Distinct(StringComparer.Ordinal)));
    }

    AppendGenericConstraints(sb, type);
    sb.AppendLine();
    sb.AppendLine("{");

    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (field.IsSpecialName)
        {
            continue;
        }

        sb.Append("    public ");
        if (field.IsStatic) sb.Append("static ");
        if (field.IsInitOnly) sb.Append("readonly ");
        sb.Append(RenderTypeName(field.FieldType)).Append(' ').Append(field.Name).AppendLine(";");
    }

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (property.GetIndexParameters().Length > 0)
        {
            continue;
        }

        sb.Append("    public ");
        if ((property.GetMethod ?? property.SetMethod)?.IsStatic == true) sb.Append("static ");
        sb.Append(RenderTypeName(property.PropertyType)).Append(' ').Append(property.Name).Append(" { ");
        if (property.CanRead) sb.Append("get; ");
        if (property.CanWrite) sb.Append("set; ");
        sb.AppendLine("}");
    }

    foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        sb.Append("    public ").Append(type.Name.Split('`')[0]).Append('(');
        AppendParameters(sb, ctor.GetParameters());
        sb.AppendLine(") { }");
    }

    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (method.IsSpecialName)
        {
            continue;
        }

        sb.Append("    public ");
        if (method.IsStatic) sb.Append("static ");
        if (method.IsAbstract) sb.Append("abstract ");
        else if (method.IsVirtual && !method.IsFinal) sb.Append("virtual ");

        sb.Append(RenderMethodSignature(method));
        if (method.IsAbstract)
        {
            sb.AppendLine(";");
            continue;
        }

        sb.AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        throw new global::System.NotImplementedException();");
        sb.AppendLine("    }");
    }

    sb.AppendLine("}");
}

static string RenderMethodSignature(MethodInfo method)
{
    var sb = new StringBuilder();
    sb.Append(RenderTypeName(method.ReturnType)).Append(' ').Append(method.Name);
    if (method.IsGenericMethodDefinition)
    {
        sb.Append('<').Append(string.Join(", ", method.GetGenericArguments().Select(a => a.Name))).Append('>');
    }

    sb.Append('(');
    AppendParameters(sb, method.GetParameters());
    sb.Append(')');
    return sb.ToString();
}

static void AppendParameters(StringBuilder sb, IReadOnlyList<ParameterInfo> parameters)
{
    for (var i = 0; i < parameters.Count; i++)
    {
        var parameter = parameters[i];
        if (parameter.IsOut) sb.Append("out ");
        else if (parameter.ParameterType.IsByRef) sb.Append("ref ");

        var type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
        sb.Append(RenderTypeName(type)).Append(' ').Append(parameter.Name ?? $"arg{i}");

        if (i < parameters.Count - 1)
        {
            sb.Append(", ");
        }
    }
}

static string GetTypeDeclarationName(Type type)
{
    if (!type.IsGenericTypeDefinition)
    {
        return type.Name.Split('`')[0];
    }

    return $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(a => a.Name))}>";
}

static void AppendGenericConstraints(StringBuilder sb, Type type)
{
    if (!type.IsGenericTypeDefinition)
    {
        return;
    }

    foreach (var arg in type.GetGenericArguments())
    {
        var constraints = new List<string>();
        var attrs = arg.GenericParameterAttributes;

        if (attrs.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
        {
            constraints.Add("class");
        }

        if (attrs.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
        {
            constraints.Add("struct");
        }

        foreach (var constraintType in arg.GetGenericParameterConstraints())
        {
            if (constraintType == typeof(ValueType))
            {
                continue;
            }

            constraints.Add(RenderTypeName(constraintType));
        }

        if (attrs.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
        {
            constraints.Add("new()");
        }

        if (constraints.Count > 0)
        {
            sb.Append(" where ").Append(arg.Name).Append(" : ").Append(string.Join(", ", constraints));
        }
    }
}

static string RenderTypeName(Type type)
{
    if (type.IsByRef)
    {
        return RenderTypeName(type.GetElementType()!);
    }

    if (type.IsArray)
    {
        return $"{RenderTypeName(type.GetElementType()!)}[]";
    }

    if (type.IsGenericParameter)
    {
        return type.Name;
    }

    var aliases = new Dictionary<Type, string>
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(string)] = "string",
        [typeof(char)] = "char",
        [typeof(object)] = "object"
    };

    if (aliases.TryGetValue(type, out var alias))
    {
        return alias;
    }

    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
    {
        return $"{RenderTypeName(type.GetGenericArguments()[0])}?";
    }

    if (type.IsGenericType)
    {
        var genericDefName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        genericDefName = genericDefName.Split('`')[0].Replace('+', '.');
        var args = string.Join(", ", type.GetGenericArguments().Select(RenderTypeName));
        return $"global::{genericDefName}<{args}>";
    }

    return $"global::{(type.FullName ?? type.Name).Replace('+', '.')}";
}

sealed class RecoveryLoadContext : AssemblyLoadContext
{
    private readonly string _assemblyDirectory;

    public RecoveryLoadContext(string assemblyDirectory) : base(isCollectible: true)
    {
        _assemblyDirectory = assemblyDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var candidate = Path.Combine(_assemblyDirectory, $"{assemblyName.Name}.dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
