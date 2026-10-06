using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Controller'lardan TempData'ya tipli flash mesaj yazmak icin helper.
/// Layout'ta tipi (success/error/warning/info) pill rengini belirler.
/// </summary>
public static class StudioFlash
{
    private const string MsgKey  = "StudioMessage";
    private const string TypeKey = "StudioMessageType";

    public static void Success(ITempDataDictionary t, string message) => Set(t, message, "success");
    public static void Error  (ITempDataDictionary t, string message) => Set(t, message, "error");
    public static void Warning(ITempDataDictionary t, string message) => Set(t, message, "warning");
    public static void Info   (ITempDataDictionary t, string message) => Set(t, message, "info");

    private static void Set(ITempDataDictionary t, string message, string type)
    {
        t[MsgKey]  = message;
        t[TypeKey] = type;
    }
}
