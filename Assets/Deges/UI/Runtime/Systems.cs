using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.ECS;
using Nodus.UI;
using UnityEngine.UIElements;

namespace Deges.UI {
    [SystemPhase(SystemPhase.Update)]
    public sealed class FeedbackInputSystem : QuerySystem<RootReference> {
        protected override void OnUpdate() {
            foreach (var entity in Query.Entities.ToEntityList()) {
                var reference = entity.GetComponent<RootReference>();
                if (reference.Root == null) continue;

                var isBound = entity.TryGetComponent<BoundUIRoot>(out var bound)
                    && ReferenceEquals(bound.Root, reference.Root);
                if (isBound) continue;

                Bind(reference.Root, Query.Store);
                entity.AddComponent(new BoundUIRoot { Root = reference.Root });
            }
        }

        static void Bind(VisualElement root, EntityStore store) {
            root.RegisterCallback<ClickEvent>(evt => {
                if (evt.target is not VisualElement target) return;
                if (target.name == "FeedbackBackdrop") {
                    store.CreateEntity(new PopView { StackId = "Main" });
                    return;
                }
                var button = target as Button ?? target.GetFirstAncestorOfType<Button>();
                if (button?.name == "FeedbackSubmitButton") Submit(root, store);
            });
        }

        static void Submit(VisualElement root, EntityStore store) {
            var field = root.Q<TextField>("FeedbackMessage")
                ?? throw new System.InvalidOperationException("Missing FeedbackMessage field.");
            if (string.IsNullOrWhiteSpace(field.value)) {
                field.Focus();
                return;
            }
            store.CreateEntity(new FeedbackSubmission { Message = field.value });
            store.CreateEntity(new PopView { StackId = "Main" });
        }
    }
}
