using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewportDrawer : MonoBehaviour
{
    [SerializeField] Vector2 sizeViewport = new Vector2(0.3f,0.3f);
    [SerializeField] float distanceFromCamera = 1.0f;
    public LineRenderer lineRenderer; 


    // Start is called before the first frame update
    void Start()
    {
        lineRenderer = this.GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        lineRenderer.positionCount = 5;

        Vector3 topLeft = new Vector3(-sizeViewport.x / 2.0f, sizeViewport.y / 2.0f, distanceFromCamera);
        Vector3 topRight = new Vector3(sizeViewport.x / 2.0f, sizeViewport.y / 2.0f, distanceFromCamera);
        Vector3 bottomRight = new Vector3(sizeViewport.x / 2.0f, -sizeViewport.y / 2.0f, distanceFromCamera);
        Vector3 bottomLeft = new Vector3(-sizeViewport.x / 2.0f, -sizeViewport.y / 2.0f, distanceFromCamera);

        lineRenderer.SetPosition(0, topLeft);
        lineRenderer.SetPosition(1, topRight);
        lineRenderer.SetPosition(2, bottomRight);
        lineRenderer.SetPosition(3, bottomLeft);
        lineRenderer.SetPosition(4, topLeft);
    }
}
