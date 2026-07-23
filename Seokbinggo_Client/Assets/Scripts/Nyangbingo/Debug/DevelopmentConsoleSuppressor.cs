#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using UnityEngine;

namespace Nyangbingo.Debugging
{
    public static class DevelopmentConsoleSuppressor
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void DisableDevelopmentConsole()
        {
            UnityEngine.Debug.developerConsoleEnabled = false;
            UnityEngine.Debug.developerConsoleVisible = false;
        }
    }
}
#endif
