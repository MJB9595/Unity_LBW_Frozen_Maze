using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class WinLossController : MonoBehaviour
{
    public static WinLossController Instance;

    public GameObject winUI;
    public GameObject lossUI;
    private bool isGameOver = false;

    private void Awake()
    {
        Instance = this;
        if (winUI) winUI.SetActive(false);
        if (lossUI) lossUI.SetActive(false);
    }

    public void Win()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("Escape Success!");
        if (winUI) winUI.SetActive(true);
        Time.timeScale = 0; // Pause game
    }

    public void Loss()
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log("Caught by Boss!");
        if (lossUI) lossUI.SetActive(true);
        Time.timeScale = 0;
    }

    public void Restart()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
