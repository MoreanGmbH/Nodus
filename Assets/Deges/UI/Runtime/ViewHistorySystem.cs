using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.Navigation;
using Nodus.UI;

namespace Deges.UI {
    public sealed class ViewHistorySystem : QuerySystem<ViewState> {
        protected override void OnUpdate() {
            foreach (var entity in Query.Entities.ToEntityList()) {
                if (entity.IsNull || !entity.Parent.IsNull) continue;

                var view = entity.GetComponent<ViewState>();
                if (view.ViewId == "FeedbackPanel" || view.ViewId == "MapPanel") {
                    if (entity.HasComponent<Tracked>()) entity.RemoveComponent<Tracked>();
                    continue;
                }

                if (!entity.HasComponent<Tracked>()) entity.AddComponent(new Tracked());
            }
        }
    }
}
