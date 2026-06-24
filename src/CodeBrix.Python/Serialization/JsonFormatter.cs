using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Nodes;

namespace CodeBrix.Python.Serialization;

/// <summary>
/// A self-describing <see cref="IFormatter"/> that serializes an object graph to JSON
/// (System.Text.Json) and then to UTF-8 bytes, as a managed replacement for the now-removed
/// <c>BinaryFormatter</c>.
/// <para>
/// This is an <b>additive</b> alternative formatter: it does not change any existing
/// CodeBrix.Python behavior. It honors the same runtime-serialization contract the legacy
/// <c>BinaryFormatter</c> relied on, so the existing <c>[Serializable]</c> / <see cref="ISerializable"/>
/// state types round-trip without modification:
/// </para>
/// <list type="bullet">
///   <item><see cref="ISerializable"/> types: serialized via <see cref="ISerializable.GetObjectData"/>
///   and reconstructed through the <c>(SerializationInfo, StreamingContext)</c> constructor.</item>
///   <item><c>[Serializable]</c> classes and any value-type (struct): serialized field-by-field
///   (public and non-public instance fields, excluding <c>[NonSerialized]</c>), including base-class
///   fields.</item>
///   <item>Primitives, <see cref="string"/>, <see cref="decimal"/>, <see cref="DateTime"/>,
///   <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>, <see cref="Guid"/>, <see cref="char"/>,
///   and enums.</item>
///   <item>Single-dimension arrays of any supported element type.</item>
///   <item>Polymorphism: every object records its concrete assembly-qualified type, so values stored
///   under a base-typed or <see cref="object"/>-typed slot reconstruct to their exact runtime type.</item>
///   <item>Reference identity and cycles among reference types (shared references are written once and
///   referred to by id). Cycles are supported when the cycle passes through at least one field-based
///   object or array; a cycle composed solely of <see cref="ISerializable"/> objects is reported as an
///   error rather than silently corrupting data.</item>
///   <item>Serialization callbacks (<see cref="OnSerializingAttribute"/>, <see cref="OnSerializedAttribute"/>,
///   <see cref="OnDeserializingAttribute"/>, <see cref="OnDeserializedAttribute"/>),
///   <see cref="IDeserializationCallback"/>, and <see cref="IObjectReference"/>.</item>
/// </list>
/// <para>
/// Not supported (intentionally out of scope; these are unused by CodeBrix.Python's runtime-state
/// serialization): <see cref="ISurrogateSelector"/> surrogates, multi-dimensional / non-zero-based
/// arrays, and <c>[Serializable]</c> delegates. Unsupported inputs throw <see cref="SerializationException"/>
/// rather than failing silently.
/// </para>
/// </summary>
public sealed class JsonFormatter : IFormatter
{
    // Envelope keys (the leading marker characters keep these distinct from ISerializable member names).
    private const string KeyRef = "$ref";        // back-reference to an already-emitted object: { "$ref": id }
    private const string KeyId = "$id";          // object identity for a reference type
    private const string KeyType = "$type";      // assembly-qualified type name to reconstruct
    private const string KeySimpleCode = "$p";   // primitive/simple type code (see SimpleCode)
    private const string KeySimpleValue = "$v";  // primitive/simple value, stored as an invariant string
    private const string KeyArrayElement = "$elem"; // array element type name
    private const string KeyArrayItems = "$items";  // array items
    private const string KeyFields = "$fields";     // field-based payload
    private const string KeySerializable = "$ser";  // ISerializable payload
    private const string KeyMemberType = "dt";      // declared type of an ISerializable member
    private const string KeyMemberValue = "v";      // value of an ISerializable member

    private readonly IFormatterConverter _converter = new FormatterConverter();

    /// <inheritdoc />
    public ISurrogateSelector? SurrogateSelector { get; set; }

    /// <inheritdoc />
    public SerializationBinder? Binder { get; set; }

    /// <inheritdoc />
    public StreamingContext Context { get; set; } = new StreamingContext(StreamingContextStates.All);

