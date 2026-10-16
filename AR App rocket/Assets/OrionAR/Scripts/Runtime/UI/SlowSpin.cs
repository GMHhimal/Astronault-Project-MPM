using UnityEngine;

namespace OrionAR
{
    /// <summary>Rotates a backdrop body about its own axis, very slowly.</summary>
    public class SlowSpin : MonoBehaviour
    {
        public float degreesPerSecond = 2f;
        public Vector3 axis = Vector3.up;

        void Update()
        {
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
