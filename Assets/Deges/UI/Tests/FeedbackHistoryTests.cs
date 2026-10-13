#if UNITY_EDITOR
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.ECS;
using Nodus.Navigation;
using Nodus.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;
using ComponentAsset = Nodus.ECS.Definitions.Component;
using EntityAsset = Nodus.ECS.Definitions.Entity;

using Entities = Nodus.ECS.Definitions.Entities;

namespace Deges.UI.Tests {
    public sealed class FeedbackHistoryTests {
        EntityStore store;
        SystemRoot systems;
        Entities open, close, back, forward;

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
            open = AssetDatabase.LoadAssetAtPath<Entities>("Assets/Deges/UI/Actions/OpenFeedback.asset");
            close = AssetDatabase.LoadAssetAtPath<Entities>("Assets/Deges/UI/Actions/CloseFeedback.asset");
            back = AssetDatabase.LoadAssetAtPath<Entities>("Packages/com.nodus.navigation/Runtime/Actions/Back.asset");
            forward = AssetDatabase.LoadAssetAtPath<Entities>("Packages/com.nodus.navigation/Runtime/Actions/Forward.asset");
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
            Assert.AreEqual(open.ToData().Entities.Count, EcsUrls.Parse(EcsUrls.Embedded("https://nodus.example/", open.ToData())).Entities.Count);
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
        public void UxmlButtonsReferenceSharedActions() {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Deges/UI/Runtime/Elements/Infrastructure/InfrastructurePanel.uxml");
            Assert.NotNull(asset);
            var tree = asset.Instantiate();
            var openButton = tree.Q<EntitiesButton>("OpenFeedbackButton");
            Assert.NotNull(openButton);
            Assert.AreSame(open, openButton.Entities);
            Assert.AreSame(back, tree.Q<EntitiesButton>("BackButton").Entities);
            Assert.AreSame(forward, tree.Q<EntitiesButton>("ForwardButton").Entities);
        }

        [Test]
        public void AllScenariosComposeIndependentScenarios() {
            var all = AssetDatabase.LoadAssetAtPath<ScenarioContainer>("Assets/Deges/UI/Tests/Scenarios/AllScenarios.asset");
            var feedback = AssetDatabase.LoadAssetAtPath<Scenario>("Assets/Deges/UI/Tests/Scenarios/FeedbackFlow.asset");
            Assert.NotNull(all);
            Assert.NotNull(feedback);
            var feedbackSteps = ScenarioRunner.Plan(feedback);
            Assert.AreEqual(2, feedbackSteps.Count);
            Assert.AreSame(open, feedbackSteps[0].Entities);
            Assert.AreSame(close, feedbackSteps[1].Entities);
            Assert.AreEqual(12, feedbackSteps[0].WaitFrames);
            Assert.IsTrue(feedbackSteps[0].CaptureScreenshot);

            var steps = ScenarioRunner.Plan(all);
            Assert.AreEqual(6, steps.Count);
            Assert.AreEqual("SetGerman", steps[2].Entities.name);
            Assert.AreEqual("SetEnglish", steps[3].Entities.name);
            Assert.AreEqual("SetLightTheme", steps[4].Entities.name);
            Assert.AreEqual("SetDarkTheme", steps[5].Entities.name);
        }

        [Test]
        public void LocaleThemePresetsDoNotChangeHistory() {
            EcsRegistry.RegisterComponent<SetLocale>();
            EcsRegistry.RegisterComponent<SetTheme>();
            var all = AssetDatabase.LoadAssetAtPath<ScenarioContainer>("Assets/Deges/UI/Tests/Scenarios/AllScenarios.asset");
            var steps = ScenarioRunner.Plan(all);
            var initialHistory = store.GetHistory().ChildCount;
            var systems = new SystemRoot(store) { new SetLocaleSystem(), new SetThemeSystem() };
            for (int i = 2; i < steps.Count; i++) {
                EcsRuntime.Spawn(steps[i].Entities);
                systems.Update(default);
                foreach (var e in store.Query<ThemeState>().Entities)
                    Assert.IsFalse(e.HasComponent<Tracked>());
                foreach (var e in store.Query<LocaleState>().Entities)
                    Assert.IsFalse(e.HasComponent<Tracked>());
            }
            Assert.AreEqual(initialHistory, store.GetHistory().ChildCount);
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