    /// <summary>Serializes <paramref name="graph"/> to <paramref name="serializationStream"/> as UTF-8 JSON.</summary>
    public void Serialize(Stream serializationStream, object graph)
    {
        if (serializationStream is null) throw new ArgumentNullException(nameof(serializationStream));

        var state = new WriteState();
        JsonNode? root = Encode(graph, graph?.GetType() ?? typeof(object), state);
        string json = root is null ? "null" : root.ToJsonString();
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        serializationStream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Reconstructs the object graph previously written by <see cref="Serialize"/>.</summary>
    public object Deserialize(Stream serializationStream)
    {
        if (serializationStream is null) throw new ArgumentNullException(nameof(serializationStream));

        using var ms = new MemoryStream();
        serializationStream.CopyTo(ms);
        string json = Encoding.UTF8.GetString(ms.ToArray());

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (Exception e)
        {
            throw new SerializationException("The stream does not contain valid JsonFormatter data.", e);
        }

        var state = new ReadState();
        object? result = Decode(root, typeof(object), state);
        state.RunDeserializationCallbacks();
        return result!;
    }

    // ---------------------------------------------------------------- write

    private sealed class WriteState
    {
        public readonly Dictionary<object, int> Ids = new(ReferenceEqualityComparer.Instance);
        public int Next;
    }

    private JsonNode? Encode(object? value, Type declaredType, WriteState state)
    {
        if (value is null) return null;

        Type rt = value.GetType();

        if (TryEncodeSimple(value, rt, out JsonObject? simple))
        {
            return simple;
        }

        bool isReference = !rt.IsValueType;
        if (isReference && state.Ids.TryGetValue(value, out int existing))
        {
            return new JsonObject { [KeyRef] = existing };
        }

        var envelope = new JsonObject();
        if (isReference)
        {
            int id = state.Next++;
            state.Ids[value] = id;            // register before recursing so field/array cycles resolve
            envelope[KeyId] = id;
        }

        if (rt.IsArray)
        {
            if (rt.GetArrayRank() != 1 || ((Array)value).GetLowerBound(0) != 0)
            {
                throw new SerializationException($"Only single-dimension, zero-based arrays are supported: '{rt}'.");
            }
            Type elementType = rt.GetElementType()!;
            envelope[KeyType] = rt.AssemblyQualifiedName;
            envelope[KeyArrayElement] = elementType.AssemblyQualifiedName;
            var items = new JsonArray();
            foreach (object? element in (Array)value)
            {
                items.Add(Encode(element, elementType, state));
            }
            envelope[KeyArrayItems] = items;
            return envelope;
        }

        if (value is ISerializable serializable)
        {
            InvokeCallbacks(value, rt, typeof(OnSerializingAttribute));
            var info = new SerializationInfo(rt, _converter);
            serializable.GetObjectData(info, Context);
            // GetObjectData may redirect the reconstruction type via SerializationInfo.SetType(...);
            // ObjectType reflects that (and is just rt otherwise).
            envelope[KeyType] = info.ObjectType.AssemblyQualifiedName;
            var members = new JsonObject();
            foreach (SerializationEntry entry in info)
            {
                members[entry.Name] = new JsonObject
                {
                    [KeyMemberType] = entry.ObjectType.AssemblyQualifiedName,
                    [KeyMemberValue] = Encode(entry.Value, entry.ObjectType, state),
                };
            }
            envelope[KeySerializable] = members;
            InvokeCallbacks(value, rt, typeof(OnSerializedAttribute));
            return envelope;
        }

        if (rt.IsSerializable || rt.IsValueType)
        {
            InvokeCallbacks(value, rt, typeof(OnSerializingAttribute));
            envelope[KeyType] = rt.AssemblyQualifiedName;
            var fields = new JsonObject();
            foreach ((string key, FieldInfo field) in GetSerializableFields(rt))
            {
                fields[key] = Encode(field.GetValue(value), field.FieldType, state);
            }
            envelope[KeyFields] = fields;
            InvokeCallbacks(value, rt, typeof(OnSerializedAttribute));
            return envelope;
        }

        throw new SerializationException(
            $"Type '{rt.AssemblyQualifiedName}' is not serializable. It is neither a supported primitive, " +
            "an array, an [Serializable] type, nor an ISerializable type.");
    }

    private bool TryEncodeSimple(object value, Type rt, out JsonObject? node)
    {
        node = null;
        string code;
        if (rt.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(rt);
            object raw = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            node = new JsonObject
            {
                [KeySimpleCode] = "enum",
                [KeyType] = rt.AssemblyQualifiedName,
                [KeySimpleValue] = Convert.ToString(raw, CultureInfo.InvariantCulture),
            };
            return true;
        }

        switch (value)
        {
            case bool b: code = "bool"; node = Simple(code, b ? "true" : "false"); return true;
            case char c: code = "char"; node = Simple(code, ((int)c).ToString(CultureInfo.InvariantCulture)); return true;
            case sbyte sb: node = Simple("i8", sb.ToString(CultureInfo.InvariantCulture)); return true;
            case byte by: node = Simple("u8", by.ToString(CultureInfo.InvariantCulture)); return true;
            case short s16: node = Simple("i16", s16.ToString(CultureInfo.InvariantCulture)); return true;
            case ushort u16: node = Simple("u16", u16.ToString(CultureInfo.InvariantCulture)); return true;
            case int i32: node = Simple("i32", i32.ToString(CultureInfo.InvariantCulture)); return true;
            case uint u32: node = Simple("u32", u32.ToString(CultureInfo.InvariantCulture)); return true;
            case long i64: node = Simple("i64", i64.ToString(CultureInfo.InvariantCulture)); return true;
            case ulong u64: node = Simple("u64", u64.ToString(CultureInfo.InvariantCulture)); return true;
            case float f32: node = Simple("f32", f32.ToString("R", CultureInfo.InvariantCulture)); return true;
            case double f64: node = Simple("f64", f64.ToString("R", CultureInfo.InvariantCulture)); return true;
            case decimal dec: node = Simple("dec", dec.ToString(CultureInfo.InvariantCulture)); return true;
            case string str: node = Simple("str", str); return true;
            case DateTime dt: node = Simple("date", dt.ToString("O", CultureInfo.InvariantCulture)); return true;
            case DateTimeOffset dto: node = Simple("dto", dto.ToString("O", CultureInfo.InvariantCulture)); return true;
            case TimeSpan ts: node = Simple("ts", ts.ToString("c", CultureInfo.InvariantCulture)); return true;
            case Guid g: node = Simple("guid", g.ToString("D")); return true;
            default: return false;
        }

        static JsonObject Simple(string code, string raw) => new() { [KeySimpleCode] = code, [KeySimpleValue] = raw };
    }

    // ---------------------------------------------------------------- read

    private sealed class ReadState
    {
        public readonly Dictionary<int, object> Objects = new();
        public readonly List<IDeserializationCallback> Callbacks = new();

        public void RunDeserializationCallbacks()
        {
            foreach (IDeserializationCallback cb in Callbacks)
            {
                cb.OnDeserialization(null);
            }
        }
    }

    private object? Decode(JsonNode? node, Type declaredType, ReadState state)
    {
        if (node is null) return null;

        if (node is not JsonObject obj)
        {
            throw new SerializationException("Malformed JsonFormatter data: expected an object envelope.");
        }

        if (obj.TryGetPropertyValue(KeyRef, out JsonNode? refNode))
        {
            int refId = refNode!.GetValue<int>();
            if (!state.Objects.TryGetValue(refId, out object? referenced))
            {
                throw new SerializationException(
                    $"Encountered a forward reference (id {refId}) that cannot be resolved. Cycles composed " +
                    "solely of ISerializable objects are not supported.");
            }
            return referenced;
        }

        if (obj.TryGetPropertyValue(KeySimpleCode, out JsonNode? codeNode))
        {
            return DecodeSimple(obj, codeNode!.GetValue<string>());
        }

        int? id = obj.TryGetPropertyValue(KeyId, out JsonNode? idNode) ? idNode!.GetValue<int>() : null;
        Type type = ResolveType(obj[KeyType]!.GetValue<string>());

        if (obj.TryGetPropertyValue(KeyArrayItems, out JsonNode? itemsNode))
        {
            Type elementType = ResolveType(obj[KeyArrayElement]!.GetValue<string>());
            var items = (JsonArray)itemsNode!;
            Array array = Array.CreateInstance(elementType, items.Count);
            if (id.HasValue) state.Objects[id.Value] = array;
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(Decode(items[i], elementType, state), i);
            }
            return array;
        }

        if (obj.TryGetPropertyValue(KeyFields, out JsonNode? fieldsNode))
        {
            object instance = RuntimeHelpers.GetUninitializedObject(type);
            if (id.HasValue && !type.IsValueType) state.Objects[id.Value] = instance;
            InvokeCallbacks(instance, type, typeof(OnDeserializingAttribute));
            var fields = (JsonObject)fieldsNode!;
            foreach ((string key, FieldInfo field) in GetSerializableFields(type))
            {
                if (fields.TryGetPropertyValue(key, out JsonNode? fieldNode))
                {
                    field.SetValue(instance, Decode(fieldNode, field.FieldType, state));
                }
            }
            InvokeCallbacks(instance, type, typeof(OnDeserializedAttribute));
            RegisterCallback(instance, state);
            return instance;
        }

        if (obj.TryGetPropertyValue(KeySerializable, out JsonNode? serNode))
        {
            var info = new SerializationInfo(type, _converter);
            foreach (KeyValuePair<string, JsonNode?> member in (JsonObject)serNode!)
            {
                var m = (JsonObject)member.Value!;
                Type memberType = ResolveType(m[KeyMemberType]!.GetValue<string>());
                info.AddValue(member.Key, Decode(m[KeyMemberValue], memberType, state), memberType);
            }

            ConstructorInfo ctor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(SerializationInfo), typeof(StreamingContext) },
                modifiers: null)
                ?? throw new SerializationException(
                    $"Type '{type}' implements ISerializable but is missing the (SerializationInfo, StreamingContext) constructor.");

