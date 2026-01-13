using System;

namespace ParseLord2.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class HoverInfoAttribute : Attribute
{
    internal HoverInfoAttribute(string hoverText) => HoverText = hoverText;
    public string HoverText { get; set; }
}