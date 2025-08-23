using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NudityMattersMore_opinions
{
    [StaticConstructorOnStartup]
    public static class SituationalOpinionDefCache
    {
        /// <summary>
        /// Кэшированный список всех специфичных (не базовых) мнений.
        /// </summary>
        public static readonly List<OpinionDef_Situational> SpecificOpinions;

        /// <summary>
        /// Кэшированная ссылка на базовое (запасное) мнение.
        /// </summary>
        public static readonly OpinionDef_Situational BaseOpinion;

        static SituationalOpinionDefCache()
        {
            // Находим и кэшируем все Defs, у которых есть расширение с условиями.
            // Это будут наши "специфичные" мнения, которые мы фильтруем в игре.
            SpecificOpinions = DefDatabase<OpinionDef_Situational>.AllDefsListForReading
                .Where(def => def.GetModExtension<OpinionConditionExtension_Situational>() != null)
                .ToList();

            // Находим и кэшируем единственное базовое мнение.
            BaseOpinion = DefDatabase<OpinionDef_Situational>.AllDefsListForReading
                .FirstOrDefault(def => def.GetModExtension<IsBaseOpinionExtension>() != null);

            ModLog.Message("[NMM Opinions] SituationalOpinionDefCache: Caching complete. " +
                           $"Found {SpecificOpinions.Count} specific opinions and {(BaseOpinion != null ? "1 base opinion" : "0 base opinions")}.");
        }
    }
}