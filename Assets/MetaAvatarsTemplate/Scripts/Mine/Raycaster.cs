using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

//using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR;
using System;

public class Raycaster : MonoBehaviour
{
    public enum DeviceType
    {
        LTouch, RTouch, Keyboard
    };


    [SerializeField] DeviceType deviceType = DeviceType.Keyboard;
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] Color colorOfRay;
    [SerializeField] float rayLength = 5.0f;
    [SerializeField] float lowerThreshold = 0.02f;
    [SerializeField] float upperThreshold = 5.0f;
    [SerializeField] float stepSize = 0.1f;
    private bool lasttimeTriggered;
    bool triggered = false;
    bool handTriggered = false;

    float initTimestamp = 0;
    float timeElapsed = 0;
    Vector3 rotationObj;
    float zDepth = 0;

    TaskManager taskManager;
    OVRInput.Controller controllerActive;

    GameObject lockedObject;
    // Start is called before the first frame update
    void Start()
    {
        lineRenderer.SetPosition(0, this.gameObject.transform.TransformPoint(0, 0, rayLength));
        controllerActive = OVRInput.Controller.RTouch;
        taskManager = GameObject.Find("RootAreas").GetComponent<TaskManager>();
    }

    // Update is called once per frame
    void Update()
    {
        

        Debug.DrawLine(this.gameObject.transform.TransformPoint(0, 0, 0), this.gameObject.transform.TransformPoint(0, 0, rayLength), Color.cyan);
        //OVRInput.Get()
    }

    void FixedUpdate()
    {
        if (deviceType == DeviceType.RTouch)
        {
            controllerActive = OVRInput.Controller.RTouch;
        }
        else if(deviceType == DeviceType.LTouch)
        {
            controllerActive = OVRInput.Controller.LTouch;
        }


        // Bit shift the index of the layer (8) to get a bit mask
        int layerMask = 1 << 9;
        // This would cast rays only against colliders in layer 8.
        // But instead we want to collide against everything except layer 8. The ~ operator does this, it inverts a bitmask.
        //layerMask = ~layerMask;

        //OVRInput.Controller controllerActive = OVRInput.Controller.RTouch;
        //triggered = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controllerActive);
        //print("triggered ? " + triggered);

        //lasttimeTriggered = triggered;
        triggered = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controllerActive);
        handTriggered = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controllerActive);

        if (!triggered)
        {
            //transform.DetachChildren();
        }
        if(!triggered && lasttimeTriggered)
        {
            timeElapsed += (Time.realtimeSinceStartup - initTimestamp);//time one person is interacting with an object
        }
        //print("triggered ? " + triggered + " button up : " + buttonUp);
        if (lockedObject)
            return;

        RaycastHit hit;
        // Does the ray intersect any objects excluding the player layer
        

        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, upperThreshold,layerMask))
        {
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            //Debug.Log("Did Hit");
            Controller_mapping(hit);
            print("@hit" + hit.transform.gameObject.name + "from player " + (taskManager.isRemotePlayer ? "P2" : "P1"));
            Vector3 hitLocalPos = transform.InverseTransformPoint(hit.transform.position);

            lineRenderer.SetPosition(0, this.gameObject.transform.TransformPoint(0, 0, 0));
            lineRenderer.SetPosition(1, this.gameObject.transform.TransformPoint(0, 0, hitLocalPos.z));


            if (taskManager)
            {
                if (taskManager.currentTaskState <= TaskState.BothConnected)
                    return;
                if (taskManager.isRemotePlayer)
                {
                    try
                    {
                        taskManager.GetComponent<PhotonView>().RPC("objectInteractedByP2", RpcTarget.AllBuffered, true);
                    }
                    catch(Exception ex)
                    {
                        print("error in the RPC call for the raycaster" + ex.StackTrace);
                    }
                }
                else
                {
                    taskManager.objectInteractedByP1(true);
                }
                
            }
        }
        else
        {
            lineRenderer.SetPosition(0, this.gameObject.transform.TransformPoint(0, 0, 0));
            lineRenderer.SetPosition(1, this.gameObject.transform.TransformPoint(0, 0, rayLength));
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1000, Color.white);
            //Debug.Log("Did not Hit");
            if (transform.childCount > 0)
            {
                //transform.DetachChildren();
            }
            if (taskManager)
            {
                if (taskManager.currentTaskState <= TaskState.BothConnected)
                    return;
                if (taskManager.isRemotePlayer)
                {
                    try
                    {
                        taskManager.GetComponent<PhotonView>().RPC("objectInteractedByP2", RpcTarget.AllBuffered, false);
                    }
                    catch (Exception ex)
                    {
                        print("error in the RPC call for the raycaster" + ex.StackTrace);
                    }
                }
                else
                {
                    taskManager.objectInteractedByP1(false);
                }
            }
            
        }

        lasttimeTriggered = triggered;
    }

    private void OnTriggerEnter(Collider other)
    {
        //this.
    }

    private void OnTriggerStay(Collider other)
    {
        
        if (triggered && !other.gameObject.name.Contains("Raycaster"))
        {
            //print("entered");
            if (lockedObject)
                return;
            BoxCollider collider = GetComponent<BoxCollider>();
            other.gameObject.transform.position = collider.ClosestPoint(other.gameObject.transform.position);
            Vector2 thumbstickValue = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controllerActive);

            float hitDepth = transform.InverseTransformPoint(other.transform.position).z;
            if (thumbstickValue.y > 0)
            {
                other.transform.position = other.transform.position + (this.transform.transform.forward * stepSize);
                zDepth = this.transform.InverseTransformPoint(other.transform.position).z;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        lockedObject = null;
    }
    void Controller_mapping(RaycastHit hitObj)
    {
        Vector3 hitPosition = hitObj.transform.position;
        float hitDepth = transform.InverseTransformPoint(hitPosition).z;

        PhotonView photonView = hitObj.transform.gameObject.GetPhotonView();
       
        if (photonView)
        {
            if(triggered || handTriggered)
            {
                photonView.RequestOwnership();//request ownership of the object
                print("requesting ownership of object " + hitObj.transform.name + "from player " + (taskManager.isRemotePlayer ? "P2" : "P1"));
            }
        }
        else if(!photonView)
        {
            print("Photon view not present in object " + hitObj.transform.name + "from player " + (taskManager.isRemotePlayer ? "P2" : "P1"));
        }

        zDepth = this.gameObject.transform.InverseTransformPoint(hitObj.transform.position).z;
        
        
        //hitObj.transform.position = transform.TransformPoint(transform.localPosition.x, transform.localPosition.y, hitDepth);
        
        
        if(deviceType == DeviceType.Keyboard)
        {
            hitObj.transform.parent = this.transform;

            if (Input.GetKey(KeyCode.Q))
            {
                if (hitDepth > lowerThreshold)
                {
                    hitObj.transform.position = hitObj.transform.position - (this.transform.transform.forward * stepSize);
                }
                else
                {
                    //do nothing
                }
            }
            else if (Input.GetKey(KeyCode.Z))
            {
                if (hitDepth < upperThreshold)
                {
                    hitObj.transform.position = hitObj.transform.position + (this.transform.transform.forward * stepSize);
                }
                else
                {
                    //do nothing
                }
            }
        }
        else {


            Vector2 thumbstickValue = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controllerActive);
            if (!lasttimeTriggered && triggered)
            {
                rotationObj = hitObj.transform.eulerAngles;
                initTimestamp = Time.realtimeSinceStartup;
            }
            if (triggered)
            {
                zDepth = this.transform.InverseTransformPoint(hitObj.transform.position).z;
                Vector3 worldPos = this.transform.TransformPoint(this.transform.localPosition.x, this.transform.localPosition.y, zDepth);
                
                //x, y, zDepth
                
                hitObj.transform.position = worldPos;
                //hitObj.transform.position = this.transform.InverseTransformPoint()
                //hitObj.transform.parent = this.gameObject.transform;
                //hitObj.transform.eulerAngles = rotationObj;//lockRotation
            }
            if(thumbstickValue.y > 0 && handTriggered)
            {
                if (hitDepth < upperThreshold)
                {
                    hitObj.transform.position = hitObj.transform.position + (this.transform.transform.forward * stepSize);
                    zDepth = this.transform.InverseTransformPoint(hitObj.transform.position).z;
                }
                else
                {
                    //do nothing
                }
            }
            else if(thumbstickValue.y < 0 && handTriggered)
            {
                if (hitDepth > lowerThreshold)
                {
                    hitObj.transform.position = hitObj.transform.position - (this.transform.transform.forward * stepSize);
                    zDepth = this.transform.InverseTransformPoint(hitObj.transform.position).z;
                }
                else
                {
                    //do nothing
                }
            }
            //print("zDepth = " + zDepth);
        }
    }

        
}
