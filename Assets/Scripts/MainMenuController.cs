using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Play按钮点击时调用
    public void OnPlayButtonClicked()
    {
        Debug.Log("Play按钮被点击");
        SceneManager.LoadScene("Gameplay"); // "Gameplay" 替换为你的目标场景名
    }

    // Exit按钮点击时调用
    public void OnExitButtonClick()
    {
        Debug.Log("退出游戏");
        Application.Quit();
    }
} 