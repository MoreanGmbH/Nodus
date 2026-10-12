using Friflo.Engine.ECS;
using UnityEngine.UIElements;

namespace Deges.UI {
    [ComponentKey("Deges.UI.FeedbackSubmission")]
    public struct FeedbackSubmission : IComponent {
        public string Message;
    }

    [ComponentKey(null)]
    public struct BoundUIRoot : IComponent {
        public VisualElement Root;
        public bool LocaleBound;
    }
}
