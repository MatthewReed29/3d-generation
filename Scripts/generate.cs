using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine.Experimental.AI;
using System;
using UnityEditor;
using UnityEngine.UIElements;
using Unity.VisualScripting;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using TMPro;
using Unity.AI.Navigation;
using System.IO;
using System.Globalization;
using UnityEngine.Rendering;
using System.Linq;
using System.Runtime.InteropServices;

#if UNITY_EDITOR

[CustomEditor(typeof(generate)), CanEditMultipleObjects]
public class generation_controls : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        //base.OnInspectorGUI();
        if(GUILayout.Button("Generate"))
        {
            generate script = (generate)target;
            script.startGeneration();
        }

        if(GUILayout.Button("Save"))
        {
            generate script = (generate)target;
            script.saveMeshes();
        }

        if(GUILayout.Button("Load"))
        {
            generate script = (generate)target;
            script.loadMeshes(script.loadID);
        }
    }
}

#endif

public class generate : MonoBehaviour
{
    public float threshold;
    public Vector3Int range;
    public Vector3 stretch;
    public int object_scale;
    //float[,,] weights;
    bool done = false;
    Thread t;
    public int seed;
    int[,] triTable = new int[256, 16] //this is the marching cubes look up table
{{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
{3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
{3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
{3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
{9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
{9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
{2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
{8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
{9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
{4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
{3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
{1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
{4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
{4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
{9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
{5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
{2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
{9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
{0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
{2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
{10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
{4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
{5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
{5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
{9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
{0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
{1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
{10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
{8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
{2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
{7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
{9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
{2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
{11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
{9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
{5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
{11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
{11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
{1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
{9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
{5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
{2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
{0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
{5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
{6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
{3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
{6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
{5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
{1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
{10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
{6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
{8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
{7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
{3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
{5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
{0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
{9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
{8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
{5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
{0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
{6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
{10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
{10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
{8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
{1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
{3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
{0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
{10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
{3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
{6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
{9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
{8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
{3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
{6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
{0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
{10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
{10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
{2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
{7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
{7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
{2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
{1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
{11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
{8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
{0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
{7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
{10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
{2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
{6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
{7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
{2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
{1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
{10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
{10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
{0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
{7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
{6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
{8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
{9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
{6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
{4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
{10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
{8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
{0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
{1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
{8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
{10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
{4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
{10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
{5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
{11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
{9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
{6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
{7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
{3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
{7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
{9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
{3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
{6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
{9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
{1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
{4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
{7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
{6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
{3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
{0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
{6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
{0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
{11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
{6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
{5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
{9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
{1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
{1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
{10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
{0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
{5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
{10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
{11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
{9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
{7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
{2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
{8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
{9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
{9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
{1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
{9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
{9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
{5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
{0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
{10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
{2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
{0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
{0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
{9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
{5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
{3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
{5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
{8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
{0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
{9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
{0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
{1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
{3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
{4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
{9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
{11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
{11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
{2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
{9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
{3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
{1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
{4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
{4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
{0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
{3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
{3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
{0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
{9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
{1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}};
    int3[] cubeCorners =
    {
        new int3(0, 0, 0),
        new int3(1, 0, 0),
        new int3(1, 0, 1),
        new int3(0, 0, 1),
        new int3(0, 1, 0),
        new int3(1, 1, 0),
        new int3(1, 1, 1),
        new int3(0, 1, 1)
    };

    Vector3[] edgeLookUp = new Vector3[12]
    {
        new Vector3(0.5f,0,0),
        new Vector3(1,0,0.5f),
        new Vector3(0.5f,0,1),
        new Vector3(0,0,0.5f),
        new Vector3(0.5f,1,0),
        new Vector3(1,1,0.5f),
        new Vector3(0.5f,1,1),
        new Vector3(0,1,0.5f),
        new Vector3(0,0.5f,0),
        new Vector3(1,0.5f,0),
        new Vector3(1,0.5f,1),
        new Vector3(0,0.5f,1),
    };

    Mutex frameMutex = new Mutex();
    List<mesh_data> frames = new List<mesh_data>();
    [SerializeField]
    int chunk_volume;
    public float start_magnitude;
    public float start_frequency;
    public float ground_weight_top;
    public float ground_weight_centre;
    public float ground_weight_strength;
    public float ground_weight_variation;
    public int octaves;
    public int warping_itterations;
    public float warp_factor;
    public Material mat;
    private int meshesMade = 0;
    private int meshesDisplayed = 0;
    public bool navigate;
    Vector2[] layer_rotation;
    float[] layer_offset;
    public float gradient_limit;
    public float gradient_power;
    public float perlin_range;
    long time_sum = 0;
    float offset_variation_x;
    float offset_variation_z;

    bool keepRunning = true;

    public int loadID;

    float timeStart = -1f;

    List<KeyValuePair<Tuple<float, float>, float>> debugFlattness = new List<KeyValuePair<Tuple<float, float>, float>>();

    List<GameObject> objects = new List<GameObject>();
    List<Mesh> current_meshes = new List<Mesh>();

    // Start is called before the first frame update
    [DllImport("NoiseDLL")]
    private static extern float alternativeNoise(float inx, float iny, float inz, ref float outx, ref float outy, ref float outz); //DLL function replacing betterNoise
    [DllImport("NoiseDLL")]
    private static extern void setInfo(float[] layer_rotation_x, float[] layer_rotation_y, int length, float gradLimit, float thresh, float startMagnitude, float startFrequency, float gradPower); //DLL function taking the generation data into memory for the c++ file
    [DllImport("NoiseDLL")]
    private static extern float octaveNoise2(float inputx, float inputy, float inputz, float top_stretchx, float top_stretchy, float top_stretchz, int octaves, float start); //DLL function replacing octaveNoise
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (meshesMade > meshesDisplayed) //displays the meshes to the world after they have been finished off thread
        {
            for (int i = meshesDisplayed; i < meshesMade; i++)
            {
                Mesh meh = new Mesh();
                meh.vertices = frames[i].vertices;
                meh.triangles = frames[i].triangles;

                meh.RecalculateNormals();
                GameObject g = new GameObject("Too many polys", new Type[] { typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(paintGround) });
                g.GetComponent<MeshFilter>().mesh = meh;
                /*Texture2D texture = new Texture2D(100, 100);
                Color[] pixels = new Color[100 * 100];
                for (int x = 0; x < 100; x++)
                {
                    for (int y = 0; y < 100; y++)
                    {
                        float v = Mathf.PerlinNoise(x /20f, y/20f);
                        pixels[100 * y + x] = new Color(v,v,v,v);
                    }
                }
                texture.SetPixels(0,0,100,100, pixels);
                texture.Apply();
                mat.mainTexture = texture;*/
                g.GetComponent<MeshRenderer>().material = mat;
                g.GetComponent<MeshCollider>().sharedMesh = meh;
                //Material dynamicMat = new Material(mat.shader);



                g.transform.SetParent(transform);
                current_meshes.Add(meh);
                objects.Add(g);
                Debug.Log(Time.time - timeStart);
            }
            meshesDisplayed = meshesMade;
        }
        if (done)
        {
            done = false;
            if (navigate)
            {
                //GetComponent<NavMeshSurface>().UpdateNavMesh(GetComponent<NavMeshSurface>().navMeshData);
                //GetComponent<NavMeshSurface>().AddData();
                GetComponent<NavMeshSurface>().BuildNavMesh();
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            startGeneration();
        }
    }


    Color[] generateColors(Mesh mesh)
    {
        Color[] colours = new Color[mesh.vertices.Length]; //framework for a function I may develop, where vertices are coloured via some algorithm to make the terrain less flat looking

        return colours;
    }

    Vector3 stringToVertices(string input)
    {
        Vector3 to_return = Vector3.zero;
        float[] asArray = new float[3];
        int pointer = 0;
        string handel1 = "";
        for(int i = 1; i < input.Length; i++)
        {
            if (input[i] == ',')
            {
                asArray[pointer] = float.Parse(handel1, CultureInfo.InvariantCulture.NumberFormat);
                handel1 = "";
                pointer++;
            }else if (input[i] == ')')
            {
                asArray[pointer] = float.Parse(handel1, CultureInfo.InvariantCulture.NumberFormat);
                break;
            }
            else
            {
                handel1 += input[i];
            }
        }
        for(int i = 0; i < asArray.Length; i++)
        {
            to_return = new Vector3(asArray[0], asArray[1], asArray[2]);
        }
        return to_return;
    }

    static int to_int(string s)
    {
        Char[] chars = s.ToCharArray();
        int result = 0;
        int negative;
        String[] numbers = new String[10];
        if (s.Length == 0)
        {
            return 0;
        }
        for (int i = 0; i < 10; i++)
        {
            numbers[i] = (i.ToString());
        }
        if (chars[0] == '-')
        {
            negative = 1;
        }
        else
        {
            negative = 0;
        }
        for (int c = negative; c < chars.Length; c++)
        {
            bool valid_num = false;
            for (int n = 0; n < numbers.Length; n++)
            {
                if (chars[c].ToString() == numbers[n])
                {
                    if (negative == 0)
                    {
                        result += (int)(Mathf.Pow(10f, (float)(chars.Length - c - 1)) * n);
                    }
                    else
                    {
                        result -= (int)(Mathf.Pow(10f, (float)(chars.Length - c - 1)) * n);
                    }

                    valid_num = true;
                }
            }
            if (!valid_num)
            {
                return 0;
            }
        }
        return result;
    }

    public void saveMeshes()
    {
        string num_name = UnityEngine.Random.Range(0, 100000).ToString();
        using (FileStream fileWriter = File.Open("save" + num_name + ".custom", FileMode.Create))
        {
            (new BinaryWriter(fileWriter)).Write(current_meshes.Count);
            Mutex streamsMutex = new Mutex();
            Vector3[][] vertexArrayArray = new Vector3[current_meshes.Count * 3][];
            int[][] triangleArrayArray = new int[current_meshes.Count][];
            for (int m = 0; m < current_meshes.Count; m++)
            {
                vertexArrayArray[m] = current_meshes[m].vertices;
                triangleArrayArray[m] = current_meshes[m].triangles;
            }
            Parallel.For(0, current_meshes.Count, (m) => {
            //for (int m = 0; m < current_meshes.Count; m++)
            //{
                MemoryStream stream = new MemoryStream(sizeof(int) * 2 + vertexArrayArray[m].Length * sizeof(float) * 3 + triangleArrayArray[m].Length * sizeof(int));
                BinaryWriter binWriter = new BinaryWriter(stream);
                binWriter.Write(vertexArrayArray[m].Length);
                binWriter.Write(triangleArrayArray[m].Length);
                for (int v = 0; v < vertexArrayArray[m].Length; v++)
                {
                    binWriter.Write(vertexArrayArray[m][v].x);
                    binWriter.Write(vertexArrayArray[m][v].y);
                    binWriter.Write(vertexArrayArray[m][v].z);
                }
                for (int i = 0; i < triangleArrayArray[m].Length; i++)
                {
                    binWriter.Write(triangleArrayArray[m][i]);
                }
                byte[] array = stream.ToArray();
                streamsMutex.WaitOne(60000);
                fileWriter.Write(array);
                streamsMutex.ReleaseMutex();
                Debug.Log(m);
            });
            fileWriter.Close();
        }
    }

    public void loadMeshes(int id)    //uses the id that is taken from the editor to load from the files and place as meshes
    {
        for(int i = 0; i < objects.Count; i++)
        {
            Destroy(objects[i]); 
        }
        current_meshes.Clear();
        objects.Clear();
        using (BinaryReader binReader = new BinaryReader(File.Open("save" + id.ToString() + ".custom", FileMode.Open))) 
        {
            int meshCount = binReader.ReadInt32();
            Mesh[] meshes = new Mesh[meshCount];
            for(int m = 0; m < meshCount; m++)
            {
                meshes[m] = new Mesh();
                int vertexCount = binReader.ReadInt32();
                Vector3[] vertices = new Vector3[vertexCount];
                int indexCount = binReader.ReadInt32();
                int[] indices = new int[indexCount];


                for (int v = 0; v < vertexCount; v++)
                {
                    float x = binReader.ReadSingle();
                    float y = binReader.ReadSingle();
                    float z = binReader.ReadSingle();
                    vertices[v] = new Vector3(x, y, z);
                }
                for (int i = 0; i < indexCount; i++)
                {
                    int index = binReader.ReadInt32();
                    indices[i] = index;
                }
                meshes[m].vertices = vertices;
                meshes[m].triangles = indices;
            }
            for (int i = 0; i < meshCount; i++)
            {
                meshes[i].RecalculateNormals();
                GameObject g = new GameObject("Too many polys", new Type[] { typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(paintGround) });
                g.GetComponent<MeshFilter>().mesh = meshes[i];
                g.GetComponent<MeshRenderer>().material = mat;
                g.GetComponent<MeshCollider>().sharedMesh = meshes[i];
                g.transform.SetParent(transform);
                current_meshes.Add(meshes[i]);
                objects.Add(g);
            }
            if(navigate)
            {
                GetComponent<NavMeshSurface>().BuildNavMesh();
            }
            
        }
    }
    public void startGeneration() //first function called upon generation
    {
        float[] arrx = new float[20];
        float[] arry = new float[20];

        debugFlattness.Clear();
        timeStart = Time.time;
        layer_rotation = new Vector2[octaves];
        layer_offset = new float[octaves];
        float store_random = 0;

        System.Random r = new System.Random(seed);
        for (int i = 0; i < octaves; i++)
        {
            store_random = (float)r.NextDouble();
            layer_rotation[i] = new Vector2(Mathf.Cos(store_random * 2 * Mathf.PI), Mathf.Sin(store_random * 2 * Mathf.PI));
            layer_offset[i] = (float)r.NextDouble() * 1000;
        }
        offset_variation_x = (float)r.NextDouble() * 10000f;
        offset_variation_z = (float)r.NextDouble() * 10000f;

        for (int i = 0; i < layer_rotation.Length; i++)
        {
            arrx[i] = layer_rotation[i].x;
            arry[i] = layer_rotation[i].y;
        }
        setInfo(arrx, arry, layer_rotation.Length, gradient_limit, threshold, start_magnitude, start_frequency, gradient_power); //offloads info to the DLL file's accessable memory
        if (t == null || !t.IsAlive) { // checks if the main aditional thread is still running
            done = false;
            for (int o = 0; o < objects.Count; o++) {
                Destroy(objects[o]);
                Destroy(current_meshes[o]);
            }
            frames.Clear();
            current_meshes.Clear();
            current_meshes.TrimExcess();
            objects.Clear();
            objects.TrimExcess();
            frames.TrimExcess(); //clears all current mesh related data and objects
            meshesDisplayed = 0;
            meshesMade = 0;
            int maxChunkDim = (int)(MathF.Cbrt(chunk_volume)/object_scale);

            Tuple<Vector3Int, Vector3>[] queues = new Tuple<Vector3Int, Vector3>[16];

            keepRunning = true;

            Thread[] threads = new Thread[16]; //currently 16 hardcoded threads

            int tracker = 0;

            for(int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() => {
                    int index = tracker;
                    tracker++;
                    while(keepRunning)
                    {
                        if (queues[index] != null)
                        {
                            generation(queues[index].Item1, queues[index].Item2); // creates the generation threads
                            queues[index] = null;
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                });
                threads[i].Start();
            }

            Thread endCheckerThread = new Thread(() =>
            {
                bool threadsActive = true;
                while (threadsActive)
                {
                    threadsActive = false;
                    for (int i = 0; i < threads.Length; i++)
                    {
                        if (threads[i].IsAlive)
                        {
                            threadsActive = true;
                            break;
                        }
                    }
                    Thread.Sleep(20);
                }
                Debug.Log("finished");
                done = true;
            });
            // this thread alocates tasks to the other threads
            t  = new Thread(() =>
            {

                for (int x = 0; x < range.x; x += maxChunkDim)
                {
                    for (int y = 0; y < range.y; y += maxChunkDim)
                    {
                        for (int z = 0; z < range.z; z += maxChunkDim)
                        {
                            bool pass = false;
                            for (int q = 0; q < 16; q++)
                            {
                                if (queues[q] == null)
                                {
                                    pass = true;
                                    queues[q] = new Tuple<Vector3Int, Vector3>(new Vector3Int(Math.Min(maxChunkDim + 2, range.x - x), Math.Min(maxChunkDim + 2, range.y - y), Math.Min(maxChunkDim + 2, range.z - z)), new Vector3(x - 1, y - 1, z - 1));
                                    break;
                                }
                            }
                            if (!pass)
                            {
                                z -= maxChunkDim;
                                Thread.Sleep(1);
                            }
                        }

                    }
                }

                keepRunning = false;
                endCheckerThread.Start();
                //Debug.Log("Done");
            });

            t.Start();


            /*t = new Thread(() => {
                System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                timer.Start();
                Parallel.For(0, range.x / maxChunkDim, xInd =>
                {
                    int x = (int)(xInd * maxChunkDim);
                    //for (int x = 0; x < range.x; x += maxChunkDim)
                    //{
                    //for (int x = 0; x < range.x; x += maxChunkDim)
                    for (int y = 0; y < range.y; y += maxChunkDim)
                    {
                        for (int z = 0; z < range.z; z += maxChunkDim)
                        {
                            generation(new Vector3Int(Math.Min(maxChunkDim + 2, range.x - x), Math.Min(maxChunkDim + 2, range.y - y), Math.Min(maxChunkDim + 2, range.z - z)), new Vector3(x - 1, y - 1, z - 1));
                        }
                    }
                });


                timer.Stop();
                Debug.Log(timer.ElapsedMilliseconds);
                Debug.Log("Done");
                Debug.Log(time_sum);
                time_sum = 0;
                done = true;
            });
            t.Start();*/
        }
    }

    private void generation(Vector3Int localRange, Vector3 offset) // probably soon to be ported over the dll. This function loops through a provided chunk of space and generates weight values
    {                                                              // these weight values are then used to inform the marching cubes later on in the function. This will definitley not be moved to a dll
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        float[,,] weights = new float[(int)(localRange.x * object_scale) + 1, (int)(localRange.y * object_scale) + 1, (int)(localRange.z * object_scale) + 1];
        done = false;
        for (int x = 0; x < localRange.x * object_scale + 1; x++)
        {
            for (int z = 0; z < localRange.z * object_scale + 1; z++)
            {
                // this antiflatness value uses calculations I did in desmos a long time ago to make the ground weighting more or less 
                float antiFlatness = Mathf.Clamp(2 * ground_weight_variation * (Mathf.PerlinNoise(((x / (float)object_scale) + offset.x + 0) * stretch.x * 2f + offset_variation_x, ((z / (float)object_scale) + offset.z + 0) * stretch.z * 2f + offset_variation_z) - 0.5f), -ground_weight_top * 0.97f, ground_weight_top * 0.97f);
                //antiFlatness = ground_weight_variation;
                /*if (offset.y < 10f)
                {
                    Mutex m = new Mutex();
                    m.WaitOne();
                    debugFlattness.Add(new KeyValuePair<Tuple<float, float>, float>(new Tuple<float, float>(((float)x / (float)object_scale + offset.x), ((float)z / (float)object_scale + offset.z)), antiFlatness));
                    m.ReleaseMutex();
                }*/

                for (int y = (z%2 + x%2)%2; y < localRange.y * object_scale + 1; y+=2)
                {
                    //if (y / object_scale < ground_weight_top)
                    //{
                    
                    //if(do_once > high)
                    //{
                    //    high = do_once;
                    //}
                    //else if(do_once < low)
                    //{
                    //    low = do_once;
                    //}
                        weights[x, y, z] += ((ground_weight_top + antiFlatness + ground_weight_centre - ((stretch.y / 0.001f) * (((float)y / (float)object_scale) + offset.y))) / (ground_weight_top + antiFlatness)) * ground_weight_strength; //antiflatness is the issue
                    //}
                    if ( float.IsNaN(weights[x,y,z]))
                    {
                        Debug.Log("Warning");
                    }
                    weights[x, y, z] = octaveNoise2((float)x / (float)object_scale + offset.x, (float)y / (float)object_scale + offset.y, (float)z / (float)object_scale + offset.z, stretch.x, stretch.y, stretch.z, octaves, weights[x,y,z]);
                    //weights[x, y, z] = 200 - (y / (float)object_scale + offset.y) + (2 * ground_weight_variation * (Mathf.PerlinNoise(((x / (float)object_scale) + offset.x + 0) * stretch.x * 10f + offset_variation_x, ((z / (float)object_scale) + offset.z + 0) * stretch.z * 10f + offset_variation_z) - 0.5f));
                    if (float.IsNaN(weights[x, y, z]))
                    {
                        Debug.Log("wwarning");
                    }
                }
            }
        }

        

        for (int x = 0; x < localRange.x * object_scale + 1; x++)
        {
            for (int z = 0; z < localRange.z * object_scale + 1; z++)
            {
                float antiFlatness = Mathf.Clamp(2 * ground_weight_variation * (Mathf.PerlinNoise(((x / (float)object_scale) + offset.x + 0) * stretch.x * 2f + offset_variation_x, ((z / (float)object_scale) + offset.z + 0) * stretch.z * 2f + offset_variation_z) - 0.5f), -ground_weight_top * 0.97f, ground_weight_top * 0.97f);
                /*if(offset.y < 2f)
                {
                    Mutex m = new Mutex();
                    m.WaitOne();
                    debugFlattness.Add(new KeyValuePair<Tuple<float, float>, float>(new Tuple<float, float>(((float)x / (float)object_scale + offset.x), ((float)z / (float)object_scale + offset.z)), antiFlatness));
                    m.ReleaseMutex();
                }*/

                //antiFlatness = ground_weight_variation;
                for (int y = (z % 2 + (x + 1) % 2) % 2; y < localRange.y * object_scale + 1; y+=2)
                {
                    //if (y / object_scale < ground_weight_top)
                    //{

                    //if(do_once > high)
                    //{
                    //    high = do_once;
                    //}
                    //else if(do_once < low)
                    //{
                    //    low = do_once;
                    //}
                    if(x != 0 && x != localRange.x * object_scale && y != 0 && y != localRange.y * object_scale && z != 0 && z != localRange.z * object_scale && false)
                    {
                        weights[x, y, z] = (weights[x + 1, y, z] + weights[x - 1, y, z] + weights[x, y + 1, z] + weights[x, y - 1, z] + weights[x, y, z + 1] + weights[x, y, z - 1]) / 6f;
                    }
                    else
                    {
                        weights[x, y, z] += ((ground_weight_top + antiFlatness + ground_weight_centre - ((stretch.y / 0.001f) * (((float)y / (float)object_scale) + offset.y))) / (ground_weight_top + antiFlatness)) * ground_weight_strength; //antiflatness is the issue
                                                                                                                                                                                                                                                //}
                        if (float.IsNaN(weights[x, y, z]))
                        {
                            Debug.Log("Warning");
                        }
                        //weights[x, y, z] = octaveNoise(new Vector3((float)x / (float)object_scale, (float)y / (float)object_scale, (float)z / (float)object_scale) + offset, stretch, octaves, weights[x, y, z]);
                        weights[x, y, z] = octaveNoise2((float)x / (float)object_scale + offset.x, (float)y / (float)object_scale + offset.y, (float)z / (float)object_scale + offset.z, stretch.x, stretch.y, stretch.z, octaves, weights[x, y, z]);
                        //weights[x, y, z] = 200 - (y / (float)object_scale + offset.y) + (2 * ground_weight_variation * (Mathf.PerlinNoise(((x / (float)object_scale) + offset.x + 0) * stretch.x * 10f + offset_variation_x, ((z / (float)object_scale) + offset.z + 0) * stretch.z * 10f + offset_variation_z) - 0.5f));
                        if (float.IsNaN(weights[x, y, z]))
                        {
                            Debug.Log("wwarning");
                        }
                    }
                    

                }
            }
        }
        

        //Debug.Log(stopwatch.ElapsedMilliseconds);

        List<poly> polys = new List<poly>();

        for (int x = 0; x < (int)(localRange.x * object_scale); x++)
        {
            for (int y = 0; y < (int)(localRange.y * object_scale); y++)
            {
                for (int z = 0; z < (int)(localRange.z * object_scale); z++)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        int edge = triTable[binary_based_index(x, y, z, weights), i];
                        if (edge != -1) 
                        {
                            if (i % 3 == 0)
                            {
                                polys.Add(new poly(new Vector3[3]));
                            }
                            Vector3Int corner1 = new Vector3Int((int)edgeLookUp[edge].x, (int)edgeLookUp[edge].y, (int)edgeLookUp[edge].z);
                            Vector3Int corner2 = new Vector3Int((int)Math.Ceiling(edgeLookUp[edge].x), (int)Math.Ceiling(edgeLookUp[edge].y), (int)Math.Ceiling(edgeLookUp[edge].z));
                            float interp = 0;
                            if (corner1 != corner2) {
                                corner1 += new Vector3Int(x, y, z);
                                corner2 += new Vector3Int(x, y, z);
                                float w1 = weights[corner1.x, corner1.y, corner1.z];
                                float w2 = weights[corner2.x, corner2.y, corner2.z];
                                interp = (threshold - w1) / (w2 - w1);
                            }
                            polys[polys.Count - 1].vertices[i % 3] = (((Vector3)corner1 * (1 - interp)) + ((Vector3)corner2 * (interp))) / object_scale;
                        }
                        else {
                            break;
                        }
                    }
                }
            }
        }
        if(polys.Count == 0) {
            return;
        }
        List<mesh_data> mesh_frames = new List<mesh_data>();

        Dictionary<Vector3, int> vertices_for_duplicates = new Dictionary<Vector3, int>();
        int store_try_get;
        List<Vector3> verts = new List<Vector3>();
        vertices_for_duplicates.Clear();
        verts.Capacity = polys.Count * 3;
        List<int> new_tris = new List<int>();
        int index = 0;
        List<Vector2> uvs1 = new List<Vector2>();

        for (int p = 0; p < polys.Count; p++)
        {
            for (int three = 0; three < 3; three++)
            {
                if (vertices_for_duplicates.TryGetValue(polys[p].vertices[three], out store_try_get))
                {
                    new_tris.Add(store_try_get);
                }
                else
                {
                    verts.Add(polys[p].vertices[three] + offset);
                    //uvs1.Add(new Vector2(polys[v].vertices[three].x / texture_scaling, polys[v].vertices[three].z / texture_scaling));
                    vertices_for_duplicates.Add(polys[p].vertices[three], verts.Count - 1);
                    new_tris.Add(verts.Count - 1);
                }
                index += 1;
            }

            if (index > 40000 * 3)
            {
                Debug.Log("error, to many vertices");
                mesh_frames.Add(new mesh_data(verts.ToArray(), new_tris.ToArray()));
                verts.Clear();
                new_tris.Clear();
                vertices_for_duplicates.Clear();
                index = 0;
            }

        }
        frameMutex.WaitOne(60000);
        mesh_frames.Add(new mesh_data(verts.ToArray(), new_tris.ToArray())); //sort out mesh frames as a list for inconsistencies 
        for (int j = 0; j < mesh_frames[mesh_frames.Count - 1].vertices.Length; j++)
        {
            if (float.IsNaN(mesh_frames[mesh_frames.Count - 1].vertices[j].x) || float.IsNaN(mesh_frames[mesh_frames.Count - 1].vertices[j].x) || float.IsNaN(mesh_frames[mesh_frames.Count - 1].vertices[j].x))
            {
                Debug.Log(offset);
            }
        }
        meshesMade += mesh_frames.Count;
        frameMutex.ReleaseMutex();
        //mesh_data mesh_frame = new mesh_data(verts.ToArray(), new_tris);

        frames.AddRange(mesh_frames);
    }

    public struct poly
    {
        public Vector3[] vertices;
        public poly(Vector3[] vertices)
        {
            this.vertices = vertices;
        }
    }

    public struct mesh_data
    {
        public Vector3[] vertices;
        public int[] triangles;
        internal mesh_data(Vector3[] vertices, int[] triangles)
        {
            this.vertices = vertices;
            this.triangles = triangles;
        }
    }

    int binary_based_index(int x, int y, int z, float[,,] weights)
    {
        int val = 0;
        int3 start = new int3(x, y, z);


        for (int i = 0; i < 8; i++)
        {
            if (weights[(start + cubeCorners[i]).x, (start + cubeCorners[i]).y, (start + cubeCorners[i]).z] > threshold)
            {
                val += (int)Mathf.Pow((int)2, (int)i);
            }
        }

        return val;
    }

    float octaveNoise(Vector3 input, Vector3 top_stretch, int octaves, float start)
    {
        float value = start;

        Vector3 store_offset = Vector3.zero;

        float x = input.x * layer_rotation[0].x - input.z * layer_rotation[0].y;
        float z = input.x * layer_rotation[0].y + input.z * layer_rotation[0].x;

        /*Vector3 yTwist = new Vector3(1.7f, 9.2f, 10f);
        Vector3 zTwist = new Vector3(8.3f, 2.8f, 3.1f);
        Vector3 xTwist = new Vector3(5.2f, 1.3f, 4.5f);

        

        for(int i = 0; i < warping_itterations; i++)
        {
            for (int o = 1; o < octaves; o++)
            {
                store_offset += new Vector3(better_noise(new Vector4((input.x + store_offset.x + xTwist.x) * top_stretch.x, (input.y + store_offset.y + xTwist.y) * top_stretch.y, (input.z + store_offset.z + xTwist.z) * top_stretch.z, seed) * Mathf.Pow(2, 0)) * Mathf.Pow(2, -1 * o),
                    better_noise(new Vector4((input.x + store_offset.x + yTwist.x) * top_stretch.x, (input.y + store_offset.y + yTwist.y) * top_stretch.y, (input.z + store_offset.z + yTwist.z) * top_stretch.z, seed) * Mathf.Pow(2, 0) ) * Mathf.Pow(2, -1 * o), 
                    better_noise(new Vector4((input.x + store_offset.x + zTwist.x) * top_stretch.x, (input.y + store_offset.y + zTwist.y) * top_stretch.y, (input.z + store_offset.z + zTwist.z) * top_stretch.z, seed) * Mathf.Pow(2, 0) ) * Mathf.Pow(2, -1 * o)) 
                    * warp_factor;
            }
        }*/

        Vector3 hold = new Vector3((x) * top_stretch.x, (input.y) * top_stretch.y, (z) * top_stretch.z) * start_frequency;
        Vector3 gradient;
        value += better_noise(hold, out gradient) * start_magnitude;

        float cumulitive_damening = x_squared_clamp(process_for_derivitive(gradient), gradient_limit);

        if (value >= threshold - start_magnitude/* && value < threshold*/)
        {
            for (int i = 1; i < octaves; i++)
            {
                x = input.x * layer_rotation[i].x - input.z * layer_rotation[i].y;
                z = input.x * layer_rotation[i].y + input.z * layer_rotation[i].x;
                hold = new Vector3((x) * top_stretch.x, (input.y) * top_stretch.y, (z) * top_stretch.z) * IntPow(2, i) * start_frequency;

                //value += better_noise(hold, out gradient) * Mathf.Pow(2, -1 * i) * start_magnitude / Mathf.Pow(1 + cumulitive_damening, gradient_power);
                value += betterNoise2(hold, out gradient) * Mathf.Pow(2, -1 * i) * start_magnitude / Mathf.Pow(1 + cumulitive_damening, gradient_power);
                cumulitive_damening += x_squared_clamp(process_for_derivitive(gradient), gradient_limit);
                if (value < threshold - (start_magnitude * Mathf.Pow(2, -1 * i)))
                {
                    break;
                }
            }
        }

        //if (float.IsNaN(value))
        //{
        //    Debug.Log("wwarning");
        //}

        return value;
    }

    int IntPow(int value, int power)
    {
        int to_return = 1;
        for(int i = 0; i < power; i++)
        {
            to_return *= value;
        }
        return to_return;
    }

    float betterNoise2(Vector3 input, out Vector3 normal)
    {
        float outx = 0;
        float outy = 0;
        float outz = 0;
        float to_return = (alternativeNoise(input.x, input.y, input.z, ref outx, ref outy, ref outz) + 1.2f) / 2.4f;

        normal = new Vector3(outx, outy, outz);
        return to_return;
    }

    float better_noise(Vector3 input, out Vector3 normal)
    {
        float3 Out;
        float to_return = (noise.snoise(input,out Out) + 1.2f) / 2.4f;
        normal = new Vector3(Out.x, Out.y, Out.z);
        return to_return;
    }

    float process_for_derivitive(Vector3 normal)
    {
        return Mathf.Abs(new Vector2(normal.x, normal.z).magnitude / normal.y);
    }

    private void OnDestroy()
    {
        t.Abort();
        keepRunning = false;
    }

    float x_squared_clamp(float input, float point_9)
    {
        if(float.IsNaN(input) || float.IsInfinity(input))
        {
            return point_9;
        }
        float v = (input) / Mathf.Sqrt(((input/point_9) * (input / point_9)) + 1);
        if (float.IsNaN(v))
        {
            Debug.Log("wwarning");
        }
        return v;
    }

    private void OnDrawGizmos()
    {
        //KeyValuePair<Tuple<float, float>, float>[] debugFlattnessArr = debugFlattness.ToArray();
        for(int i = 0; i < debugFlattness.Count; i+= 10)
        {
            Gizmos.color = new Color((debugFlattness[i].Value + ground_weight_variation) / (ground_weight_variation * 2), (debugFlattness[i].Value + ground_weight_variation) / (2 *ground_weight_variation), (debugFlattness[i].Value + ground_weight_variation) / (2 * ground_weight_variation));
            Gizmos.DrawCube(new Vector3(debugFlattness[i].Key.Item1,100, debugFlattness[i].Key.Item2), new Vector3(1f / (float)object_scale, 1f / (float)object_scale, 1f / (float)object_scale));

        }
    }

}
