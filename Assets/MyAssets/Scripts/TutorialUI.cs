using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    private Image image;

    [SerializeField] Sprite[] sprites;
    private int index;

    [SerializeField] GameObject tutorialUI;

    private void Start()
    {
        image = GetComponent<Image>();
    }

    public void ChangeSprite()
    {
        index++; // Add 1 to the subscript of the sprite array
        if (index >= sprites.Length) // If you have reached the end of the sprite array
        {
            index = 0; // Reset the subscript to 0
            tutorialUI.SetActive(false);
        }

        image.sprite = sprites[index]; // Set the source image of the image to the sprite corresponding to the subscript in the sprite array
    }
}