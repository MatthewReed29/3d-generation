using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GoTo : MonoBehaviour
{
    Camera cam;
    NavMeshAgent self_agent;
    NavMeshPath path;
    Vector3 destination;
    float delay_store = 0f;
    // Start is called before the first frame update
    void Start()
    {
        cam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        self_agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if(Physics.Raycast(ray, out hit))
            {
                destination = hit.point;
                path = new NavMeshPath();
                if (self_agent.CalculatePath(hit.point, path))
                {
                    self_agent.SetPath(path);
                }
                else
                {
                    self_agent.SetDestination(hit.point);
                }
            }
        }
        if(self_agent.pathStatus == NavMeshPathStatus.PathPartial && Time.time > delay_store)
        {
            self_agent.CalculatePath(destination, path);
            if(path != self_agent.path)
            {
                self_agent.SetPath(path);
            }
            delay_store = Time.time + 3f;
        }
        if(self_agent.pathStatus == NavMeshPathStatus.PathComplete && path != null && Time.time > delay_store)
        {
            self_agent.CalculatePath(destination, path);
            self_agent.SetPath(path);
            delay_store = Time.time +  3f;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        foreach(Vector3 corner in path.corners)
        {
            Gizmos.DrawCube(corner, Vector3.one);
        }
    }

}
