using RimWorld;
using UnityEngine;
using Verse;
using rjw; // RJW method access
using NudityMattersMore; //  NMM classes
using System; // Added: Needed for System.Exception
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Linq operations
using Verse.Sound;
using rjw.Modules.Shared.Extensions; // PawnExtensions if needed
using System.Reflection; // To reach other mods

namespace NudityMattersMore_opinions
{
    // Make sure this class is initialized by RimWorld
    [StaticConstructorOnStartup]
    public class ITab_Pawn_NMMOpinions : ITab
    {
        // Define our desired tab dimensions.
        private const float OurContentBaseWidth = 770f;     // Original width of NMM content
        private const float OpinionsAreaWidth = 562f;       // Width of the area for opinions
        // private const float RenderAreaWidth = 300f;     // COMMENTED: Width of area for pawn rendering
        private const float VerticalLineThickness = 2f;    // Thickness of vertical line
        private const float HorizontalSectionGap = 10f;    // Padding between sections and lines

        private const float OurTotalHeight = 510f;

        // Total width calculations: NMM + Gap + Line + Opinions
        private const float OurTotalWidth = OurContentBaseWidth + HorizontalSectionGap + VerticalLineThickness + OpinionsAreaWidth;

        // --- ИСПРАВЛЕНИЕ #1: Логика сброса состояния ---
        // Переменная для хранения пешки, для которой вкладка была открыта в последний раз.
        private Pawn _lastPawnForTab = null;
        // Переменная для хранения пешки, которую мы наблюдаем (цель). НЕ ДЕЛАЙТЕ ЕЕ STATIC.
        private Pawn _selectedTargetPawn = null;

        // Enum for pawn filtering types
        private enum PawnFilterType { MapColonists, GlobalColonists, AllRegistered }
        private static PawnFilterType _currentFilter = PawnFilterType.MapColonists; // Initial state of the filter

        // ADDED: Static variable to toggle visibility of our opinion log
        private static bool _showOpinionLog = true; // Active by default

        // ADDED: Scroll position for our opinion log
        private static Vector2 _opinionLogScrollPosition = Vector2.zero;

        // Constructor
        public ITab_Pawn_NMMOpinions()
        {
            // Set the label key for the tab. This will be displayed as the tab's name.
            labelKey = "NMM_Nudity";
            tutorTag = "NMM_Nudity";

            // Set the total size of the tab directly in the constructor.
            this.size = new Vector2(OurTotalWidth, OurTotalHeight);
            ModLog.Message($"[NMM Opinions] ITab_Pawn_NMMOpinions: Constructor - Tab size set to {this.size.x}x{this.size.y}");
        }

        // Custom property to get the selected Pawn, replicating NMM's SelPawnForNMMInfo logic.
        protected Pawn SelPawnForOpinions
        {
            get
            {
                if (this.SelPawn != null)
                {
                    return this.SelPawn;
                }
                if (base.SelThing is Corpse corpse)
                {
                    return corpse.InnerPawn;
                }
                throw new InvalidOperationException("NMM Opinions tab on non-pawn non-corpse " + base.SelThing);
            }
        }

        // Override IsVisible to control when the tab appears.
        public override bool IsVisible
        {
            get
            {
                Pawn selectedPawn = null;
                try
                {
                    selectedPawn = this.SelPawnForOpinions;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[NMM Opinions] Unexpected error in SelPawnForOpinions for IsVisible: {ex.Message}");
                    return false;
                }

                if (selectedPawn == null)
                {
                    return false;
                }

                bool canDoLoving = false;
                try
                {
                    canDoLoving = rjw.xxx.can_do_loving(selectedPawn);
                }
                catch (Exception ex)
                {
                    Log.Error($"[NMM Opinions] Error checking rjws.xxx.can_do_loving for IsVisible: {ex.Message}");
                    canDoLoving = false;
                }

                bool isHuman = selectedPawn.def.race.Humanlike;

                bool isEntityOrGhoul = false;
                try
                {
                    isEntityOrGhoul = NudityMattersMore.InfoHelper.EntityOrGhoul(selectedPawn);
                }
                catch (Exception ex)
                {
                    Log.Error($"[NMM Opinions] Error checking NudityMattersMore.InfoHelper.EntityOrGhoul for IsVisible: {ex.Message}");
                    isEntityOrGhoul = false;
                }

                return canDoLoving && isHuman && !isEntityOrGhoul;
            }
        }


