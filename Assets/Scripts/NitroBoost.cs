using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class NitroBoost : MonoBehaviour
{
    public float acceleration = 20f;
    public float maxForwardSpeed = 40f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.leftShiftKey.isPressed) return;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (forwardSpeed < maxForwardSpeed)
            rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
    }
}