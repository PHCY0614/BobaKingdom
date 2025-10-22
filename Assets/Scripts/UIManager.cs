using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public void LoadLevel1()
    {
        SceneManager.LoadScene("Level1Scene"); 
    }

    public void LoadLevel2()
    {
        SceneManager.LoadScene("InnovationScene"); 
    }

    public void QuitGame()
    {
        // Purpose: Exit the game (useful for the main menu, though optional)
        Debug.Log("Quitting Game...");
        Application.Quit();
        // NOTE: Application.Quit() only works in a built game (not in Unity Editor).
    }
}
