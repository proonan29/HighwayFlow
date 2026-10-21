using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HighwayFlow
{
    /// <summary>Orbit camera over the highway: left-drag rotate, right/middle-drag pan, wheel zoom. Modes: zone (ramp area), overview (whole road), follow a vehicle.</summary>
    public class CameraRig : MonoBehaviour
    {
        public enum Mode { Zone, Overview, Follow }
        public Mode mode = Mode.Zone;
        public Vector3 pivot = new Vector3(2000f, 0f, -6f);
        public float yaw = -18f, pitch = 48f, distance = 240f;
        public float followDistance = 30f;
        public float roadLength = 4000f;
        [Range(0f, 0.5f)] public float viewportLeft;           // fraction of the screen covered by the side panel
        public System.Func<Vector2, bool> isOverUI;
        public System.Func<Vector3?> followProvider;           // world position of the followed vehicle
        Camera cam;
        Vector3 followPivot;
        bool dragging;

        public void FrameZone(float centerX, float centerZ)
        {
            mode = Mode.Zone; pivot = new Vector3(centerX, 0f, centerZ); yaw = -8f; pitch = 40f; distance = 230f; Place();
        }

        public void FrameOverview(float length)
        {
            mode = Mode.Overview; roadLength = length; pivot = new Vector3(length * 0.5f, 0f, 0f); yaw = 0f; pitch = 90f;
            if (cam == null) cam = GetComponent<Camera>();
            float aspect = cam != null ? cam.aspect : 1.78f;
            float t = Mathf.Tan((cam != null ? cam.fieldOfView : 40f) * 0.5f * Mathf.Deg2Rad);
            distance = length * 1.04f / (2f * t * aspect * Mathf.Max(0.3f, 1f - viewportLeft));
            Place();
        }

        public void SetFollow() { mode = Mode.Follow; yaw = 90f; pitch = 20f; var p = followProvider?.Invoke(); if (p.HasValue) followPivot = p.Value; }

        void LateUpdate() { HandleInput(); Place(); }

        void HandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool over = isOverUI != null && isOverUI(pos);
            bool anyPress = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            if (anyPress) dragging = !over;
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed) dragging = false;
            Vector2 d = mouse.delta.ReadValue();
            if (dragging && mouse.leftButton.isPressed && mode != Mode.Overview)
            {
                yaw += d.x * 0.25f;
                pitch = Mathf.Clamp(pitch - d.y * 0.25f, 8f, 89f);
            }
            else if (dragging && (mouse.rightButton.isPressed || mouse.middleButton.isPressed || (mode == Mode.Overview && mouse.leftButton.isPressed)))
            {
                float k = (mode == Mode.Follow ? followDistance : distance) * 0.0016f;
                Vector3 right = transform.right, fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) fwd = transform.up; fwd.Normalize();
                if (mode == Mode.Follow) mode = Mode.Zone;
                pivot += -right * d.x * k - fwd * d.y * k;
                pivot.y = 0f;
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (!over && Mathf.Abs(wheel) > 0.01f)
            {
                float f = Mathf.Pow(0.88f, Mathf.Sign(wheel) * Mathf.Min(3f, Mathf.Abs(wheel) / 120f + 0.5f));
                if (mode == Mode.Follow) followDistance = Mathf.Clamp(followDistance * f, 6f, 150f);
                else distance = Mathf.Clamp(distance * f, 20f, 6000f);
            }
#endif
        }

        public void Place()
        {
            Vector3 p = pivot; float dist = distance;
            if (mode == Mode.Follow)
            {
                var t = followProvider?.Invoke();
                if (t.HasValue) followPivot = Application.isPlaying ? Vector3.Lerp(followPivot, t.Value, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f)) : t.Value;
                p = followPivot; dist = followDistance;
            }
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = p - rot * Vector3.forward * dist + Vector3.up * (mode == Mode.Follow ? 1.5f : 0f);
            transform.rotation = rot;
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(3000f, dist * 3f);
                cam.nearClipPlane = Mathf.Clamp(dist * 0.02f, 0.3f, 20f);
                cam.ResetProjectionMatrix();
                if (viewportLeft > 0.001f)
                {
                    var m = cam.projectionMatrix;
                    m[0, 2] = -viewportLeft;
                    cam.projectionMatrix = m;
                }
            }
        }
    }
}
