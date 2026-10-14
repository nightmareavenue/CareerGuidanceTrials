using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 3.5f, -8f);
    public float positionSmooth = 5f;
    public float rotationSmooth = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // Берём только поворот машины вокруг вертикальной оси,
        // чтобы камера не кувыркалась вместе с ней
        Quaternion yaw = Quaternion.Euler(0, target.eulerAngles.y, 0);
        Vector3 desiredPos = target.position + yaw * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionSmooth * Time.deltaTime);

        Quaternion desiredRot = Quaternion.LookRotation(target.position + Vector3.up - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmooth * Time.deltaTime);
    }
}
