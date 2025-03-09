using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class paintCamera : MonoBehaviour
{
    // Start is called before the first frame update
    Camera cam;
    public Color paint;
    public float brushSize;
    public Material testMat;
    void Start()
    {
        cam = GetComponent<Camera>();
        for (int i = 0; i < testMat.passCount; i++)
        {
            Debug.Log(testMat.GetPassName(i));
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.A))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.SphereCastAll(ray, brushSize, 400, int.MaxValue);
            for(int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.gameObject.GetComponent<paintGround>())
                {
                    hits[i].collider.gameObject.GetComponent<paintGround>().paintVertices(brushSize, ray, paint);
                }
            }
        }
    }
}
