using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Nodus.Navigation;
using Nodus.UI;

namespace Deges.UI {
    // Composition boundary between UI and Navigation packages: no package cycle.
    public sealed class ViewHistorySystem : QuerySystem<ViewState> {
        protected override void OnUpdate() {
            foreach (var entity in Query.Entities.ToEntityList()) {
                if (!entity.IsNull && entity.Parent.IsNull && !entity.HasComponent<Tracked>())
                    entity.AddComponent(new Tracked());
            }
        }
    }
}
