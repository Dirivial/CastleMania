using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class GetHighScore : MonoBehaviour
{
    public UITextElement highScoreText;
    // Start is called before the first frame update
    void Start()
    {
       highScoreText.SetElementText(PlayerPrefs.GetInt("HighScore", 0).ToString()); 
    }
}
