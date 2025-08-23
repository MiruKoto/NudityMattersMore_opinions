using HarmonyLib;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace NudityMattersMore_opinions
{
    // НОВЫЙ КЛАСС: Хранит всю необходимую информацию для динамической реплики.
    // Это решает проблему с перспективой, сохраняя, кто был кем в момент события.
    public class DynamicLogTextInfo
    {
        public string RawText;
        public Pawn OriginalObserver;
        public Pawn OriginalObserved;
        public BodyPartDef BodyPart;
    }

    // Статический класс для хранения нашего динамического текста, связанного с конкретной записью в журнале.
    public static class DynamicLogTextStore
    {
        // ИЗМЕНЕНО: Словарь теперь хранит объекты DynamicLogTextInfo вместо простых строк.
        public static Dictionary<LogEntry, DynamicLogTextInfo> storedInfo = new Dictionary<LogEntry, DynamicLogTextInfo>();
    }

    [StaticConstructorOnStartup]
    public static class NMM_DynamicText_Patches
    {
        static NMM_DynamicText_Patches()
        {
            if (ModLister.GetActiveModWithIdentifier("JPT.speakup") == null)
            {
                Log.Warning("[NMM Opinions] 'SpeakUp' mod not found. Dynamic bubble text feature will be disabled.");
                return;
            }

            var harmony = new Harmony("shark510.nuditymattersmoreopinions.dynamictext.patch");

            harmony.Patch(
                original: AccessTools.Method(typeof(PlayLog), nameof(PlayLog.Add)),
                postfix: new HarmonyMethod(typeof(NMM_DynamicText_Patches), nameof(AddToLog_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(PlayLogEntry_Interaction), "ToGameStringFromPOV_Worker"),
                prefix: new HarmonyMethod(typeof(NMM_DynamicText_Patches), nameof(ToGameStringFromPOV_Worker_Prefix))
            );

            ModLog.Message("[NMM Opinions - Dynamic Text] Patches for storing and retrieving dynamic log text applied.");
        }

        // ИЗМЕНЕНО: Теперь мы проверяем и сохраняем объект DynamicLogTextInfo.
        public static void AddToLog_Postfix(LogEntry entry)
        {
            // Проверяем, является ли это записью о взаимодействии и есть ли у нас информация, ожидающая из FireSingleCommentary.
            if (entry is PlayLogEntry_Interaction && NMMFixationLogPatches.LastDynamicTextInfo != null)
            {
                // Связываем наш объект с информацией о реплике с этим конкретным экземпляром записи в журнале.
                DynamicLogTextStore.storedInfo[entry] = NMMFixationLogPatches.LastDynamicTextInfo;

                // Теперь мы можем очистить временную переменную, так как информация надежно сохранена.
                NMMFixationLogPatches.LastDynamicTextInfo = null;
            }
        }

        // ИЗМЕНЕНО: Логика для извлечения информации и вызова ProcessOpinionText с правильными ролями.
        public static bool ToGameStringFromPOV_Worker_Prefix(PlayLogEntry_Interaction __instance, ref string __result)
        {
            // Проверяем, есть ли у нас сохраненная информация для этой конкретной записи в журнале.
            if (DynamicLogTextStore.storedInfo.TryGetValue(__instance, out DynamicLogTextInfo info))
            {
                // *** ДОБАВЛЕНА ПРОВЕРКА НА NULL ДЛЯ ПЕШЕК ***
                // Если OriginalObserver или OriginalObserved оказались null,
                // это означает, что данные некорректны. В этом случае
                // мы позволяем оригинальному методу выполниться, чтобы избежать NRE.
                if (info.OriginalObserver == null || info.OriginalObserved == null)
                {
                    // ИСПРАВЛЕНО: Использование ToLabel() вместо Label для PlayLogEntry_Interaction
                    Log.Warning($"[NMM Opinions] Dynamic log text info has null observer or observed pawn. Falling back to original method to prevent NRE.");
                    DynamicLogTextStore.storedInfo.Remove(__instance); // Очищаем некорректную запись
                    return true; // Позволяем оригинальному методу выполниться
                }

                // Мы нашли нашу информацию! Теперь мы обрабатываем текст, передавая правильные роли изначального события.
                // Это исправляет ошибку, когда Анна "видела Анну".
                __result = SituationalOpinionHelper.ProcessOpinionText(
                    info.RawText,
                    info.OriginalObserver, // Передаем истинного наблюдателя события
                    info.OriginalObserved,  // Передаем истинного наблюдаемого
                    info.BodyPart          // Передаем часть тела, если она была
                );

                // Возвращая false, мы полностью пропускаем исходный метод,
                // избегая вызова GrammarResolver и ошибки "unresolvable".
                return false;
            }

            // Если у нас нет сохраненной информации для этой записи, позволяем исходному методу выполниться.
            return true;
        }
    }
}
