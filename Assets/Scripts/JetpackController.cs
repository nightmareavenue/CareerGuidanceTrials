using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class JetpackController : MonoBehaviour
{
    public float liftAcceleration = 28f;
    public float maxUpwardSpeed = 12f;
    public float airAcceleration = 18f;
    public float maxAirSpeed = 18f;
    public float airTurnAcceleration = 4f;
    public float maxAirTurnSpeed = 2f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.isPressed) return;

        float throttle = (keyboard.wKey.isPressed ? 1f : 0f)
                       - (keyboard.sKey.isPressed ? 1f : 0f);
        float steer = (keyboard.dKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed ? 1f : 0f);

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        float forwardSpeed = Vector3.Dot(horizontalVelocity, transform.forward);
        if (throttle != 0f && (Mathf.Abs(forwardSpeed) < maxAirSpeed || Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed)))
            rb.AddForce(transform.forward * throttle * airAcceleration, ForceMode.Acceleration);

        if (steer != 0f)
            rb.AddTorque(Vector3.up * steer * airTurnAcceleration, ForceMode.Acceleration);

        Vector3 angularVelocity = rb.angularVelocity;
        angularVelocity.y = Mathf.Clamp(angularVelocity.y, -maxAirTurnSpeed, maxAirTurnSpeed);
        rb.angularVelocity = angularVelocity;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y >= maxUpwardSpeed)
        {
            velocity.y = maxUpwardSpeed;
            rb.linearVelocity = velocity;
            return;
        }

        rb.AddForce(Vector3.up * liftAcceleration, ForceMode.Acceleration);
    }
}