using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.Navigation;
using Nodus.UI;
using NUnit.Framework;

namespace Deges.UI.Tests {
    public sealed class FeedbackHistoryTests {
        [Test]
        public void PushBackForwardRestoresFeedback() {
            var store = new EntityStore();
            var systems = new SystemRoot(store) {
                new PushViewSystem(),
                new PopViewSystem(),
                new FeedbackHistoryTrackingSystem(),
                new HistorySystem()
            };

            var request = store.CreateEntity(new Request { Action = ActionType.Commit });
            request.AddAction(new PushView { StackId = "Main", ViewId = "Feedback" });
            systems.Update(default);
            Assert.AreEqual(1, CountLiveViews(store));

            store.Navigate(ActionType.Back);
            systems.Update(default);
            Assert.AreEqual(0, CountLiveViews(store));

            store.Navigate(ActionType.Forward);
            systems.Update(default);
            Assert.AreEqual(1, CountLiveViews(store));
        }

        static int CountLiveViews(EntityStore store) {
            int count = 0;
            foreach (var entity in store.Query<ViewState>().Entities)
                if (entity.Parent.IsNull)
                    count++;
            return count;
        }
    }
}
