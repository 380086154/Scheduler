using System;
using System.Collections.Generic;
using System.Text;
using Acclaro.Platform.Common.Common;

namespace Acclaro.Platform.Scheduler;

internal static class AppLifetime
{
    private static SingleInstanceHelper? _singleInstance;

    public static bool TryEnter()
    {
        _singleInstance = new SingleInstanceHelper();
        return _singleInstance.CheckAndActivateInstance(false);
    }

    public static void ReleaseSingleInstanceLock()
    {
        _singleInstance?.Dispose();
        _singleInstance = null;
    }
}