using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSelector : MonoBehaviour
{
    [Header("场景设置")]
    [SerializeField] private string gameSceneName = "Gameplay"; // 游戏场景名称
    
    [Header("难度关卡设置")]
    [SerializeField] private int easyStartLevel = 1;
    [SerializeField] private int mediumStartLevel = 1;
    [SerializeField] private int hardStartLevel = 1;
    
    /// <summary>
    /// 选择Easy难度并跳转到游戏场景
    /// </summary>
    public void SelectEasyDifficulty()
    {
        SetDifficultyAndLevel(0, easyStartLevel); // 0 = Easy
        LoadGameScene();
    }
    
    /// <summary>
    /// 选择Medium难度并跳转到游戏场景
    /// </summary>
    public void SelectMediumDifficulty()
    {
        SetDifficultyAndLevel(1, mediumStartLevel); // 1 = Medium
        LoadGameScene();
    }
    
    /// <summary>
    /// 选择Hard难度并跳转到游戏场景
    /// </summary>
    public void SelectHardDifficulty()
    {
        SetDifficultyAndLevel(2, hardStartLevel); // 2 = Hard
        LoadGameScene();
    }
    
    /// <summary>
    /// 设置难度和关卡信息到PlayerPrefs
    /// </summary>
    /// <param name="difficulty">难度（0=Easy, 1=Medium, 2=Hard）</param>
    /// <param name="startLevel">起始关卡</param>
    private void SetDifficultyAndLevel(int difficulty, int startLevel)
    {
        PlayerPrefs.SetInt("SelectedDifficulty", difficulty);
        PlayerPrefs.SetInt("StartLevel", startLevel);
        PlayerPrefs.Save();
        
        string difficultyName = difficulty == 0 ? "Easy" : (difficulty == 1 ? "Medium" : "Hard");
        Debug.Log($"选择难度: {difficultyName}, 起始关卡: {startLevel}");
    }
    
    /// <summary>
    /// 加载游戏场景
    /// </summary>
    private void LoadGameScene()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogError("游戏场景名称未设置！");
        }
    }
    
    /// <summary>
    /// 退出游戏（可选功能）
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("退出游戏");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
