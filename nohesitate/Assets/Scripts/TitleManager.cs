using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    public void OnClickStart()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void OnClickExit()
    {
        Debug.Log("게임 종료 버튼 클릭됨");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}