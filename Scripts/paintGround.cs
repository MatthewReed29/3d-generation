using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class paintGround : MonoBehaviour
{
    Color[] vertexColors;
    Vector3[] vertices;
    MeshFilter meshFilter;
    
    // Start is called before the first frame update
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.sharedMesh;
        vertices = mesh.vertices;
        Vector2[] uvs1 = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            uvs1[i] = new Vector2(vertices[i].x / 20f, vertices[i].z / 20f);
        }

        meshFilter.sharedMesh.uv = uvs1;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setVerteces()
    {

    }

    public void paintVertices(float brushSize, Ray ray, Color paint)
    {
        Mesh mesh = meshFilter.sharedMesh;
        vertices = mesh.vertices;
        vertexColors = mesh.colors;
        if(vertexColors.Length == 0)
        {
            vertexColors = new Color[vertices.Length];
            for(int i =0; i < vertexColors.Length; i++)
            {
                vertexColors[i] = Color.white;
            }
        }

        Vector2[] uvs1 = new Vector2[vertices.Length];

        for(int i = 0; i < vertices.Length; i++)
        {
            uvs1[i] = new Vector2(vertices[i].x / 20f, vertices[i].z / 20f);
        }
        for (int i = 0; i < vertices.Length; i++) 
        {
            if(Vector3.Cross(ray.direction, vertices[i] + transform.position - ray.origin).magnitude / ray.direction.magnitude < brushSize)
            {
                vertexColors[i] = paint;
            }
        }
        meshFilter.sharedMesh.colors = vertexColors;
        meshFilter.sharedMesh.uv = uvs1;
    }

}
