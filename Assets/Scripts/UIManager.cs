using System;
using TMPro;
using UnityEngine;

[Serializable]
public struct UITextElement
{
    public string label;
    public TextMeshProUGUI element;

    public void SetElementText(string text)
    {
        element.text = label + " " + text;
    }
}

public class UIManager : MonoBehaviour
{

    public UITextElement HealthText;
    public UITextElement ScoreText;
    public UITextElement TimeToWaveText;
    public UITextElement EnemiesLeftText;

    public void Start()
    {
        HealthText.SetElementText("100");
        ScoreText.SetElementText("0");
        TimeToWaveText.SetElementText("5");
        EnemiesLeftText.SetElementText("0");
    }


    public void SetHealthText(float newHealth)
    {
        HealthText.SetElementText(newHealth.ToString());
    }


    public void SetScoreText(float newScore)
    {
        ScoreText.SetElementText(newScore.ToString());
    }


    public void SetTimeToWaveText(int newTimeLeft)
    {
        TimeToWaveText.SetElementText(newTimeLeft.ToString());
    }


    public void SetEnemiesLeftText(int newEnemiesLeft)
    {
        EnemiesLeftText.SetElementText(newEnemiesLeft.ToString());
    }
}