using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using NudityMattersMore; // For NMM.PawnInteractionManager, NMM.PawnInteraction, NMM.PawnState, NMM.InteractionType
using System;

namespace NudityMattersMore_opinions
{

    /// <summary>
    /// MapComponent responsible for tracking new interactions in NMM
    /// and generating corresponding situational opinions.
    /// This component will be loaded for each map.
    /// </summary>
    public class NMMOpinionsMapComponent : MapComponent
    {
        private Dictionary<int, Dictionary<string, int>> lastProcessedInteractionTicks = new Dictionary<int, Dictionary<string, int>>();
        private const int CheckFrequencyTicks = 120; // Every 2 seconds

        public NMMOpinionsMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Find.TickManager.TicksGame % CheckFrequencyTicks != 0)
            {
                return;
            }

            if (NudityMattersMore.PawnInteractionManager.InteractionLogs == null)
            {
                return;
            }

            foreach (var kvpLog in NudityMattersMore.PawnInteractionManager.InteractionLogs)
            {
                Pawn ownerPawn = Find.CurrentMap?.mapPawns?.AllPawns?.FirstOrDefault(p => p.thingIDNumber == kvpLog.Key);

                if (ownerPawn == null) continue;

                PawnInteractionLog pawnLog = kvpLog.Value;

                ProcessInteractionsList(ownerPawn, pawnLog.ObserverInteractions, true);
                ProcessInteractionsList(ownerPawn, pawnLog.ObservedInteractions, false);
            }
        }

        private void ProcessInteractionsList(Pawn ownerPawn, List<PawnInteraction> interactions, bool isObserverList)
        {
            if (interactions == null || !interactions.Any()) return;

            if (!lastProcessedInteractionTicks.ContainsKey(ownerPawn.thingIDNumber))
            {
                lastProcessedInteractionTicks[ownerPawn.thingIDNumber] = new Dictionary<string, int>();
            }
            Dictionary<string, int> pawnLastTicks = lastProcessedInteractionTicks[ownerPawn.thingIDNumber];

            for (int i = 0; i < interactions.Count; i++)
            {
                PawnInteraction interaction = interactions[i];

                if (interaction == null || interaction.Pawn == null) continue;

                Pawn otherPawn = interaction.Pawn;

                // =================================================================================
                // НОВОЕ ИСПРАВЛЕНИЕ: Проверяем, что вторая пешка тоже на карте.
                // Это предотвращает ошибки с караванами, мертвыми или покинувшими карту пешками.
                if (!otherPawn.Spawned)
                {
                    continue;
                }

                // УЛУЧШЕННОЕ ИСПРАВЛЕНИЕ: Проверяем на состояние 'None' более корректным способом,
                // но позволяем Processing для InteractionType.Covering, даже если PawnState равно None,
                // так как это может быть переходное состояние или специфическое для "прикрытия".
                // Динамические пузырьки могут срабатывать и в этих случаях, а лог - нет.
                if (interaction.InteractionType != NudityMattersMore.InteractionType.Covering && interaction.PawnState == PawnState.None)
                {
                    continue;
                }
                // =================================================================================

                string interactionKey = $"{ownerPawn.thingIDNumber}_{(isObserverList ? "ObservedByMe" : "ObservedMe")}_{otherPawn.thingIDNumber}_{interaction.InteractionType}_{interaction.PawnState}";

                if (pawnLastTicks.ContainsKey(interactionKey) && pawnLastTicks[interactionKey] >= interaction.GameTick)
                {
                    continue;
                }

                try
                {
                    SituationalOpinionHelper.GenerateAndAddSituationalOpinionEntry(
                        ownerPawn,
                        otherPawn,
                        interaction.InteractionType,
                        interaction.PawnState,
                        interaction.Aware,
                        isObserverList
                    );
                }
                catch (Exception ex)
                {
                    Log.Error($"[NMM Opinions MapComponent] Error processing situational opinion for {ownerPawn.LabelShort} (Interaction: {interactionKey}): {ex.Message}\n{ex.StackTrace}");
                }

                pawnLastTicks[interactionKey] = interaction.GameTick;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
