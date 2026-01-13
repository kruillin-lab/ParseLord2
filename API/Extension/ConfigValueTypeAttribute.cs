namespace ParseLord2.API.Extension;

using System;

/// <summary>
/// Declares the value type associated with a config enum option.
/// Used by IPC/UI helpers to interpret values.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ConfigValueTypeAttribute : Attribute
{
    public ConfigValueTypeAttribute(Type valueType) => ValueType = valueType;

    public Type ValueType { get; }
}