        // Override FillTab to draw all content.
        protected override void FillTab()
        {
            Pawn pawnTab; // Это пешка, чья вкладка сейчас открыта
            try
            {
                pawnTab = this.SelPawnForOpinions;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (pawnTab == null) return;

            // --- ИСПРАВЛЕНИЕ #1: Логика сброса состояния ---
            // Если мы открыли вкладку для новой пешки (или загрузили сохранение),
            // то сбрасываем выбранную цель на текущую пешку.
            if (_lastPawnForTab != pawnTab)
            {
                _lastPawnForTab = pawnTab;
                _selectedTargetPawn = pawnTab; // Сбрасываем выбор на саму пешку
                _opinionLogScrollPosition = Vector2.zero; // Также сбрасываем позицию скролла
            }

            // Запасная инициализация, если цель по какой-то причине null
            if (_selectedTargetPawn == null)
            {
                _selectedTargetPawn = pawnTab;
            }

            Pawn observerPawn;
            Pawn observedPawn;

            bool isObserverMode = ITabUitility.ObserverMenu();

            if (isObserverMode)
            {
                observerPawn = pawnTab;
                observedPawn = _selectedTargetPawn;
            }
            else
            {
                observedPawn = pawnTab;
                observerPawn = _selectedTargetPawn;
            }

            PawnOpinionMemory currentObserverMemory = null;
            if (observerPawn != null)
            {
                currentObserverMemory = OpinionHelper.GetOrCreatePawnOpinionMemory(observerPawn);
            }

            // Accessing NMM's TabGender via reflection
            NudityMattersMore.TabGender nmmCurrentTabGender = NudityMattersMore.TabGender.Female;
            try
            {
                Type iTabUitilityType = typeof(NudityMattersMore.ITabUitility);
                FieldInfo tabGenderField = iTabUitilityType.GetField("tabGender", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (tabGenderField != null)
                {
                    nmmCurrentTabGender = (NudityMattersMore.TabGender)tabGenderField.GetValue(null);
                }
                else
                {
                    Log.Warning("[NMM Opinions] Field 'tabGender' not found in ITabUitility via reflection. Falling back to default gender.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[NMM Opinions] Error getting NMM TabGender via reflection: {ex.Message}");
                nmmCurrentTabGender = NudityMattersMore.InfoHelper.GetTabGender(pawnTab);
            }

            // --- Shift NMM Content Area to the left ---
            Rect originalContentArea = new Rect(0f, 0f, OurContentBaseWidth, OurTotalHeight);
            ITabUitility.DrawNMMCard(originalContentArea, pawnTab);

            Widgets.DrawLineVertical(originalContentArea.xMax + (HorizontalSectionGap / 2) - (VerticalLineThickness / 2), 0, OurTotalHeight);

            Rect opinionsContentArea = new Rect(originalContentArea.xMax + HorizontalSectionGap, 0f, OpinionsAreaWidth, OurTotalHeight);
            Widgets.BeginGroup(opinionsContentArea);
            Listing_Standard customListing = new Listing_Standard();
            customListing.Begin(new Rect(0f, 0f, opinionsContentArea.width, opinionsContentArea.height));

            // Controls for dropdown and filter
            float controlY = customListing.CurHeight;
            float buttonHeight = 30f;
            float padding = 5f; // Defined only once here

            float totalControlsWidth = opinionsContentArea.width - 2 * padding;
            float dropdownButtonRelativeWidth = 0.35f;
            float filterButtonRelativeWidth = 0.25f;

            float dropdownWidth = totalControlsWidth * dropdownButtonRelativeWidth - padding;
            float filterWidth = totalControlsWidth * filterButtonRelativeWidth - padding;

            Rect dropdownRect = new Rect(0, controlY, dropdownWidth, buttonHeight);
            Rect filterRect = new Rect(opinionsContentArea.width - filterWidth, controlY, filterWidth, buttonHeight);

            // Dynamic text in the middle
            string dynamicRelationshipText = "";
            if (observerPawn != null && observedPawn != null)
            {
                if (observerPawn == observedPawn)
                {
                    dynamicRelationshipText = $"{observerPawn.LabelShort} observes Self";
                }
                else if (isObserverMode)
                {
                    dynamicRelationshipText = $"{pawnTab.LabelShort} observes {_selectedTargetPawn.LabelShort}";
                }
                else
                {
                    dynamicRelationshipText = $"{_selectedTargetPawn.LabelShort} observes {pawnTab.LabelShort}";
                }
            }
            else
            {
                dynamicRelationshipText = "No pawn selected";
            }
            Rect dynamicTextRect = new Rect(dropdownRect.xMax + padding, controlY, opinionsContentArea.width - dropdownWidth - filterWidth - 2 * padding, buttonHeight);

            // Dropdown button for selecting pawn
            string dropdownLabel = (_selectedTargetPawn != null) ? _selectedTargetPawn.LabelShort : "Select Pawn...";
            if (Widgets.ButtonText(dropdownRect, dropdownLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();

                if (observerPawn != null)
                {
                    options.Add(new FloatMenuOption(
                        $"Observe Self ({observerPawn.LabelShort})",
                        delegate {
                            _selectedTargetPawn = observerPawn;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                        },
                        extraPartOnGUI: (rect) => { return false; }
                    ));
                }

                List<Pawn> availablePawns = GetFilteredAvailablePawns(pawnTab, isObserverMode, nmmCurrentTabGender);

                foreach (Pawn p in availablePawns)
                {
                    if (p == observerPawn) continue;

                    options.Add(new FloatMenuOption(p.LabelShort, () => {
                        _selectedTargetPawn = p;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }));
                }
                if (!availablePawns.Any() && options.Count == 0)
                {
                    options.Add(new FloatMenuOption("No pawns found", null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(dynamicTextRect, dynamicRelationshipText);
            Text.Anchor = TextAnchor.UpperLeft;

            // Filter button
            string filterLabel = $"{_currentFilter.ToString()}";
            if (Widgets.ButtonText(filterRect, filterLabel))
            {
                _currentFilter = (PawnFilterType)(((int)_currentFilter + 1) % Enum.GetValues(typeof(PawnFilterType)).Length);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            customListing.Gap(buttonHeight + padding);
            Rect lineRect1 = customListing.GetRect(2f);
            Widgets.DrawLineHorizontal(lineRect1.x, lineRect1.y, lineRect1.width);
            customListing.Gap(12f);


            // Conditional rendering for opinions based on whether a target pawn is selected
            if (_selectedTargetPawn == null || currentObserverMemory == null)
            {
                customListing.Label("No pawn selected to view opinions.");
            }
            else // A target pawn is selected, show opinions.
            {
                if (observerPawn == null || observedPawn == null || currentObserverMemory == null)
                {
                    customListing.Label("Error: Cannot display opinion. Either observer, observed pawn, or observer's memory is missing.");
                    customListing.End();
                    Widgets.EndGroup();
                    return;
                }

                string nudityStatus = OpinionHelper.GetNudityStatus(observedPawn);

                BodyPartDef chestDef = DefDatabase<BodyPartDef>.GetNamed("Chest");
                BodyPartDef genitalsDef = DefDatabase<BodyPartDef>.GetNamed("Genitals");
                BodyPartDef anusDef = DefDatabase<BodyPartDef>.GetNamed("Anus");

                // Chest/Breasts Opinion
                if (observedPawn.GetBreastList().Any())
                {
                    PartOpinionData chestOpinion = OpinionHelper.GetOpinionDataForPart(observerPawn, observedPawn, chestDef, currentObserverMemory, nudityStatus);

                    string partDisplayName = (observedPawn.gender == Gender.Female) ? "breasts" : "chest";
                    customListing.Label($"{observerPawn.LabelShort}'s opinion about {observedPawn.LabelShort}'s {partDisplayName}:");
                    customListing.Gap(4f);
                    customListing.Label($"   {chestOpinion.PartSpecificLabel} ({chestOpinion.SizeLabel})");

                    if (observedPawn.gender == Gender.Male && (chestOpinion.OpinionText == "Не наблюдалось пока." || chestOpinion.OpinionText.Contains("No opinion def found")))
                    {
                        customListing.Label($"   {observerPawn.LabelShort} doesn't have any specific opinion about {observedPawn.LabelShort}'s chest/nipples.");
                    }
                    else
                    {
                        customListing.Label(chestOpinion.OpinionText);
                    }
                }
                else
                {
                    customListing.Label("Chest opinion: N/A (No breasts detected)");
                }
                customListing.Gap(12f);
                Rect lineRect2 = customListing.GetRect(2f);
                Widgets.DrawLineHorizontal(lineRect2.x, lineRect2.y, lineRect2.width);
                customListing.Gap(12f);

                // Genitals Opinion
                if (observedPawn.GetGenitalsList().Any())
                {
                    PartOpinionData genitalsOpinion = OpinionHelper.GetOpinionDataForPart(observerPawn, observedPawn, genitalsDef, currentObserverMemory, nudityStatus);

                    string partDisplayName = (observedPawn.gender == Gender.Female && rjw.PawnExtensions.GetGenitalsList(observedPawn).Any()) ? "vagina" :
                                             (observedPawn.gender == Gender.Male && rjw.PawnExtensions.GetGenitalsList(observedPawn).Any()) ? "penis" : "genitals";

                    customListing.Label($"{observerPawn.LabelShort}'s opinion about {observedPawn.LabelShort}'s {partDisplayName}:");
                    customListing.Gap(4f);
                    customListing.Label($"   {genitalsOpinion.PartSpecificLabel} ({genitalsOpinion.SizeLabel})");
                    customListing.Label(genitalsOpinion.OpinionText);
                }
                else
                {
                    customListing.Label("Genitals opinion: N/A (No genitals detected)");
                }
                customListing.Gap(12f);
                Rect lineRect3 = customListing.GetRect(2f);
                Widgets.DrawLineHorizontal(lineRect3.x, lineRect3.y, lineRect3.width);
                customListing.Gap(12f);

                // Anus Opinion
                if (observedPawn.GetAnusList().Any())
                {
                    PartOpinionData anusOpinion = OpinionHelper.GetOpinionDataForPart(observerPawn, observedPawn, anusDef, currentObserverMemory, nudityStatus);

                    customListing.Label($"{observerPawn.LabelShort}'s opinion about {observedPawn.LabelShort}'s anus:");
                    customListing.Gap(4f);
                    customListing.Label($"   {anusOpinion.PartSpecificLabel} ({anusOpinion.SizeLabel})");
                    customListing.Label(anusOpinion.OpinionText);
                }
                else
                {
                    customListing.Label("Anus opinion: N/A (No anus detected)");
                }
                customListing.Gap(20f);
            }

            customListing.End();
            Widgets.EndGroup(); // End the opinions group

            /* --- comented out, pawn render addon ---
            // --- Vertical Line 3 (after Opinions and before Render Area) ---
            Widgets.DrawLineVertical(opinionsContentArea.xMax + (HorizontalSectionGap / 2) - (VerticalLineThickness / 2), 0, OurTotalHeight);

            // --- Draw Placeholder for Nude Pawn Rendering (Rightmost) ---
            Rect pawnRenderContainerArea = new Rect(opinionsContentArea.xMax + HorizontalSectionGap, 0f, RenderAreaWidth, OurTotalHeight);

            // Draw a box as a placeholder
            Widgets.DrawBox(pawnRenderContainerArea);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(pawnRenderContainerArea, "Nude Pawn Render Area\n(Future Feature)");
            Text.Anchor = TextAnchor.UpperLeft;
            */


            // Log switching button
            float toggleButtonWidth = 150f;
            float toggleButtonHeight = 30f;
            // 'padding' is already defined above, so remove the duplicate declaration.

            // Position the button at the top center of the NMM area
            Rect toggleButtonRect = new Rect(
                originalContentArea.x + (originalContentArea.width / 2f) - (toggleButtonWidth / 2f),
                padding, // Top padding
                toggleButtonWidth,
                toggleButtonHeight
            );

            string toggleButtonLabel = _showOpinionLog ? "Interaction Log" : "Thoughts";


            if (Widgets.ButtonText(toggleButtonRect, toggleButtonLabel))
            {
                _showOpinionLog = !_showOpinionLog; // Toggle visibility
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            // Area for interaction log (overlapping NMM log)
            float ourLogRectX = originalContentArea.x + 1f;
            float ourLogRectY = originalContentArea.y + 365f;
            float ourLogRectWidth = originalContentArea.width - 2f; 
            float ourLogRectHeight = OurTotalHeight - ourLogRectY - 1f;

            Rect ourLogRect = new Rect(ourLogRectX, ourLogRectY, ourLogRectWidth, ourLogRectHeight);


            if (_showOpinionLog)
            {
                // ИСПРАВЛЕНИЕ: Вызываем метод с правильным количеством аргументов (3)
                DrawOpinionLog(ourLogRect, pawnTab, isObserverMode);
            }
            // else: NMM log will be drawn as usual in DrawNMMCard
            // Widgets.EndGroup() for opinionsContentArea was already called above.
        }

        // Add a scroll vector for the observed pawns list (now used for FloatMenu)
        private Vector2 _scrollPosition = Vector2.zero;

        // Method to get a filtered list of available pawns
        private List<Pawn> GetFilteredAvailablePawns(Pawn pawnTab, bool isObserverMode, NudityMattersMore.TabGender nmmCurrentTabGender)
        {
            List<Pawn> pawns = new List<Pawn>();
            foreach (var kvp in PawnInteractionManager.InteractionProfiles)
            {
                string key = kvp.Key;
                string[] ids = key.Split('-');
                if (ids.Length == 2 && int.TryParse(ids[0], out int id1) && int.TryParse(ids[1], out int id2))
                {
                    Pawn p1 = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == id1) ?? Find.CurrentMap?.mapPawns?.AllPawns.FirstOrDefault(p => p.thingIDNumber == id1);
                    Pawn p2 = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == id2) ?? Find.CurrentMap?.mapPawns?.AllPawns.FirstOrDefault(p => p.thingIDNumber == id2);

                    if (p1 == null || p2 == null) continue;

                    Pawn pawnToConsider = isObserverMode ? p2 : p1;
                    if (pawnToConsider == pawnTab) continue;

                    if (NudityMattersMore.InfoHelper.GetTabGender(pawnToConsider) != nmmCurrentTabGender) continue;

                    switch (_currentFilter)
                    {
                        case PawnFilterType.MapColonists:
                            if (pawnToConsider.IsColonist && pawnToConsider.Map == Find.CurrentMap)
                            {
                                pawns.Add(pawnToConsider);
                            }
                            break;
                        case PawnFilterType.GlobalColonists:
                            if (pawnToConsider.IsColonist)
                            {
                                pawns.Add(pawnToConsider);
                            }
                            break;
                        case PawnFilterType.AllRegistered:
                            pawns.Add(pawnToConsider);
                            break;
                    }
                }
            }
            return pawns.Distinct().OrderBy(p => p.LabelCap).ToList();
        }

        // ADDED: Method to render our own opinion log
        private void DrawOpinionLog(Rect rect, Pawn pawnTab, bool isObserverMode)
        {
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Tiny;

            // Определяем, чей лог показывать
            Pawn pawnWhoseLogWeShow = pawnTab;
            List<OpinionLogEntry> entriesToShow = new List<OpinionLogEntry>();
            PawnSituationalOpinionMemory memory = SituationalOpinionHelper.GetOrCreatePawnSituationalOpinionMemory(pawnWhoseLogWeShow);

            if (pawnTab == _selectedTargetPawn) // Наблюдение за собой
            {
                entriesToShow = memory.GetRecentLogEntries().Where(e => !e.IsObserverPerspective || e.IsSelfOpinion).ToList();
            }
            else // Наблюдение за другим
            {
                if (isObserverMode) // pawnTab наблюдает за _selectedTargetPawn
                {
                    entriesToShow = memory.GetRecentLogEntries().Where(e => e.IsObserverPerspective && e.ObservedPawn == _selectedTargetPawn).ToList();
                }
                else // _selectedTargetPawn наблюдает за pawnTab
                {
                    entriesToShow = memory.GetRecentLogEntries().Where(e => !e.IsObserverPerspective && e.ObserverPawn == _selectedTargetPawn).ToList();
                }
            }

            if (!entriesToShow.Any())
            {
                Rect noEntriesRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(noEntriesRect, "No situational opinions recorded yet.");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                return;
            }

            // --- Начало исправления скролл-бара ---

            // 1. Динамически вычисляем высоту всего контента
            float totalContentHeight = 0f;
            float viewWidth = rect.width - 16f; // Ширина видимой области скролла
            foreach (var entry in entriesToShow)
            {
                // Добавляем высоту текста
                totalContentHeight += Text.CalcHeight(entry.GetFormattedLogString(), viewWidth);
                // Добавляем высоту отступа
                totalContentHeight += 4f; // Увеличили отступ для лучшей читаемости
            }

            // Если контента меньше, чем высота окна, делаем высоту равной окну, чтобы избежать пустого места
            if (totalContentHeight < rect.height)
            {
                totalContentHeight = rect.height;
            }

            // 2. Используем вычисленную высоту в Rect для контента
            Rect contentRect = new Rect(0, 0, viewWidth, totalContentHeight);

            // --- Конец исправления скролл-бара ---

            Widgets.BeginScrollView(rect, ref _opinionLogScrollPosition, contentRect);

            Listing_Standard logListing = new Listing_Standard();
            // Используем тот же Rect, что и для ScrollView, но с началом в (0,0)
            logListing.Begin(new Rect(0, 0, contentRect.width, contentRect.height));

            foreach (var entry in entriesToShow)
            {
                logListing.Label((TaggedString)entry.GetFormattedLogString(), -1f, (string)entry.OpinionText);
                logListing.Gap(4f); // Увеличенный отступ
            }

            logListing.End();
            Widgets.EndScrollView();

            Text.Font = GameFont.Small;
        }
    }
}
