using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public void LoadLevel1()
    {
        //Load the Level 1 scene (Assessment 3's game level)
        Debug.Log("Loading Level 1 Scene...");
        // NOTE: The scene name here must exactly match the name you saved your Assessment 3 level as.
        // It also must be added to the Build Settings (File -> Build Settings).
        SceneManager.LoadScene("Level1Scene"); 
    }

    public void LoadLevel2()
    {
        // Purpose: Load the Design Innovation scene (Assessment 4's Level 2)
        Debug.Log("Loading Level 2 Scene...");
        // NOTE: We will create "InnovationScene" (or Level2Scene) later.
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
