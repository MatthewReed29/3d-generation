using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class place_navigator : MonoBehaviour
{
    Camera cam;
    public GameObject traversal_bio;
    // Start is called before the first frame update
    void Start()
    {
        cam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse1) && Input.GetKey(KeyCode.P))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if(Physics.Raycast(ray, out hit))
            {
                Instantiate(traversal_bio, hit.point, Quaternion.identity);
            }
        }
    }
}
