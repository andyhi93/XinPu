using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TimeManager))]
public class TimeManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 繪製原本 Inspector 的內容
        DrawDefaultInspector();

        TimeManager timeManager = (TimeManager)target;

        GUILayout.Space(10);
        
        // 加入一個按鈕
        if (GUILayout.Button("Advance 1 Hour", GUILayout.Height(30)))
        {
            timeManager.AdvanceOneHour();
        }

        if (GUILayout.Button("Advance 2 Hours (One Branch)"))
        {
            timeManager.AdvanceTime();
        }
    }
}
