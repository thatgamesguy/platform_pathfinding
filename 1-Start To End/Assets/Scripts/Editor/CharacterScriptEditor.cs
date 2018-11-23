using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Character))]
public class CharacterScriptEditor : Editor {

    public override void OnInspectorGUI() {
        Character myCharScript = (Character)target;
        //if (GUILayout.Button("Update Total Jump")) {
        //    myCharScript.jump.UpdateTotalJumps();
        //}
        DrawDefaultInspector();
        //GUI.changed = false;
        //myCharScript.jump.maxJumps1 =
        // myCharScript.jump.jumpCount1 = EditorGUILayout.IntField("Min Jump Height", myCharScript.minJumpHeight);
        if (GUI.changed && Application.isPlaying) {
            myCharScript.jump.UpdateJumpHeight();
        }
        //EditorGUILayout.LabelField("Test", myCharScript.Level.ToString());
    }
}