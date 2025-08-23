using Verse;

namespace NudityMattersMore_opinions
{
    /// <summary>
    /// A safe logging utility class that prevents NullReferenceExceptions during startup.
    /// It checks if the mod settings are loaded before attempting to access them.
    /// </summary>
    public static class ModLog
    {
        private const string Prefix = "[NMM Opinions]";

        /// <summary>
        /// Logs a message to the console only if debug logging is enabled in the mod settings.
        /// Safely handles cases where settings might not be initialized yet.
        /// </summary>
        /// <param name="text">The message to log.</param>
        public static void Message(string text)
        {
            // ЭТО КЛЮЧЕВОЕ ИЗМЕНЕНИЕ:
            // Мы проверяем, что settings не равно null, ПРЕЖДЕ чем обращаться к enableDebugLogging.
            // Если settings еще не загрузились, ошибка не произойдет, а сообщение просто не будет выведено в лог.
            if (NudityMattersMore_opinions_Mod.settings != null && NudityMattersMore_opinions_Mod.settings.enableDebugLogging)
            {
                Log.Message($"{Prefix} {text}");
            }
        }
    }
}
