using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ARBoardManager))]
public class ARBoardManagerGizmo : Editor
{
    private void OnSceneGUI()
    {
        ARBoardManager board = (ARBoardManager)target;

        DrawLaneAnchors(board.player1Lanes, Color.blue);
        DrawLaneAnchors(board.player2Lanes, Color.red);

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 200, 60));
        GUILayout.Label("Lane Anchors", EditorStyles.boldLabel);
        GUILayout.Label("Azul = Player 1  |  Rojo = Player 2");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private void DrawLaneAnchors(Transform[] lanes, Color color)
    {
        if (lanes == null) return;

        for (int i = 0; i < lanes.Length; i++)
        {
            if (lanes[i] == null) continue;

            Vector3 pos = lanes[i].position;
            float size = HandleUtility.GetHandleSize(pos) * 0.08f;

            Handles.color = color;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);

            Handles.color = Color.white;
            Handles.DrawSolidDisc(pos + Vector3.up * 0.001f, Vector3.up, size * 0.6f);

            Handles.color = color;
            Handles.Label(pos + Vector3.up * size * 1.5f + Vector3.right * size,
                lanes[i].name.Length > 0 ? lanes[i].name : $"Lane {i + 1}");
        }
    }
}
