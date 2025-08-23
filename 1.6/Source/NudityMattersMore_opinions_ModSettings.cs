using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using NudityMattersMore; // Required for InteractionType

namespace NudityMattersMore_opinions
{
    // Class for storing all mod settings
    public class NudityMattersMore_opinions_ModSettings : ModSettings
    {
        // Opinion Generator Settings
        public bool enableSituationalOpinionGenerator = true;
        public float chanceOfGeneratedOpinion = 50f;

        // Setting to enable/disable comments from our patch
        public bool enableFixationLogComments = true;

        // НОВЫЕ НАСТРОЙКИ
        public float commentaryCooldownSeconds = 60f;
        public int maxSimultaneousOpinions = 2;
        public bool allowCommentOnSameState = false;

        // Setting to enable/disable debug logs
        public bool enableDebugLogging = false;

        // Dictionary to store the toggle state for each interaction commentary.
        public Dictionary<string, bool> enabledInteractionToggles;

        // A list of interactions that should be disabled by default and shown separately.
        public static readonly HashSet<InteractionType> SensitiveInteractions = new HashSet<InteractionType>
        {
            InteractionType.Sex,
            InteractionType.Rape,
            InteractionType.Raped,
            InteractionType.Masturbation
        };

        // Method for saving/loading settings
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableSituationalOpinionGenerator, "enableSituationalOpinionGenerator", true);
            Scribe_Values.Look(ref chanceOfGeneratedOpinion, "chanceOfGeneratedOpinion", 50f);
            Scribe_Values.Look(ref enableFixationLogComments, "enableFixationLogComments", true);
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);

            // Сохранение и загрузка новых настроек
            Scribe_Values.Look(ref commentaryCooldownSeconds, "commentaryCooldownSeconds", 60f);
            Scribe_Values.Look(ref maxSimultaneousOpinions, "maxSimultaneousOpinions", 2);
            Scribe_Values.Look(ref allowCommentOnSameState, "allowCommentOnSameState", false);

            // Save and load the dictionary of toggles.
            Scribe_Collections.Look(ref enabledInteractionToggles, "enabledInteractionToggles", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && enabledInteractionToggles == null)
            {
                InitializeInteractionToggles();
            }
        }

        public void InitializeInteractionToggles()
        {
            enabledInteractionToggles = new Dictionary<string, bool>();
            foreach (InteractionType type in Enum.GetValues(typeof(InteractionType)))
            {
                enabledInteractionToggles[type.ToString()] = !SensitiveInteractions.Contains(type);
            }
        }

        public bool IsInteractionEnabled(InteractionType type)
        {
            if (enabledInteractionToggles == null)
            {
                InitializeInteractionToggles();
            }
            return enabledInteractionToggles.TryGetValue(type.ToString(), out bool enabled) && enabled;
        }

        // Method for resetting settings
        public void Reset()
        {
            enableSituationalOpinionGenerator = true;
            chanceOfGeneratedOpinion = 50f;
            enableFixationLogComments = true;
            enableDebugLogging = false;

            // Сброс новых настроек
            commentaryCooldownSeconds = 60f;
            maxSimultaneousOpinions = 2;
            allowCommentOnSameState = false;

            InitializeInteractionToggles();
        }
    }

    // The main class of the mod, which will contain the settings and the settings window
    public class NudityMattersMore_opinions_Mod : Mod
    {
        public static NudityMattersMore_opinions_ModSettings settings;

        private enum SettingsTab { General, Interactions }
        private SettingsTab _tab = SettingsTab.General;
        private Vector2 _scrollPosition = Vector2.zero;

        public NudityMattersMore_opinions_Mod(ModContentPack content) : base(content)
        {
            settings = GetSettings<NudityMattersMore_opinions_ModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("General", () => _tab = SettingsTab.General, _tab == SettingsTab.General),
                new TabRecord("Interactions", () => _tab = SettingsTab.Interactions, _tab == SettingsTab.Interactions)
            };

            float tabWidth = 120f;
            float totalTabsWidth = tabWidth * tabs.Count;
            Rect tabsRect = new Rect(inRect.x + inRect.width - totalTabsWidth, inRect.y, totalTabsWidth, 32f);
            TabDrawer.DrawTabs(tabsRect, tabs);

            Rect contentRect = new Rect(inRect);
            contentRect.yMin = inRect.y + 32f;

            switch (_tab)
            {
                case SettingsTab.General:
                    DrawGeneralSettings(contentRect);
                    break;
                case SettingsTab.Interactions:
                    DrawInteractionsSettings(contentRect);
                    break;
            }
        }

        private void DrawGeneralSettings(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // --- Секция генератора мнений ---
            listing.Label("NMM Opinions Generator Settings");
            listing.Gap(4f);
            listing.CheckboxLabeled("Use situational opinion generator", ref settings.enableSituationalOpinionGenerator, "If disabled, only predefined opinions will be displayed. Default true.");
            Rect chanceGeneratedRect = listing.GetRect(30f);
            settings.chanceOfGeneratedOpinion = Widgets.HorizontalSlider(chanceGeneratedRect, settings.chanceOfGeneratedOpinion, 0f, 100f, true, $"Chance of generated opinion: {settings.chanceOfGeneratedOpinion:F0}%", "0%", "100%", 1f);
            listing.Gap(12f);

            listing.GapLine();
            listing.Gap(12f);

            // --- Секция комментариев ---
            Text.Font = GameFont.Medium;
            listing.Label("Fixation Log Commentary Settings");
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            bool speakUpActive = ModLister.GetActiveModWithIdentifier("JPT.speakup") != null;
            if (speakUpActive)
            {
                listing.CheckboxLabeled("Enable pawn commentary on observed actions", ref settings.enableFixationLogComments, "Enables pawns to comment on what they see. Requires the 'SpeakUp' mod. Default: true.");

                // Добавляем новые настройки сюда, если SpeakUp активен
                if (settings.enableFixationLogComments)
                {
                    listing.Gap(12f);

                    // Слайдер кулдауна
                    Rect cooldownRect = listing.GetRect(30f);
                    settings.commentaryCooldownSeconds = Widgets.HorizontalSlider(cooldownRect, settings.commentaryCooldownSeconds, 0f, 300f, true, $"Commentary Cooldown: {settings.commentaryCooldownSeconds:F0} sec", "0s", "300s (5 min)", 1f);

                    // Слайдер макс. мнений
                    Rect maxOpinionsRect = listing.GetRect(30f);
                    settings.maxSimultaneousOpinions = (int)Widgets.HorizontalSlider(maxOpinionsRect, settings.maxSimultaneousOpinions, 1f, 10f, true, $"Max Simultaneous Opinions: {settings.maxSimultaneousOpinions}", "1", "10", 1f);

                    // Чекбокс для комментирования в том же состоянии
                    listing.CheckboxLabeled("Allow comment on pawn in same state", ref settings.allowCommentOnSameState, "If enabled, a naked pawn can comment on another naked pawn, etc. Default: false.");
                }
            }
            else
            {
                GUI.contentColor = Color.gray;
                listing.Label("Enable pawn commentary on observed actions ('SpeakUp' mod not found, feature disabled)");
                GUI.contentColor = Color.white;
            }

            listing.Gap(24f);
            listing.GapLine();
            listing.Gap(12f);

            // --- Секция отладки ---
            Text.Font = GameFont.Medium;
            listing.Label("Debugging");
            Text.Font = GameFont.Small;
            listing.Gap(8f);
            listing.CheckboxLabeled("Enable debug logging", ref settings.enableDebugLogging, "Shows detailed logs in the console for debugging purposes. It is recommended to keep this disabled during normal play. Default: false.");

            listing.Gap(24f);
            listing.GapLine();

            if (listing.ButtonText("Reset all settings to default"))
            {
                settings.Reset();
            }

            listing.End();
        }

        private void DrawInteractionsSettings(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            if (settings.enabledInteractionToggles == null) settings.InitializeInteractionToggles();

            listing.Gap(12f);
            Text.Font = GameFont.Medium;
            listing.Label("Problematic Interactions");
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            Text.Font = GameFont.Tiny;
            GUI.color = Color.yellow;
            listing.Label("If these interactions enabled, descriptions from other mods may look incorrect.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            foreach (InteractionType type in NudityMattersMore_opinions_ModSettings.SensitiveInteractions)
            {
                string key = type.ToString();
                bool value = settings.enabledInteractionToggles[key];
                listing.CheckboxLabeled(key, ref value, $"Enable/disable commentary for the '{key}' interaction.");
                settings.enabledInteractionToggles[key] = value;
            }

            listing.Gap(24f);
            listing.GapLine();
            listing.Gap(12f);
            Text.Font = GameFont.Medium;
            listing.Label("Other Commentaries");
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            float scrollViewHeight = inRect.height - listing.CurHeight - 10f;
            Rect scrollViewOuterRect = listing.GetRect(scrollViewHeight);

            var otherToggles = settings.enabledInteractionToggles.Keys
                .Where(k => !NudityMattersMore_opinions_ModSettings.SensitiveInteractions.Contains((InteractionType)Enum.Parse(typeof(InteractionType), k)))
                .OrderBy(k => k)
                .ToList();

            Rect scrollViewInnerRect = new Rect(0, 0, scrollViewOuterRect.width - 16f, otherToggles.Count * 28f);

            Widgets.BeginScrollView(scrollViewOuterRect, ref _scrollPosition, scrollViewInnerRect);
            Listing_Standard scrollListing = new Listing_Standard();
            scrollListing.Begin(scrollViewInnerRect);

            foreach (string key in otherToggles)
            {
                bool value = settings.enabledInteractionToggles[key];
                scrollListing.CheckboxLabeled(key, ref value, $"Enable/disable commentary for the '{key}' interaction.");
                settings.enabledInteractionToggles[key] = value;
            }

            scrollListing.End();
            Widgets.EndScrollView();

            listing.End();
        }

        public override string SettingsCategory()
        {
            return "Nudity Matters More: Opinions";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
