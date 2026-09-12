using UnityEngine;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject mainGameUI;
    [SerializeField] private GameObject connectMenu;
    [SerializeField] private GameObject winMenu;
    [SerializeField] private GameObject loseMenu;
    [SerializeField] private GameObject drawMenu;
    [SerializeField] private GameObject quitOnlyMenu;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowConnectMenu();
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void ShowConnectMenu()
    {
        SetActivePanels(connect: true, game: false, win: false, lose: false, draw: false, quitOnly: false);
    }

    public void ShowGameUI()
    {
        SetActivePanels(connect: false, game: true, win: false, lose: false, draw: false, quitOnly: false);
    }

    public void ShowWinMenu()
    {
        SetActivePanels(connect: false, game: true, win: true, lose: false, draw: false, quitOnly: false);
    }

    public void ShowLoseMenu()
    {
        SetActivePanels(connect: false, game: true, win: false, lose: true, draw: false, quitOnly: false);
    }

    public void ShowDrawMenu()
    {
        SetActivePanels(connect: false, game: true, win: false, lose: false, draw: true, quitOnly: false);
    }

    public void ShowQuitOnlyMenu()
    {
        SetActivePanels(connect: false, game: true, win: false, lose: false, draw: false, quitOnly: true);
    }

    private void SetActivePanels(bool connect, bool game, bool win, bool lose, bool draw, bool quitOnly)
    {
        if (connectMenu != null)
        {
            connectMenu.SetActive(connect);
        }

        if (mainGameUI != null)
        {
            mainGameUI.SetActive(game);
        }

        if (winMenu != null)
        {
            winMenu.SetActive(win);
        }

        if (loseMenu != null)
        {
            loseMenu.SetActive(lose);
        }

        if (drawMenu != null)
        {
            drawMenu.SetActive(draw);
        }

        if (quitOnlyMenu != null)
        {
            quitOnlyMenu.SetActive(quitOnly);
        }
    }
}
