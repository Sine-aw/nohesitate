using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerHeartUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private List<Image> heartImages;

    [Header("하트 스프라이트")]
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite halfHeart;
    [SerializeField] private Sprite emptyHeart;

    private void Start()
    {
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (playerHealth == null || heartImages == null || heartImages.Count == 0) return;

        float currentHealth = playerHealth.CurrentHealth;

        float totalPoints = currentHealth / 5f;

        for (int i = 0; i < heartImages.Count; i++)
        {
            float heartThreshold = i * 2;

            if (totalPoints >= heartThreshold + 2)
            {
                heartImages[i].sprite = fullHeart;
                heartImages[i].gameObject.SetActive(true);
            }
            else if (totalPoints >= heartThreshold + 1)
            {
                heartImages[i].sprite = halfHeart;
                heartImages[i].gameObject.SetActive(true);
            }
            else
            {
                if (emptyHeart != null)
                {
                    heartImages[i].sprite = emptyHeart;
                    heartImages[i].gameObject.SetActive(true);
                }
                else
                {
                    heartImages[i].gameObject.SetActive(false);
                }
            }
        }
    }
}