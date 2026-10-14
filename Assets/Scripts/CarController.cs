using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft, frontRight, rearLeft, rearRight;

    [Header("Wheel Meshes")]
    public Transform flMesh, frMesh, rlMesh, rrMesh;

    [Header("Settings")]
    public float motorTorque = 1500f;
    public float brakeTorque = 3000f;
    public float maxSteerAngle = 30f;
    public float maxSpeed = 25f;                       // м/с (~90 км/ч)
    public Vector3 centerOfMass = new Vector3(0, 0.3f, 0);

    // Цилиндр лежит осью по Y, поэтому поворачиваем его на 90° по Z
    public Vector3 meshRotationOffset = new Vector3(0, 0, 90);

    Rigidbody rb;
    float throttle, steer;
    bool braking;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass;
    }

    // Ввод читаем в Update, физику применяем в FixedUpdate
    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        throttle = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0)
                 - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
        steer = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0)
              - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
        braking = k.spaceKey.isPressed;

        // R — поставить машинку на колёса
        if (k.rKey.wasPressedThisFrame)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.position += Vector3.up * 1.5f;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
        }
    }

    void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude;
        float torque = speed < maxSpeed ? throttle * motorTorque : 0f;
        float brake = braking ? brakeTorque : 0f;

        // Полный привод
        foreach (var w in new[] { frontLeft, frontRight, rearLeft, rearRight })
        {
            w.motorTorque = torque;
            w.brakeTorque = brake;
        }

        // На скорости руль поворачивается меньше
        float angle = steer * maxSteerAngle * Mathf.Lerp(1f, 0.4f, speed / maxSpeed);
        frontLeft.steerAngle = angle;
        frontRight.steerAngle = angle;

        UpdateWheel(frontLeft, flMesh);
        UpdateWheel(frontRight, frMesh);
        UpdateWheel(rearLeft, rlMesh);
        UpdateWheel(rearRight, rrMesh);
    }

    void UpdateWheel(WheelCollider col, Transform wheel)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheel.SetPositionAndRotation(pos, rot * Quaternion.Euler(meshRotationOffset));
    }
}
