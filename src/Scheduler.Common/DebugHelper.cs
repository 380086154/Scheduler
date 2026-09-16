namespace Acclaro.Platform.Common.Common;

public static class DebugHelper
{
#if DEBUG
    public static bool IsDebug => true;
#else
    public static bool IsDebug => false;
#endif
}
