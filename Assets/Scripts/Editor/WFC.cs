using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(WFC))]
public class WFCEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of our inspector UI
        VisualElement myInspector = new VisualElement();

        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Editor/WFC.uxml");
        visualTree.CloneTree(myInspector);


        myInspector.Add(new Label() { text = "Object to use for importing tiles" });
        myInspector.Add(new ObjectField() { bindingPath = "XML_IO" });

        myInspector.Add(new Button(() => { ((WFC)target).GenerateFull(); }) { text = "Generate Full", tooltip = "Generate a full set of tiles" });
        myInspector.Add(new Button(() => { ((WFC)target).ToggleStepping(); }) { text = "Toggle Stepping", tooltip = "This is used to continously go add tiles." });
        myInspector.Add(new Button(() => { ((WFC)target).Clear(); }) { text = "Clear", tooltip = "Clear the instantiated tiles" });

        // Return the finished inspector UI
        return myInspector;
    }
}
