#if UNITY_EDITOR
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.ECS;
using Nodus.Navigation;
using Nodus.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ComponentAsset = Nodus.ECS.Definitions.Component;
using EntityAsset = Nodus.ECS.Definitions.Entity;
using Entities = Nodus.ECS.Definitions.Entities;

namespace Deges.UI.Tests {
    public sealed class FeedbackHistoryTests {
        EntityStore store;
        SystemRoot systems;
        Entities open, close, back, forward;

        static T LoadByGuid<T>(string guid) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        [SetUp]
        public void Setup() {
            store = new EntityStore();
            EcsRegistry.RegisterStore("Main", store);
            EcsRegistry.RegisterStore("Navigation", store);
            EcsRegistry.RegisterComponent<Request>();
            EcsRegistry.RegisterComponent<PushView>();
            EcsRegistry.RegisterComponent<PopView>();
            EcsRegistry.RegisterComponent<CommandStatus>();
            systems = new SystemRoot(store) {
                new PushViewSystem(), new PopViewSystem(),
                new ViewHistorySystem(), new HistorySystem()
            };
            open = LoadByGuid<Entities>("a32178f14b225f07a28ca5162196f29e");
            close = LoadByGuid<Entities>("ed5515929495513b8e8f56dccebe0511");
            back = LoadByGuid<Entities>("0fa860d76b7059d3917929ff3df0a55e");
            forward = LoadByGuid<Entities>("efee0c4d38115538b57e1da7ca2eda93");
            Assert.NotNull(open); Assert.NotNull(close); Assert.NotNull(back); Assert.NotNull(forward);
        }

        [TearDown]
        public void TearDown() {
            EcsRegistry.UnregisterStore("Navigation", store);
            EcsRegistry.UnregisterStore("Main", store);
        }

        [Test]
        public void JsonAndScriptableObjectProduceSameEntities() {
            string json = open.ToJson();
            Assert.AreEqual(json, EcsJson.Serialize(EcsJson.Parse(json), true));
            Assert.AreEqual(open.ToData().Entities.Count,
                EcsUrls.Parse(EcsUrls.Embedded("https://nodus.example/", open.ToData())).Entities.Count);
        }

        [Test]
        public void SharedComponentChangesAppearInEveryGeneratedEntity() {
            var shared = ScriptableObject.CreateInstance<ComponentAsset>();
            var first = ScriptableObject.CreateInstance<EntityAsset>();
            var second = ScriptableObject.CreateInstance<EntityAsset>();
            var definitions = ScriptableObject.CreateInstance<Entities>();
            try {
                shared.Key = "Nodus.UI.PushView";
                shared.Json = "{\"StackId\":\"Main\",\"ViewId\":\"FeedbackPanel\"}";
                first.Components.Add(shared);
                second.Components.Add(shared);
                definitions.Store = "Main";
                definitions.Roots.Add(first);
                definitions.Roots.Add(second);
                Assert.AreEqual("FeedbackPanel", definitions.ToData().Entities[0].Components[0].Data["ViewId"].ToString());
                shared.Json = "{\"StackId\":\"Main\",\"ViewId\":\"OtherPanel\"}";
                var data = definitions.ToData();
                Assert.AreEqual("OtherPanel", data.Entities[0].Components[0].Data["ViewId"].ToString());
                Assert.AreEqual("OtherPanel", data.Entities[1].Components[0].Data["ViewId"].ToString());
                Assert.DoesNotThrow(() => EcsJson.Parse(definitions.ToJson()));
            } finally {
                UnityEngine.Object.DestroyImmediate(definitions);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(shared);
            }
        }

