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
                    entity.AddComponent(new BoundUIRoot { Root = reference.Root });
                }

                SyncSelections(reference.Root, Query.Store);
            }
        }

        static string Localize(StringTable table, string key, string fallback) {
            var entry = table?.GetEntry(key);
            return entry != null && !string.IsNullOrWhiteSpace(entry.LocalizedValue)
                ? entry.LocalizedValue : fallback;
        }

        static void RefreshDropdown(DropdownField dropdown) {
            var selected = dropdown.value;
            dropdown.SetValueWithoutNotify(null);
            dropdown.SetValueWithoutNotify(selected);
        }

        // Fit the selected localized label (plus padding and the icon).
        static void FitDropdown(DropdownField dropdown) {
            dropdown.schedule.Execute(() => {
                var label = dropdown.Q<TextElement>(className: "unity-base-popup-field__text");
                if (label == null)
                    return;

                var text = dropdown.formatSelectedValueCallback?.Invoke(dropdown.value) ?? dropdown.value;
                if (string.IsNullOrEmpty(text))
                    return;

                var size = label.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined);
                dropdown.style.width = (float)Math.Ceiling(size.x) + 54;
            });
        }

        // The selected control fits its label; the open menu fits its longest option.
        static void FitPopup(DropdownField dropdown) {
            dropdown.schedule.Execute(() => {
                if (dropdown.panel == null)
                    return;

                var popup = dropdown.panel.visualTree.Q<VisualElement>(className: GenericDropdownMenu.containerOuterUssClassName);
                var label = dropdown.Q<TextElement>(className: "unity-base-popup-field__text");
                if (popup == null || label == null)
                    return;

                float width = dropdown.resolvedStyle.width;
                foreach (var choice in dropdown.choices) {
                    var text = dropdown.formatListItemCallback?.Invoke(choice) ?? choice;
                    var size = label.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined,
                        0, VisualElement.MeasureMode.Undefined);
                    width = Math.Max(width, size.x + 48);
                }

                width = (float)Math.Ceiling(width);
                popup.style.width = width;
                popup.style.minWidth = width;

                // Keep the wider popup inside the panel near the right edge.
                var panelWidth = dropdown.panel.visualTree.resolvedStyle.width;
                if (popup.resolvedStyle.left + width > panelWidth)
                    popup.style.left = Math.Max(0, panelWidth - width);
            });
        }

        static void Bind(VisualElement root, UIConfiguration configuration, EntityStore store) {
            if (!configuration)
                throw new InvalidOperationException("Assign UIConfiguration on the UI component.");

            var theme = root.Q<DropdownField>("ThemeDropdown")
                ?? throw new InvalidOperationException("Missing ThemeDropdown in InfrastructurePanel.uxml.");
            var language = root.Q<DropdownField>("LanguageDropdown")
                ?? throw new InvalidOperationException("Missing LanguageDropdown in InfrastructurePanel.uxml.");

            var themeNames = new List<string>();
            foreach (var profile in configuration.Themes) {
                if (!profile)
                    throw new InvalidOperationException("Missing theme in UIConfiguration.");
                themeNames.Add(profile.name);
            }
            if (themeNames.Count == 0)
                throw new InvalidOperationException("UIConfiguration requires at least one theme.");

            var codes = new List<string>();
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                codes.Add(locale.Identifier.Code);
            if (codes.Count == 0)
                throw new InvalidOperationException("Add locales in Project Settings > Localization.");

            StringTable stringTable = null;

            theme.choices = themeNames;
            theme.formatSelectedValueCallback = name =>
                string.IsNullOrEmpty(name) ? string.Empty : Localize(stringTable, name, name);
            theme.formatListItemCallback = theme.formatSelectedValueCallback;
            theme.SetValueWithoutNotify(themeNames[0]);
            theme.RegisterValueChangedCallback(evt => {
                store.CreateEntity(new SetTheme { ThemeName = evt.newValue });
                FitDropdown(theme);
            });
            theme.RegisterCallback<PointerDownEvent>(_ => FitPopup(theme));

            language.choices = codes;
            language.formatSelectedValueCallback = code => {
                if (string.IsNullOrEmpty(code))
                    return string.Empty;
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                var fallback = locale != null ? locale.LocaleName : code;
                var suffix = fallback.LastIndexOf(" (", StringComparison.Ordinal);
                if (suffix >= 0)
                    fallback = fallback.Substring(0, suffix);
                return Localize(stringTable, $"Locale.{code}", fallback);
            };
            language.formatListItemCallback = language.formatSelectedValueCallback;
            language.SetValueWithoutNotify(LocalizationSettings.SelectedLocale.Identifier.Code);
            language.RegisterValueChangedCallback(evt => {
                var locale = LocalizationSettings.AvailableLocales.GetLocale(evt.newValue);
                if (locale == null)
                    throw new InvalidOperationException($"Unknown locale: {evt.newValue}");

                LocalizationSettings.SelectedLocale = locale;
                store.CreateEntity(new SetLocale { Code = evt.newValue });
            });
            language.RegisterCallback<PointerDownEvent>(_ => FitPopup(language));

            void LoadStrings(Locale _) {
                var requestedCode = LocalizationSettings.SelectedLocale?.Identifier.Code;
                var request = LocalizationSettings.StringDatabase.GetTableAsync("MainTable");
                request.Completed += operation => {
                    if (requestedCode != LocalizationSettings.SelectedLocale?.Identifier.Code)
                        return;
                    stringTable = operation.Result;
                    RefreshDropdown(theme);
                    RefreshDropdown(language);
                    FitDropdown(theme);
                    FitDropdown(language);
                };
            }

            LocalizationSettings.SelectedLocaleChanged += LoadStrings;
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
                LocalizationSettings.SelectedLocaleChanged -= LoadStrings);
            LoadStrings(LocalizationSettings.SelectedLocale);
            FitDropdown(theme);
            FitDropdown(language);
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
                    Navigation.CreateRequest(new PushView { StackId = "Main", ViewId = "FeedbackPanel" }, "Open feedback");
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
