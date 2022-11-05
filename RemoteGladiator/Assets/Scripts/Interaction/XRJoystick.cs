using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace Interaction
{
    public class XRJoystick : XRBaseInteractable
    {
        private const float MaxDeadZonePercent = 0.9f;

        public enum JoystickType
        {
            BothCircle,
            BothSquare,
            FrontBack,
            LeftRight,
        }

        [Serializable]
        public class ValueChangeEvent : UnityEvent<float> { }

        [SerializeField] private JoystickType joystickMotion = JoystickType.BothCircle;
        [SerializeField] private Transform handle = null;
        [SerializeField] private Vector2 value = Vector2.zero;
        [SerializeField] private bool recenterOnRelease = true;

        [SerializeField] [Range(1.0f, 90.0f)] private float maxAngle = 60.0f;
        [SerializeField] [Range(1.0f, 90.0f)] private float deadZoneAngle = 10.0f;

        [SerializeField] private ValueChangeEvent onValueChangeX = new();
        [SerializeField] private ValueChangeEvent onValueChangeY = new();

        private IXRSelectInteractor _selectInteractor;
        
        public JoystickType JoystickMotion { get { return joystickMotion; } set { joystickMotion = value; } }
        public Transform Handle { get { return handle; } set { handle = value; } }
        public Vector2 Value
        {
            get { return value; }
            set
            {
                if (!recenterOnRelease)
                {
                    SetValue(value);
                    SetHandleAngle(value * maxAngle);
                }
            }
        }

        /// <summary>
        /// If true, the joystick will return to center on release
        /// </summary>
        public bool RecenterOnRelease { get { return recenterOnRelease; } set { recenterOnRelease = value; } }

        /// <summary>
        /// Maximum angle the joystick can move
        /// </summary>
        public float MaxAngle { get { return maxAngle; } set { maxAngle = value; } }

        /// <summary>
        /// Minimum amount the joystick must move off the center to register changes
        /// </summary>
        public float DeadZoneAngle { get { return deadZoneAngle; } set { deadZoneAngle = value; } }

        /// <summary>
        /// Events to trigger when the joystick's x value changes
        /// </summary>
        public ValueChangeEvent OnValueChangeX => onValueChangeX;

        /// <summary>
        /// Events to trigger when the joystick's y value changes
        /// </summary>
        public ValueChangeEvent OnValueChangeY => onValueChangeY;

        void Start()
        {
            if (recenterOnRelease)
                SetHandleAngle(Vector2.zero);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        private void StartGrab(SelectEnterEventArgs args)
        {
            _selectInteractor = args.interactorObject;
        }

        private void EndGrab(SelectExitEventArgs arts)
        {
            UpdateValue();

            if (recenterOnRelease)
            {
                SetHandleAngle(Vector2.zero);
                SetValue(Vector2.zero);
            }

            _selectInteractor = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            {
                if (isSelected)
                {
                    UpdateValue();
                }
            }
        }

        Vector3 GetLookDirection()
        {
            Vector3 direction = _selectInteractor.GetAttachTransform(this).position - handle.position;
            direction = transform.InverseTransformDirection(direction);
            switch (joystickMotion)
            {
                case JoystickType.FrontBack:
                    direction.x = 0;
                    break;
                case JoystickType.LeftRight:
                    direction.z = 0;
                    break;
            }

            direction.y = Mathf.Clamp(direction.y, 0.01f, 1.0f);
            return direction.normalized;
        }

        void UpdateValue()
        {
            var lookDirection = GetLookDirection();

            // Get up/down angle and left/right angle
            var upDownAngle = Mathf.Atan2(lookDirection.z, lookDirection.y) * Mathf.Rad2Deg;
            var leftRightAngle = Mathf.Atan2(lookDirection.x, lookDirection.y) * Mathf.Rad2Deg;

            // Extract signs
            var signX = Mathf.Sign(leftRightAngle);
            var signY = Mathf.Sign(upDownAngle);

            upDownAngle = Mathf.Abs(upDownAngle);
            leftRightAngle = Mathf.Abs(leftRightAngle);

            var stickValue = new Vector2(leftRightAngle, upDownAngle) * (1.0f / maxAngle);

            // Clamp the stick value between 0 and 1 when doing everything but circular stick motion
            if (joystickMotion != JoystickType.BothCircle)
            {
                stickValue.x = Mathf.Clamp01(stickValue.x);
                stickValue.y = Mathf.Clamp01(stickValue.y);
            }
            else
            {
                // With circular motion, if the stick value is greater than 1, we normalize
                // This way, an extremely strong value in one direction will influence the overall stick direction
                if (stickValue.magnitude > 1.0f)
                {
                    stickValue.Normalize();
                }
            }

            // Rebuild the angle values for visuals
            leftRightAngle = stickValue.x * signX * maxAngle;
            upDownAngle = stickValue.y * signY * maxAngle;

            // Apply deadzone and sign back to the logical stick value
            var deadZone = deadZoneAngle / maxAngle;
            var aliveZone = (1.0f - deadZone);
            stickValue.x = Mathf.Clamp01((stickValue.x - deadZone)) / aliveZone;
            stickValue.y = Mathf.Clamp01((stickValue.y - deadZone)) / aliveZone;

            // Re-apply signs
            stickValue.x *= signX;
            stickValue.y *= signY;

            SetHandleAngle(new Vector2(leftRightAngle, upDownAngle));
            SetValue(stickValue);
        }

        void SetValue(Vector2 value)
        {
            this.value = value;
            onValueChangeX.Invoke(this.value.x);
            onValueChangeY.Invoke(this.value.y);
        }

        void SetHandleAngle(Vector2 angles)
        {
            if (handle == null)
                return;

            var xComp = Mathf.Tan(angles.x * Mathf.Deg2Rad);
            var zComp = Mathf.Tan(angles.y * Mathf.Deg2Rad);
            var largerComp = Mathf.Max(Mathf.Abs(xComp), Mathf.Abs(zComp));
            var yComp = Mathf.Sqrt(1.0f - largerComp * largerComp);

            handle.up = (transform.up * yComp) + (transform.right * xComp) + (transform.forward * zComp);
        }

        void OnDrawGizmosSelected()
        {
            var angleStartPoint = transform.position;

            if (handle != null)
                angleStartPoint = handle.position;

            const float k_AngleLength = 0.25f;

            if (joystickMotion != JoystickType.LeftRight)
            {
                Gizmos.color = Color.green;
                var axisPoint1 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(maxAngle, 0.0f, 0.0f) * Vector3.up) * k_AngleLength;
                var axisPoint2 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(-maxAngle, 0.0f, 0.0f) * Vector3.up) * k_AngleLength;
                Gizmos.DrawLine(angleStartPoint, axisPoint1);
                Gizmos.DrawLine(angleStartPoint, axisPoint2);

                if (deadZoneAngle > 0.0f)
                {
                    Gizmos.color = Color.red;
                    axisPoint1 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(deadZoneAngle, 0.0f, 0.0f) * Vector3.up) * k_AngleLength;
                    axisPoint2 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(-deadZoneAngle, 0.0f, 0.0f) * Vector3.up) * k_AngleLength;
                    Gizmos.DrawLine(angleStartPoint, axisPoint1);
                    Gizmos.DrawLine(angleStartPoint, axisPoint2);
                }
            }

            if (joystickMotion != JoystickType.FrontBack)
            {
                Gizmos.color = Color.green;
                var axisPoint1 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(0.0f, 0.0f, maxAngle) * Vector3.up) * k_AngleLength;
                var axisPoint2 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(0.0f, 0.0f, -maxAngle) * Vector3.up) * k_AngleLength;
                Gizmos.DrawLine(angleStartPoint, axisPoint1);
                Gizmos.DrawLine(angleStartPoint, axisPoint2);

                if (deadZoneAngle > 0.0f)
                {
                    Gizmos.color = Color.red;
                    axisPoint1 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(0.0f, 0.0f, deadZoneAngle) * Vector3.up) * k_AngleLength;
                    axisPoint2 = angleStartPoint + transform.TransformDirection(Quaternion.Euler(0.0f, 0.0f, -deadZoneAngle) * Vector3.up) * k_AngleLength;
                    Gizmos.DrawLine(angleStartPoint, axisPoint1);
                    Gizmos.DrawLine(angleStartPoint, axisPoint2);
                }
            }
        }

        void OnValidate()
        {
            deadZoneAngle = Mathf.Min(deadZoneAngle, maxAngle * MaxDeadZonePercent);
        }
    }
}