            object instance = ctor.Invoke(new object[] { info, Context });
            if (instance is IObjectReference objRef)
            {
                instance = objRef.GetRealObject(Context);
            }
            if (id.HasValue && !type.IsValueType) state.Objects[id.Value] = instance;
            RegisterCallback(instance, state);
            return instance;
        }

        throw new SerializationException("Malformed JsonFormatter data: unrecognized envelope.");
    }

    private static object DecodeSimple(JsonObject obj, string code)
    {
        if (code == "enum")
        {
            // ResolveType is instance state-free for enums; call the static path directly.
            Type enumType = Type.GetType(obj[KeyType]!.GetValue<string>(), throwOnError: true)!;
            Type underlying = Enum.GetUnderlyingType(enumType);
            object raw = Convert.ChangeType(obj[KeySimpleValue]!.GetValue<string>(), underlying, CultureInfo.InvariantCulture);
            return Enum.ToObject(enumType, raw);
        }

        string s = obj[KeySimpleValue]!.GetValue<string>();
        return code switch
        {
            "bool" => bool.Parse(s),
            "char" => (char)int.Parse(s, CultureInfo.InvariantCulture),
            "i8" => sbyte.Parse(s, CultureInfo.InvariantCulture),
            "u8" => byte.Parse(s, CultureInfo.InvariantCulture),
            "i16" => short.Parse(s, CultureInfo.InvariantCulture),
            "u16" => ushort.Parse(s, CultureInfo.InvariantCulture),
            "i32" => int.Parse(s, CultureInfo.InvariantCulture),
            "u32" => uint.Parse(s, CultureInfo.InvariantCulture),
            "i64" => long.Parse(s, CultureInfo.InvariantCulture),
            "u64" => ulong.Parse(s, CultureInfo.InvariantCulture),
            "f32" => float.Parse(s, CultureInfo.InvariantCulture),
            "f64" => double.Parse(s, CultureInfo.InvariantCulture),
            "dec" => decimal.Parse(s, CultureInfo.InvariantCulture),
            "str" => s,
            "date" => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "dto" => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "ts" => TimeSpan.ParseExact(s, "c", CultureInfo.InvariantCulture),
            "guid" => Guid.Parse(s),
            _ => throw new SerializationException($"Unknown simple type code '{code}'."),
        };
    }

    private static void RegisterCallback(object instance, ReadState state)
    {
        if (instance is IDeserializationCallback callback)
        {
            state.Callbacks.Add(callback);
        }
    }

    // ---------------------------------------------------------------- shared helpers

    private Type ResolveType(string assemblyQualifiedName)
    {
        if (Binder is not null)
        {
            int comma = assemblyQualifiedName.IndexOf(',');
            string typeName = comma < 0 ? assemblyQualifiedName : assemblyQualifiedName[..comma].Trim();
            string assemblyName = comma < 0 ? string.Empty : assemblyQualifiedName[(comma + 1)..].Trim();
            Type? bound = Binder.BindToType(assemblyName, typeName);
            if (bound is not null) return bound;
        }

        return Type.GetType(assemblyQualifiedName, throwOnError: true)!;
    }

    /// <summary>
    /// Returns the serializable instance fields of <paramref name="type"/> (most-derived through base),
    /// excluding <c>[NonSerialized]</c> fields, paired with a stable, collision-free key.
    /// </summary>
    private static IReadOnlyList<(string Key, FieldInfo Field)> GetSerializableFields(Type type)
    {
        var fields = new List<FieldInfo>();
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsNotSerialized) continue;
                fields.Add(field);
            }
        }

        var nameCounts = new Dictionary<string, int>();
        foreach (FieldInfo field in fields)
        {
            nameCounts[field.Name] = nameCounts.GetValueOrDefault(field.Name) + 1;
        }

        var result = new List<(string, FieldInfo)>(fields.Count);
        foreach (FieldInfo field in fields)
        {
            string key = nameCounts[field.Name] > 1
                ? field.DeclaringType!.FullName + "::" + field.Name
                : field.Name;
            result.Add((key, field));
        }
        return result;
    }

    private void InvokeCallbacks(object instance, Type type, Type attributeType)
    {
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (MethodInfo method in current.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!method.IsDefined(attributeType, inherit: false)) continue;
                method.Invoke(instance, new object[] { Context });
            }
        }
    }
}