        [Test]
        public void FeedbackDoesNotCreateHistory() {
            var history = store.GetHistory();
            EcsRuntime.Spawn(open); systems.Update(default);
            Assert.AreEqual(1, CountViews());
            foreach (var view in store.Query<ViewState>().Entities)
                Assert.IsFalse(view.HasComponent<Tracked>(), "Feedback must not be tracked.");
            Assert.AreEqual(1, history.ChildCount);

            EcsRuntime.Spawn(close); systems.Update(default);
            Assert.AreEqual(0, CountViews());
            Assert.AreEqual(1, history.ChildCount);

            EcsRuntime.Spawn(back); systems.Update(default);
            EcsRuntime.Spawn(forward); systems.Update(default);
            Assert.AreEqual(0, CountViews());
            Assert.AreEqual(1, history.ChildCount);
        }

        [Test]
        public void OtherViewsRemainNavigable() {
            var history = store.GetHistory();
            store.CreateEntity(new ViewState { StackId = "Main", ViewId = "OtherPanel", InstanceId = "TestView", Order = 0 });
            store.CreateEntity(new Request { Action = ActionType.Commit, Label = "Other view" });
            systems.Update(default);
            Assert.AreEqual(2, history.ChildCount);

            EcsRuntime.Spawn(back); systems.Update(default);
            Assert.AreEqual(0, CountViews());
            EcsRuntime.Spawn(forward); systems.Update(default);
            Assert.AreEqual(1, CountViews());
        }

        [Test]
        public void UxmlControlsReferenceSharedActions() {
            var view = LoadByGuid<VisualTreeAsset>("35bf7ee41de8b264587e46da591bec5e");
            Assert.NotNull(view);
            var tree = view.Instantiate();
            Assert.AreSame(open, tree.Q<EntitiesButton>("OpenFeedbackButton").Entities);
            Assert.AreSame(back, tree.Q<EntitiesButton>("BackButton").Entities);
            Assert.AreSame(forward, tree.Q<EntitiesButton>("ForwardButton").Entities);

            var locale = tree.Q<EntitiesDropdown>("LanguageDropdown");
            var theme = tree.Q<EntitiesDropdown>("ThemeDropdown");
            Assert.NotNull(locale);
            Assert.NotNull(theme);
            Assert.NotNull(locale.Options);
            Assert.NotNull(theme.Options);
            Assert.AreEqual(DropdownSelection.Locale, locale.Options.Selection);
            Assert.AreEqual(DropdownSelection.Theme, theme.Options.Selection);
            Assert.AreEqual(2, locale.Options.Items.Count);
            Assert.AreEqual(2, theme.Options.Items.Count);
            foreach (var choice in locale.Options.Items) Assert.NotNull(choice.Action);
            foreach (var choice in theme.Options.Items) Assert.NotNull(choice.Action);
        }

        [Test]
        public void DropdownActionsDoNotChangeHistory() {
            EcsRegistry.RegisterComponent<SetLocale>();
            EcsRegistry.RegisterComponent<SetTheme>();
            var locale = LoadByGuid<DropdownOptions>("ffd7b35c14404a7985d7f31e85dfce94");
            var theme = LoadByGuid<DropdownOptions>("48a8b501084b42e189ec919a9bea0edb");
            Assert.NotNull(locale);
            Assert.NotNull(theme);
            var history = store.GetHistory();
            var handlers = new SystemRoot(store) { new SetLocaleSystem(), new SetThemeSystem() };
            foreach (var item in locale.Items) {
                EcsRuntime.Spawn(item.Action);
                handlers.Update(default);
            }
            foreach (var item in theme.Items) {
                EcsRuntime.Spawn(item.Action);
                handlers.Update(default);
            }
            Assert.AreEqual(1, history.ChildCount);
            foreach (var state in store.Query<LocaleState>().Entities)
                Assert.IsFalse(state.HasComponent<Tracked>());
            foreach (var state in store.Query<ThemeState>().Entities)
                Assert.IsFalse(state.HasComponent<Tracked>());
        }

        int CountViews() {
            int count = 0;
            foreach (var entity in store.Query<ViewState>().Entities)
                if (entity.Parent.IsNull) count++;
            return count;
        }
    }
}
#endif
