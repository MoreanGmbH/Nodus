using System.Collections;
using Nodus.Maps;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deges.Maps {
    public sealed class MapStartup : MonoBehaviour {
        IEnumerator Start() {
            yield return null;
            if (SceneManager.GetActiveScene().name == "StartScene") MapScene.Open();
        }
    }
}
