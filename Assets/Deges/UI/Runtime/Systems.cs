using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.ECS;
using Nodus.Navigation;
using Nodus.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Deges.UI {
    [SystemPhase(SystemPhase.Update)]
    public sealed class FeedbackInputSystem : QuerySystem<RootReference> {
        protected override void OnUpdate() {
            foreach (var entity in Query.Entities.ToEntityList()) {
                var reference = entity.GetComponent<RootReference>();
                if (reference.Root == null)
                    continue;

                var isBound = entity.TryGetComponent<BoundUIRoot>(out var bound)
                    && ReferenceEquals(bound.Root, reference.Root);

                if (!isBound) {
                    BindThemeAndButtons(reference.Root, reference.Configuration, Query.Store);
                    bound = new BoundUIRoot { Root = reference.Root };
                    entity.AddComponent(bound);
                }

                if (!bound.LocaleBound && LocalizationSettings.InitializationOperation.IsDone) {
                    BindLanguage(reference.Root, Query.Store);
                    bound.LocaleBound = true;
                    entity.AddComponent(bound);
                }

                SyncSelections(reference.Root, Query.Store, bound.LocaleBound);
            }
        }

        static void BindThemeAndButtons(VisualElement root, UIConfiguration configuration, EntityStore store) {
            if (configuration == null)
                throw new InvalidOperationException("Assign UIConfiguration on the UI component.");

            var dropdown = root.Q<DropdownField>("ThemeDropdown")
                ?? throw new InvalidOperationException("Missing ThemeDropdown in Infrastructure.uxml.");

            var ids = new List<string>();
            foreach (var profile in configuration.Themes) {
                if (profile == null)
                    throw new InvalidOperationException("UIConfiguration contains a missing ThemeProfile.");
                ids.Add(profile.Id);
            }
            if (ids.Count == 0)
                throw new InvalidOperationException("Add ThemeProfiles to UIConfiguration > Themes.");

            dropdown.choices = ids;
            dropdown.formatSelectedValueCallback = id => configuration.FindTheme(id).DisplayName;
            dropdown.formatListItemCallback = id => configuration.FindTheme(id).DisplayName;
            dropdown.SetValueWithoutNotify(ids[0]);
            dropdown.RegisterValueChangedCallback(evt => store.CreateEntity(new SetTheme { ThemeId = evt.newValue }));
            root.RegisterCallback<ClickEvent>(evt => OnClick(evt, root, store));
        }

        static void BindLanguage(VisualElement root, EntityStore store) {
            var dropdown = root.Q<DropdownField>("LanguageDropdown")
                ?? throw new InvalidOperationException("Missing LanguageDropdown in Infrastructure.uxml.");

            var locales = LocalizationSettings.AvailableLocales.Locales;
            if (locales.Count == 0)
                throw new InvalidOperationException("Add locales in Project Settings > Localization.");

            var choices = new List<string>();
            foreach (var locale in locales)
                choices.Add(locale.LocaleName);

            dropdown.choices = choices;
            dropdown.formatSelectedValueCallback = ShortLocaleName;
            dropdown.formatListItemCallback = ShortLocaleName;
            dropdown.SetValueWithoutNotify(LocalizationSettings.SelectedLocale.LocaleName);
            dropdown.RegisterValueChangedCallback(evt => {
                foreach (var locale in LocalizationSettings.AvailableLocales.Locales) {
                    if (locale.LocaleName != evt.newValue)
                        continue;
                    store.CreateEntity(new SetLocale { Code = locale.Identifier.Code });
                    return;
                }
                throw new InvalidOperationException($"Unknown locale: {evt.newValue}");
            });
        }

        static string ShortLocaleName(string value) {
            var suffix = value.LastIndexOf(" (", StringComparison.Ordinal);
            return suffix >= 0 ? value.Substring(0, suffix) : value;
        }

        static void SyncSelections(VisualElement root, EntityStore store, bool localeBound) {
            var theme = root.Q<DropdownField>("ThemeDropdown");
            foreach (var entity in store.Query<ThemeState>().Entities) {
                var id = entity.GetComponent<ThemeState>().ThemeId;
                if (theme.value != id)
                    theme.SetValueWithoutNotify(id);
                break;
            }

            if (!localeBound)
                return;

            var language = root.Q<DropdownField>("LanguageDropdown");
            var current = LocalizationSettings.SelectedLocale.LocaleName;
            if (language.value != current)
                language.SetValueWithoutNotify(current);
        }

        static void OnClick(ClickEvent evt, VisualElement root, EntityStore store) {
            if (evt.target is not VisualElement target)
                return;
            var button = target as Button ?? target.GetFirstAncestorOfType<Button>();
            if (button == null)
                return;

            switch (button.name) {
                case "OpenFeedbackButton":
                    Navigation.CreateRequest(new PushView { StackId = "Main", ViewId = "Feedback" }, "Open feedback");
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
