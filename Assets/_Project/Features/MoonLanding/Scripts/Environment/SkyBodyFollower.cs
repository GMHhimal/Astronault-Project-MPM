using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Keeps a sky object (the Earth) at a fixed direction from the camera, like a skybox element,
    /// so it never shows parallax and stays lit by the real sun light (correct Earth phase).
    /// </summary>
    public class SkyBodyFollower : MonoBehaviour
    {
        public Transform viewer;
        public Vector3 direction = new Vector3(0.2f, 0.6f, 0.75f);
        public float distance = 12000f;
        [Tooltip("Degrees per second around the body's own axis.")]
        public float spinSpeed = 0.15f;

        void LateUpdate()
        {
            if (viewer == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                viewer = cam.transform;
            }
            transform.position = viewer.position + direction.normalized * distance;
            if (spinSpeed != 0f) transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }
    }
}
