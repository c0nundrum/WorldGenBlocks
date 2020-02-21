using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class CameraControlSystem : ComponentSystem
{

    private bool useKeyboardInput = true;
    private string horizontalAxis = "Horizontal";
    private string verticalAxis = "Vertical";

    private float keyboardMovementSpeed = 5f;
    private float rotationSpeed = 3f;
    private float mouseRotationSpeed = 10f;

    private bool useKeyboardRotation = true;
    private KeyCode rotateRightKey = KeyCode.X;
    private KeyCode rotateLeftKey = KeyCode.Z;

    private bool useMouseRotation = true;
    private KeyCode mouseRotationKey = KeyCode.Mouse1;

    private int RotationDirection
    {
        get
        {
            bool rotateRight = Input.GetKey(rotateRightKey);
            bool rotateLeft = Input.GetKey(rotateLeftKey);
            if (rotateLeft && rotateRight)
                return 0;
            else if (rotateLeft && !rotateRight)
                return -1;
            else if (!rotateLeft && rotateRight)
                return 1;
            else
                return 0;
        }
    }

    private float2 KeyboardInput
    {
        get { return useKeyboardInput ? new Vector2(Input.GetAxis(horizontalAxis), Input.GetAxis(verticalAxis)) : Vector2.zero; }
    }

    private Vector2 MouseInput
    {
        get { return Input.mousePosition; }
    }

    private Vector2 MouseAxis
    {
        get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
    }

    private void Rotation(Camera mainCamera)
    {
        if (useKeyboardRotation)
            mainCamera.transform.Rotate(Vector3.up, RotationDirection * Time.DeltaTime * rotationSpeed, Space.World);

        if (useMouseRotation && Input.GetKey(mouseRotationKey))
            mainCamera.transform.Rotate(Vector3.up, -MouseAxis.x * Time.DeltaTime * mouseRotationSpeed, Space.World);
    }

    private void move(Camera mainCamera)
    {
        if (useKeyboardInput)
        {
            Vector3 desiredMove = new Vector3(KeyboardInput.x, 0, KeyboardInput.y);

            desiredMove *= keyboardMovementSpeed;
            desiredMove *= Time.DeltaTime;
            desiredMove = Quaternion.Euler(new Vector3(0f, mainCamera.transform.eulerAngles.y, 0f)) * desiredMove;
            desiredMove = mainCamera.transform.InverseTransformDirection(desiredMove);

            mainCamera.transform.Translate(desiredMove, Space.Self);
        }
    }

    protected override void OnUpdate()
    {
        Camera mainCamera = Camera.main;

        move(mainCamera);
        Rotation(mainCamera);

    }
}
