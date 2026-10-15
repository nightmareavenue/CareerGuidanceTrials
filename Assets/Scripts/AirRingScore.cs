using UnityEngine;

public class AirRingScore : MonoBehaviour
{
    int points;
    GUIStyle scoreStyle;

    public void AddPoint()
    {
        points++;
    }

    void OnGUI()
    {
        if (scoreStyle == null)
        {
            scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            scoreStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(20, 20, 190, 52), GUIContent.none);
        GUI.Label(new Rect(34, 25, 170, 42), "RINGS: " + points, scoreStyle);
    }
}