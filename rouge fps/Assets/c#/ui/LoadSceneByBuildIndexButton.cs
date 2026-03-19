using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneByBuildIndexButton : MonoBehaviour
{
    [SerializeField] private int sceneBuildIndex = 0;

    public void LoadConfiguredScene()
    {
        LoadScene(sceneBuildIndex);
    }

    public void LoadScene(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning($"Invalid scene build index: {buildIndex}", this);
            return;
        }

        SceneManager.LoadScene(buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
