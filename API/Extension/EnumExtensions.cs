namespace ParseLord2.API.Extension;

using System;
using System.Linq;
using System.Reflection;
using global::ParseLord2.API.Enum;

public static class EnumExtensions
{
    public static Type ValueType(this AutoRotationConfigOption option)
    {
        var field = typeof(AutoRotationConfigOption).GetField(option.ToString());
        var attr = field?.GetCustomAttributes(typeof(ConfigValueTypeAttribute), false)
                        ?.OfType<ConfigValueTypeAttribute>()
                        ?.FirstOrDefault();
        return attr?.ValueType ?? typeof(string);
    }

    public static string Description(this BailMessage msg) => msg switch
    {
        BailMessage.LiveDisabled => "IPC is disabled in configuration.",
        BailMessage.InvalidLease => "Invalid lease request.",
        BailMessage.BlacklistedLease => "Lease request rejected (blacklisted).",
        _ => msg.ToString(),
    };
}
