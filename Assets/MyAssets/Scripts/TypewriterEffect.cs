using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class TypewriterEffect : MonoBehaviour
{
    public float delay = 0.1f;

    public IEnumerator ShowText(string text, TMP_Text tmpText)
    {
        for (int i = 0; i <= text.Length; i++)
        {
            string displayText = text.Substring(0, i);
            tmpText.text = displayText;
            // Update text display
            yield return new WaitForSeconds(0.1f); // Wait for a while before continuing
        }
    }
}