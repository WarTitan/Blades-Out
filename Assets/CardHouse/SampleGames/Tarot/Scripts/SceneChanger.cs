using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardHouse
{
    public class SceneChanger : MonoBehaviour
    {
        public string SceneToSpawn;
        public void Activate()
        {
            SceneManager.LoadScene(SceneToSpawn);
        }
    }
}