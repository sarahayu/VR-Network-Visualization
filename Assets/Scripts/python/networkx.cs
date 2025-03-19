using UnityEditor;
using UnityEditor.Scripting.Python;

public class MenuItem_networkx_Class
{
   [MenuItem("Python Scripts/networkx")]
   public static void networkx()
   {
       PythonRunner.RunFile("Assets/Scripts/python/networkx.py");
       }
};
