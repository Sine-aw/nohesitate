using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SignPost : MonoBehaviour
{
    [Header("UI 설정")]
    [SerializeField] private GameObject promptUIPanel;
    [SerializeField] private GameObject hintUIPanel;
    [SerializeField] private float interactDistance = 3f;

    private Transform playerTransform;
    private bool isPlayerNearby = false;
    private bool isHintOpen = false;

    private void Start()
    {
        if (promptUIPanel) promptUIPanel.SetActive(false);
        if (hintUIPanel) hintUIPanel.SetActive(false);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance <= interactDistance)
        {
            if (!isPlayerNearby)
            {
                isPlayerNearby = true;
                if (promptUIPanel && !isHintOpen) promptUIPanel.SetActive(true);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ToggleHintUI();
            }
        }
        else
        {
            if (isPlayerNearby)
            {
                isPlayerNearby = false;
                if (promptUIPanel) promptUIPanel.SetActive(false);

                if (isHintOpen)
                {
                    CloseHintUI();
                }
            }
        }

        Keyboard currentKeyboard = Keyboard.current;
        if (currentKeyboard != null && currentKeyboard.escapeKey.wasPressedThisFrame && isHintOpen)
        {
            CloseHintUI();
        }
    }

    private void ToggleHintUI()
    {
        isHintOpen = !isHintOpen;

        if (hintUIPanel) hintUIPanel.SetActive(isHintOpen);
        if (promptUIPanel) promptUIPanel.SetActive(!isHintOpen);

        if (isHintOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void CloseHintUI()
    {
        isHintOpen = false;
        if (hintUIPanel) hintUIPanel.SetActive(false);
        if (promptUIPanel && isPlayerNearby) promptUIPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}