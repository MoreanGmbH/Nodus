using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.ECS;
using Nodus.Navigation;
using Nodus.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Deges.UI {
    [SystemPhase(SystemPhase.Update)]
    public sealed class FeedbackInputSystem : QuerySystem<RootReference> {
        protected override void OnUpdate() {
            if (!LocalizationSettings.InitializationOperation.IsDone)
                return;

            foreach (var entity in Query.Entities.ToEntityList()) {
                var reference = entity.GetComponent<RootReference>();
                if (reference.Root == null)
                    continue;

                var isBound = entity.TryGetComponent<BoundUIRoot>(out var bound)
                    && ReferenceEquals(bound.Root, reference.Root);

                if (!isBound) {
                    Bind(reference.Root, reference.Configuration, Query.Store);
                    entity.AddComponent(new BoundUIRoot { Root = reference.Root, LocaleBound = true });
                }

                SyncSelections(reference.Root, Query.Store);
            }
        }

        static string Localize(StringTable table, string key, string fallback) {
            var entry = table?.GetEntry(key);
            return entry != null && !string.IsNullOrWhiteSpace(entry.LocalizedValue)
                ? entry.LocalizedValue
                : fallback;
        }

        static void RefreshDropdown(DropdownField dropdown) {
            // A formatter is not necessarily reevaluated when the locale changes
            // while the underlying selected value stays the same.
            var selected = dropdown.value;
            dropdown.SetValueWithoutNotify(null);
            dropdown.SetValueWithoutNotify(selected);
        }

        static void Bind(VisualElement root, UIConfiguration configuration, EntityStore store) {
            if (configuration == null)
                throw new InvalidOperationException("Assign UIConfiguration on the UI component.");

            var theme = root.Q<DropdownField>("ThemeDropdown")
                ?? throw new InvalidOperationException("Missing ThemeDropdown in Infrastructure.uxml.");
            var language = root.Q<DropdownField>("LanguageDropdown")
                ?? throw new InvalidOperationException("Missing LanguageDropdown in Infrastructure.uxml.");

            var themeNames = new List<string>();
            var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in configuration.Themes) {
                if (profile == null)
                    throw new InvalidOperationException("UIConfiguration contains a missing ThemeProfile.");
                if (!uniqueNames.Add(profile.name))
                    throw new InvalidOperationException($"Duplicate theme asset name: {profile.name}");
                themeNames.Add(profile.name);
            }
            if (themeNames.Count == 0)
                throw new InvalidOperationException("Add ThemeProfiles to UIConfiguration > Themes.");

            var locales = LocalizationSettings.AvailableLocales.Locales;
            var languageCodes = new List<string>();
            var localesByCode = new Dictionary<string, Locale>(StringComparer.OrdinalIgnoreCase);
            foreach (var locale in locales) {
                var code = locale.Identifier.Code;
                if (!localesByCode.TryAdd(code, locale))
                    throw new InvalidOperationException($"Duplicate locale code: {code}");
                languageCodes.Add(code);
            }
            if (languageCodes.Count == 0)
                throw new InvalidOperationException("Add locales in Project Settings > Localization.");

            // Cached StringTable is populated asynchronously; works in WebGL too.
            StringTable stringTable = null;

            theme.choices = themeNames;
            theme.formatSelectedValueCallback = name =>
                string.IsNullOrEmpty(name) ? string.Empty : Localize(stringTable, $"Theme.{name}", name);
            theme.formatListItemCallback = theme.formatSelectedValueCallback;
            theme.SetValueWithoutNotify(themeNames[0]);
            theme.RegisterValueChangedCallback(evt =>
                store.CreateEntity(new SetTheme { ThemeName = evt.newValue }));

            language.choices = languageCodes;
            language.formatSelectedValueCallback = code => {
                if (string.IsNullOrEmpty(code))
                    return string.Empty;
                if (!localesByCode.TryGetValue(code, out var locale))
                    return code;
                var fallback = locale.LocaleName;
                var suffix = fallback.LastIndexOf(" (", StringComparison.Ordinal);
                if (suffix >= 0)
                    fallback = fallback.Substring(0, suffix);
                return Localize(stringTable, $"Locale.{code}", fallback);
            };
            language.formatListItemCallback = language.formatSelectedValueCallback;
            language.SetValueWithoutNotify(LocalizationSettings.SelectedLocale.Identifier.Code);
            language.RegisterValueChangedCallback(evt => {
                if (!localesByCode.TryGetValue(evt.newValue, out var locale))
                    throw new InvalidOperationException($"Unknown locale: {evt.newValue}");

                // Apply to Unity directly. Also emit ECS request for LocaleState consumers.
                LocalizationSettings.SelectedLocale = locale;
                store.CreateEntity(new SetLocale { Code = evt.newValue });
            });

            void LoadStrings(Locale _) {
                var requestedCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
                var request = LocalizationSettings.StringDatabase.GetTableAsync("MainTable");
                request.Completed += operation => {
                    if (requestedCode != LocalizationSettings.SelectedLocale?.Identifier.Code)
                        return;
                    stringTable = operation.Result;
                    RefreshDropdown(theme);
                    RefreshDropdown(language);
                };
            }

            LocalizationSettings.SelectedLocaleChanged += LoadStrings;
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
                LocalizationSettings.SelectedLocaleChanged -= LoadStrings);
            LoadStrings(LocalizationSettings.SelectedLocale);
            root.RegisterCallback<ClickEvent>(evt => OnClick(evt, root, store));
        }

        static void SyncSelections(VisualElement root, EntityStore store) {
            var theme = root.Q<DropdownField>("ThemeDropdown");
            foreach (var entity in store.Query<ThemeState>().Entities) {
                var name = entity.GetComponent<ThemeState>().ThemeName;
                if (theme.choices.Contains(name) && theme.value != name)
                    theme.SetValueWithoutNotify(name);
                break;
            }

            var language = root.Q<DropdownField>("LanguageDropdown");
            var code = LocalizationSettings.SelectedLocale?.Identifier.Code;
            if (code != null && language.value != code)
                language.SetValueWithoutNotify(code);
        }

        static void OnClick(ClickEvent evt, VisualElement root, EntityStore store) {
            if (evt.target is not VisualElement target)
                return;
            var button = target as Button ?? target.GetFirstAncestorOfType<Button>();
            if (button == null)
                return;

            switch (button.name) {
                case "OpenFeedbackButton":
                    Navigation.CreateRequest(new PushView { StackId = "Main", ViewId = "FeedbackView" }, "Open feedback");
                    break;
                case "FeedbackCloseButton":
                    Navigation.CreateRequest(new PopView { StackId = "Main" }, "Close feedback");
                    break;
                case "FeedbackSubmitButton":
                    Submit(root, store);
                    break;
                case "BackButton":
                    Navigation.Back();
                    break;
                case "ForwardButton":
                    Navigation.Forward();
                    break;
            }
        }

        static void Submit(VisualElement root, EntityStore store) {
            var field = root.Q<TextField>("FeedbackMessage")
                ?? throw new InvalidOperationException("Missing FeedbackMessage field.");
            if (string.IsNullOrWhiteSpace(field.value)) {
                field.Focus();
                return;
            }

            store.CreateEntity(new FeedbackSubmission { Message = field.value });
            Navigation.CreateRequest(new PopView { StackId = "Main" }, "Submit feedback");
        }
    }
}
