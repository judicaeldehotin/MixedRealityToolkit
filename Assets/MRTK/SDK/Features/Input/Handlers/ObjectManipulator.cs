// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Experimental.Physics;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace Microsoft.MixedReality.Toolkit.UI
{
    /// <summary>
    /// This script allows for an object to be movable, scalable, and rotatable with one or two hands.
    /// You may also configure the script on only enable certain manipulations. The script works with
    /// both HoloLens' gesture input and immersive headset's motion controller input.
    /// </summary>
    [HelpURL("https://microsoft.github.io/MixedRealityToolkit-Unity/Documentation/README_ObjectManipulator.html")]
    [RequireComponent(typeof(ConstraintManager))]
    public class ObjectManipulator : MonoBehaviour, IMixedRealityPointerHandler, IMixedRealityFocusChangedHandler
    {
        #region Public Enums

        /// <summary>
        /// Describes what pivot the manipulated object will rotate about when
        /// you rotate your hand. This is not a description of any limits or
        /// additional rotation logic. If no other factors (such as constraints)
        /// are involved, rotating your hand by an amount should rotate the object
        /// by the same amount.
        /// For example a possible future value here is RotateAboutUserDefinedPoint
        /// where the user could specify a pivot that the object is to rotate
        /// around.
        /// An example of a value that should not be found here is MaintainRotationToUser
        /// as this restricts rotation of the object when we rotate the hand.
        /// </summary>
        public enum RotateInOneHandType
        {
            RotateAboutObjectCenter,
            RotateAboutGrabPoint
        };
        [System.Flags]
        public enum ReleaseBehaviorType
        {
            KeepVelocity = 1 << 0,
            KeepAngularVelocity = 1 << 1
        }
        #endregion Public Enums

        #region Serialized Fields

        [SerializeField]
        [Tooltip("Transform that will be dragged. Defaults to the object of the component.")]
        private Transform hostTransform = null;

        /// <summary>
        /// Transform that will be dragged. Defaults to the object of the component.
        /// </summary>
        public Transform HostTransform
        {
            get
            {
                if (hostTransform == null)
                {
                    hostTransform = gameObject.transform;
                }

                return hostTransform;
            }
            set => hostTransform = value;
        }

        [SerializeField]
        [EnumFlags]
        [Tooltip("Can manipulation be done only with one hand, only with two hands, or with both?")]
        private ManipulationHandFlags manipulationType = ManipulationHandFlags.OneHanded | ManipulationHandFlags.TwoHanded;

        /// <summary>
        /// Can manipulation be done only with one hand, only with two hands, or with both?
        /// </summary>
        public ManipulationHandFlags ManipulationType
        {
            get => manipulationType;
            set => manipulationType = value;
        }

        [SerializeField]
        [EnumFlags]
        [Tooltip("What manipulation will two hands perform?")]
        private TransformFlags twoHandedManipulationType = TransformFlags.Move | TransformFlags.Rotate | TransformFlags.Scale;

        /// <summary>
        /// What manipulation will two hands perform?
        /// </summary>
        public TransformFlags TwoHandedManipulationType
        {
            get => twoHandedManipulationType;
            set => twoHandedManipulationType = value;
        }

        [SerializeField]
        [Tooltip("Specifies whether manipulation can be done using far interaction with pointers.")]
        private bool allowFarManipulation = true;

        /// <summary>
        /// Specifies whether manipulation can be done using far interaction with pointers.
        /// </summary>
        public bool AllowFarManipulation
        {
            get => allowFarManipulation;
            set => allowFarManipulation = value;
        }

        [SerializeField]
        [Tooltip(
             "Whether physics forces are used to move the object when performing near manipulations. " +
             "Off will make the object feel more directly connected to the hand. On will honor the mass and inertia of the object. " +
             "The default is off.")]
        private bool useForcesForNearManipulation = false;

        /// <summary>
        /// Whether physics forces are used to move the object when performing near manipulations.
        /// </summary>
        /// <remarks>
        /// Setting this to <c>false</c> will make the object feel more directly connected to the
        /// users hand. Setting this to <c>true</c> will honor the mass and inertia of the object,
        /// but may feel as though the object is connected through a spring. The default is <c>false</c>.
        /// </remarks>
        public bool UseForcesForNearManipulation
        {
            get => useForcesForNearManipulation;
            set => useForcesForNearManipulation = value;
        }

        [SerializeField]
        [Tooltip("Rotation behavior of object when using one hand near")]
        private RotateInOneHandType oneHandRotationModeNear = RotateInOneHandType.RotateAboutGrabPoint;

        /// <summary>
        /// Rotation behavior of object when using one hand near
        /// </summary>
        public RotateInOneHandType OneHandRotationModeNear
        {
            get => oneHandRotationModeNear;
            set => oneHandRotationModeNear = value;
        }

        [SerializeField]
        [Tooltip("Rotation behavior of object when using one hand at distance")]
        private RotateInOneHandType oneHandRotationModeFar = RotateInOneHandType.RotateAboutGrabPoint;

        /// <summary>
        /// Rotation behavior of object when using one hand at distance
        /// </summary>
        public RotateInOneHandType OneHandRotationModeFar
        {
            get => oneHandRotationModeFar;
            set => oneHandRotationModeFar = value;
        }

        [SerializeField]
        [EnumFlags]
        [Tooltip("Rigid body behavior of the dragged object when releasing it.")]
        private ReleaseBehaviorType releaseBehavior = ReleaseBehaviorType.KeepVelocity | ReleaseBehaviorType.KeepAngularVelocity;

        /// <summary>
        /// Rigid body behavior of the dragged object when releasing it.
        /// </summary>
        public ReleaseBehaviorType ReleaseBehavior
        {
            get => releaseBehavior;
            set => releaseBehavior = value;
        }

        /// <summary>
        /// Obsolete: Whether to enable frame-rate independent smoothing.
        /// </summary>
        [Obsolete("SmoothingActive is obsolete and will be removed in a future version. Applications should use SmoothingFar, SmoothingNear or a combination of the two.")]
        public bool SmoothingActive
        {
            get => smoothingFar;
            set => smoothingFar = value;
        }

        [FormerlySerializedAs("smoothingActive")]
        [SerializeField]
        [Tooltip("Frame-rate independent smoothing for far interactions. Far smoothing is enabled by default.")]
        private bool smoothingFar = true;

        /// <summary>
        /// Whether to enable frame-rate independent smoothing for far interactions.
        /// </summary>
        /// <remarks>
        /// Far smoothing is enabled by default.
        /// </remarks>
        public bool SmoothingFar
        {
            get => smoothingFar;
            set => smoothingFar = value;
        }

        [SerializeField]
        [Tooltip("Frame-rate independent smoothing for near interactions. Near smoothing is disabled by default because the effect may be perceived as being 'disconnected' from the hand.")]
        private bool smoothingNear = false;

        /// <summary>
        /// Whether to enable frame-rate independent smoothing for near interactions.
        /// </summary>
        /// <remarks>
        /// Near smoothing is disabled by default because the effect may be perceived as being 'disconnected' from the hand.
        /// </remarks>
        public bool SmoothingNear
        {
            get => smoothingNear;
            set => smoothingNear = value;
        }

        [SerializeField]
        [Range(0, 1)]
        [Tooltip("Enter amount representing amount of smoothing to apply to the movement. Smoothing of 0 means no smoothing. Max value means no change to value.")]
        private float moveLerpTime = 0.001f;

        /// <summary>
        /// Enter amount representing amount of smoothing to apply to the movement. Smoothing of 0 means no smoothing. Max value means no change to value.
        /// </summary>
        public float MoveLerpTime
        {
            get => moveLerpTime;
            set => moveLerpTime = value;
        }

        [SerializeField]
        [Range(0, 1)]
        [Tooltip("Enter amount representing amount of smoothing to apply to the rotation. Smoothing of 0 means no smoothing. Max value means no change to value.")]
        private float rotateLerpTime = 0.001f;

        /// <summary>
        /// Enter amount representing amount of smoothing to apply to the rotation. Smoothing of 0 means no smoothing. Max value means no change to value.
        /// </summary>
        public float RotateLerpTime
        {
            get => rotateLerpTime;
            set => rotateLerpTime = value;
        }

        [SerializeField]
        [Range(0, 1)]
        [Tooltip("Enter amount representing amount of smoothing to apply to the scale. Smoothing of 0 means no smoothing. Max value means no change to value.")]
        private float scaleLerpTime = 0.001f;

        /// <summary>
        /// Enter amount representing amount of smoothing to apply to the scale. Smoothing of 0 means no smoothing. Max value means no change to value.
        /// </summary>
        public float ScaleLerpTime
        {
            get => scaleLerpTime;
            set => scaleLerpTime = value;
        }

        [SerializeField]
        [Tooltip("Elastics Manager slot to enable elastics simulation when manipulating the object.")]
        private ElasticsManager elasticsManager;
        /// <summary>
        /// Elastics Manager slot to enable elastics simulation when manipulating the object.
        /// </summary>
        public ElasticsManager ElasticsManager
        {
            get => elasticsManager;
            set => elasticsManager = value;
        }

        #endregion Serialized Fields

        #region Event handlers
        [Header("Manipulation Events")]
        [SerializeField]
        [FormerlySerializedAs("OnManipulationStarted")]
        private ManipulationEvent onManipulationStarted = new ManipulationEvent();

        /// <summary>
        /// Unity event raised on manipulation started
        /// </summary>
        public ManipulationEvent OnManipulationStarted
        {
            get => onManipulationStarted;
            set => onManipulationStarted = value;
        }

        [SerializeField]
        [FormerlySerializedAs("OnManipulationEnded")]
        private ManipulationEvent onManipulationEnded = new ManipulationEvent();

        /// <summary>
        /// Unity event raised on manipulation ended
        /// </summary>
        public ManipulationEvent OnManipulationEnded
        {
            get => onManipulationEnded;
            set => onManipulationEnded = value;
        }

        [SerializeField]
        [FormerlySerializedAs("OnHoverEntered")]
        private ManipulationEvent onHoverEntered = new ManipulationEvent();

        /// <summary>
        /// Unity event raised on hover started
        /// </summary>
        public ManipulationEvent OnHoverEntered
        {
            get => onHoverEntered;
            set => onHoverEntered = value;
        }

        [SerializeField]
        [FormerlySerializedAs("OnHoverExited")]
        private ManipulationEvent onHoverExited = new ManipulationEvent();

        /// <summary>
        /// Unity event raised on hover ended
        /// </summary>
        public ManipulationEvent OnHoverExited
        {
            get => onHoverExited;
            set => onHoverExited = value;
        }

        #endregion Event Handlers

        #region Private Properties

        private ManipulationMoveLogic moveLogic;
        private TwoHandScaleLogic scaleLogic;
        private TwoHandRotateLogic rotateLogic;

        /// <summary>
        /// Holds the pointer and the initial intersection point of the pointer ray
        /// with the object on pointer down in pointer space
        /// </summary>
        private struct PointerData
        {
            public IMixedRealityPointer pointer;
            private Vector3 initialGrabPointInPointer;

            public PointerData(IMixedRealityPointer pointer, Vector3 worldGrabPoint) : this()
            {
                this.pointer = pointer;
                this.initialGrabPointInPointer = Quaternion.Inverse(pointer.Rotation) * (worldGrabPoint - pointer.Position);
            }

            public bool IsNearPointer => pointer is IMixedRealityNearPointer;

            /// Returns the grab point on the manipulated object in world space
            public Vector3 GrabPoint => (pointer.Rotation * initialGrabPointInPointer) + pointer.Position;
        }

        private Dictionary<uint, PointerData> pointerIdToPointerMap = new Dictionary<uint, PointerData>();
        private Quaternion objectToGripRotation;
        private bool isNearManipulation;
        private bool isManipulationStarted;
        private bool isSmoothing;

        private Rigidbody rigidBody;
        private bool wasGravity = false;
        private bool wasKinematic = false;

        private ConstraintManager constraints;

        private bool IsOneHandedManipulationEnabled => manipulationType.HasFlag(ManipulationHandFlags.OneHanded) && pointerIdToPointerMap.Count == 1;
        private bool IsTwoHandedManipulationEnabled => manipulationType.HasFlag(ManipulationHandFlags.TwoHanded) && pointerIdToPointerMap.Count > 1;

        #endregion Private Properties

        #region MonoBehaviour Functions

        private void Awake()
        {
            moveLogic = new ManipulationMoveLogic();
            rotateLogic = new TwoHandRotateLogic();
            scaleLogic = new TwoHandScaleLogic();

            if (elasticsManager)
            {
                elasticsManager.InitializeElastics(HostTransform);
            }
        }
        private void Start()
        {
            rigidBody = HostTransform.GetComponent<Rigidbody>();
            constraints = gameObject.EnsureComponent<ConstraintManager>();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculates the unweighted average, or centroid, of all pointers'
        /// grab points, as defined by the PointerData.GrabPoint property.
        /// Does not use the rotation of each pointer; represents a pure
        /// geometric centroid  of the grab points in world space.
        /// </summary>
        /// <returns>
        /// Worldspace grab point centroid of all pointers
        /// in pointerIdToPointerMap.
        /// </returns>
        private Vector3 GetPointersGrabPoint()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var p in pointerIdToPointerMap.Values)
            {
                sum += p.GrabPoint;
                count++;
            }
            return sum / Math.Max(1, count);
        }

        /// <summary>
        /// Calculates the multiple-handed pointer pose, used for
        /// far-interaction hand-ray-based manipulations. Uses the
        /// unweighted vector average of the pointers' forward vectors
        /// to calculate a compound pose that takes into account the
        /// pointing direction of each pointer.
        /// </summary>
        /// <returns>
        /// Compound pose calculated as the average of the poses
        /// corresponding to all of the pointers in pointerIdToPointerMap.
        /// </returns>
        private MixedRealityPose GetPointersPose()
        {
            Vector3 sumPos = Vector3.zero;
            Vector3 sumDir = Vector3.zero;
            int count = 0;
            foreach (var p in pointerIdToPointerMap.Values)
            {
                sumPos += p.pointer.Position;
                sumDir += p.pointer.Rotation * Vector3.forward;
                count++;
            }

            return new MixedRealityPose
            {
                Position = sumPos / Math.Max(1, count),
                Rotation = Quaternion.LookRotation(sumDir / Math.Max(1, count))
            };
        }

        private Vector3 GetPointersVelocity()
        {
            Vector3 sum = Vector3.zero;
            int numControllers = 0;
            foreach (var p in pointerIdToPointerMap.Values)
            {
                // Check pointer has a valid controller (e.g. gaze pointer doesn't)
                if (p.pointer.Controller != null)
                {
                    numControllers++;
                    sum += p.pointer.Controller.Velocity;
                }
            }
            return sum / Math.Max(1, numControllers);
        }

        private Vector3 GetPointersAngularVelocity()
        {
            Vector3 sum = Vector3.zero;
            int numControllers = 0;
            foreach (var p in pointerIdToPointerMap.Values)
            {
                // Check pointer has a valid controller (e.g. gaze pointer doesn't)
                if (p.pointer.Controller != null)
                {
                    numControllers++;
                    sum += p.pointer.Controller.AngularVelocity;
                }
            }
            return sum / Math.Max(1, numControllers);
        }

        private bool IsNearManipulation()
        {
            foreach (var item in pointerIdToPointerMap)
            {
                if (item.Value.IsNearPointer)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion Private Methods

        #region Public Methods

        /// <summary>
        /// Releases the object that is currently manipulated
        /// </summary>
        public void ForceEndManipulation()
        {
            // end manipulation
            if (isManipulationStarted)
            {
                HandleManipulationEnded(GetPointersGrabPoint(), GetPointersVelocity(), GetPointersAngularVelocity());
            }
            pointerIdToPointerMap.Clear();
        }

        /// <summary>
        /// Gets the grab point for the given pointer id.
        /// Only use if you know that your given pointer id corresponds to a pointer that has grabbed
        /// this component.
        /// </summary>
        public Vector3 GetPointerGrabPoint(uint pointerId)
        {
            Assert.IsTrue(pointerIdToPointerMap.ContainsKey(pointerId));
            return pointerIdToPointerMap[pointerId].GrabPoint;
        }

        #endregion Public Methods

        #region Hand Event Handlers

        /// <inheritdoc />
        public void OnPointerDown(MixedRealityPointerEventData eventData)
        {
            if (eventData.used ||
                    (!allowFarManipulation && eventData.Pointer as IMixedRealityNearPointer == null))
            {
                return;
            }

            // If we only allow one handed manipulations, check there is no hand interacting yet.
            if (manipulationType != ManipulationHandFlags.OneHanded || pointerIdToPointerMap.Count == 0)
            {
                uint id = eventData.Pointer.PointerId;
                // Ignore poke pointer events
                if (!pointerIdToPointerMap.ContainsKey(id))
                {
                    // cache start ptr grab point
                    pointerIdToPointerMap.Add(id, new PointerData(eventData.Pointer, eventData.Pointer.Result.Details.Point));

                    // Re-initialize elastic systems.
                    if (elasticsManager)
                    {
                        elasticsManager.InitializeElastics(HostTransform);
                    }

                    // Call manipulation started handlers
                    if (IsTwoHandedManipulationEnabled)
                    {
                        if (!isManipulationStarted)
                        {
                            HandleManipulationStarted();
                        }
                        HandleTwoHandManipulationStarted();
                    }
                    else if (IsOneHandedManipulationEnabled)
                    {
                        if (!isManipulationStarted)
                        {
                            HandleManipulationStarted();
                        }
                        HandleOneHandMoveStarted();
                    }
                }
            }

            if (pointerIdToPointerMap.Count > 0)
            {
                // Always mark the pointer data as used to prevent any other behavior to handle pointer events
                // as long as the ObjectManipulator is active.
                // This is due to us reacting to both "Select" and "Grip" events.
                eventData.Use();
            }
        }

        public void OnPointerDragged(MixedRealityPointerEventData eventData)
        {
            // Call manipulation updated handlers
            if (IsOneHandedManipulationEnabled)
            {
                HandleOneHandMoveUpdated();
            }
            else if (IsTwoHandedManipulationEnabled)
            {
                HandleTwoHandManipulationUpdated();
            }
        }

        /// <inheritdoc />
        public void OnPointerUp(MixedRealityPointerEventData eventData)
        {
            // Get pointer data before they are removed from the map
            Vector3 grabPoint = GetPointersGrabPoint();
            Vector3 velocity = GetPointersVelocity();
            Vector3 angularVelocity = GetPointersAngularVelocity();

            uint id = eventData.Pointer.PointerId;
            if (pointerIdToPointerMap.ContainsKey(id))
            {
                pointerIdToPointerMap.Remove(id);
            }

            // Call manipulation ended handlers
            var handsPressedCount = pointerIdToPointerMap.Count;
            if (manipulationType.HasFlag(ManipulationHandFlags.TwoHanded) && handsPressedCount == 1)
            {
                if (manipulationType.HasFlag(ManipulationHandFlags.OneHanded))
                {
                    HandleOneHandMoveStarted();
                }
                else
                {
                    HandleManipulationEnded(grabPoint, velocity, angularVelocity);
                }
            }
            else if (isManipulationStarted && handsPressedCount == 0)
            {
                HandleManipulationEnded(grabPoint, velocity, angularVelocity);
            }

            eventData.Use();
        }

        #endregion Hand Event Handlers

        #region Private Event Handlers
        private void HandleTwoHandManipulationStarted()
        {
            var handPositionArray = GetHandPositionArray();

            if (twoHandedManipulationType.HasFlag(TransformFlags.Rotate))
            {
                rotateLogic.Setup(handPositionArray, HostTransform);
            }
            if (twoHandedManipulationType.HasFlag(TransformFlags.Move))
            {
                // If near manipulation, a pure grabpoint centroid is used for
                // the initial pointer pose; if far manipulation, a more complex
                // look-rotation-based pointer pose is used.
                MixedRealityPose pointerPose = IsNearManipulation() ? new MixedRealityPose(GetPointersGrabPoint()) : GetPointersPose();
                MixedRealityPose hostPose = new MixedRealityPose(HostTransform.position, HostTransform.rotation);
                moveLogic.Setup(pointerPose, GetPointersGrabPoint(), hostPose, HostTransform.localScale);
            }
            if (twoHandedManipulationType.HasFlag(TransformFlags.Scale))
            {
                scaleLogic.Setup(handPositionArray, HostTransform);
            }
        }

        private void HandleTwoHandManipulationUpdated()
        {
            var targetTransform = new MixedRealityTransform(HostTransform.position, HostTransform.rotation, HostTransform.localScale);

            var handPositionArray = GetHandPositionArray();

            if (twoHandedManipulationType.HasFlag(TransformFlags.Scale))
            {
                targetTransform.Scale = scaleLogic.UpdateMap(handPositionArray);
                constraints.ApplyScaleConstraints(ref targetTransform, false, IsNearManipulation());
            }
            if (twoHandedManipulationType.HasFlag(TransformFlags.Rotate))
            {
                targetTransform.Rotation = rotateLogic.Update(handPositionArray, targetTransform.Rotation);
                constraints.ApplyRotationConstraints(ref targetTransform, false, IsNearManipulation());
            }
            if (twoHandedManipulationType.HasFlag(TransformFlags.Move))
            {
                // If near manipulation, a pure GrabPoint centroid is used for
                // the pointer pose; if far manipulation, a more complex
                // look-rotation-based pointer pose is used.
                MixedRealityPose pose = IsNearManipulation() ? new MixedRealityPose(GetPointersGrabPoint()) : GetPointersPose();
                targetTransform.Position = moveLogic.Update(pose, targetTransform.Rotation, targetTransform.Scale, true);
                constraints.ApplyTranslationConstraints(ref targetTransform, false, IsNearManipulation());
            }

            ApplyTargetTransform(targetTransform);
        }

        private void HandleOneHandMoveStarted()
        {
            Assert.IsTrue(pointerIdToPointerMap.Count == 1);
            PointerData pointerData = GetFirstPointer();
            IMixedRealityPointer pointer = pointerData.pointer;

            // Calculate relative transform from object to grip.
            Quaternion gripRotation;
            TryGetGripRotation(pointer, out gripRotation);
            Quaternion worldToGripRotation = Quaternion.Inverse(gripRotation);
            objectToGripRotation = worldToGripRotation * HostTransform.rotation;

            MixedRealityPose pointerPose = new MixedRealityPose(pointer.Position, pointer.Rotation);
            MixedRealityPose hostPose = new MixedRealityPose(HostTransform.position, HostTransform.rotation);
            moveLogic.Setup(pointerPose, pointerData.GrabPoint, hostPose, HostTransform.localScale);
        }

        private void HandleOneHandMoveUpdated()
        {
            Debug.Assert(pointerIdToPointerMap.Count == 1);
            PointerData pointerData = GetFirstPointer();
            IMixedRealityPointer pointer = pointerData.pointer;

            var targetTransform = new MixedRealityTransform(HostTransform.position, HostTransform.rotation, HostTransform.localScale);

            constraints.ApplyScaleConstraints(ref targetTransform, true, IsNearManipulation());

            Quaternion gripRotation;
            TryGetGripRotation(pointer, out gripRotation);
            targetTransform.Rotation = gripRotation * objectToGripRotation;

            constraints.ApplyRotationConstraints(ref targetTransform, true, IsNearManipulation());

            RotateInOneHandType rotateInOneHandType = isNearManipulation ? oneHandRotationModeNear : oneHandRotationModeFar;
            MixedRealityPose pointerPose = new MixedRealityPose(pointer.Position, pointer.Rotation);
            targetTransform.Position = moveLogic.Update(pointerPose, targetTransform.Rotation, targetTransform.Scale, rotateInOneHandType != RotateInOneHandType.RotateAboutObjectCenter);

            constraints.ApplyTranslationConstraints(ref targetTransform, true, IsNearManipulation());

            ApplyTargetTransform(targetTransform);
        }

        private void HandleManipulationStarted()
        {
            isManipulationStarted = true;
            isNearManipulation = IsNearManipulation();
            isSmoothing = (isNearManipulation ? smoothingNear : smoothingFar);

            // TODO: If we are on HoloLens 1, push and pop modal input handler so that we can use old
            // gaze/gesture/voice manipulation. For HoloLens 2, we don't want to do this.
            if (OnManipulationStarted != null)
            {
                OnManipulationStarted.Invoke(new ManipulationEventData
                {
                    ManipulationSource = gameObject,
                    IsNearInteraction = isNearManipulation,
                    Pointer = GetFirstPointer().pointer,
                    PointerCentroid = GetPointersGrabPoint(),
                    PointerVelocity = GetPointersVelocity(),
                    PointerAngularVelocity = GetPointersAngularVelocity()
                });
            }

            if (rigidBody != null)
            {
                wasGravity = rigidBody.useGravity;
                wasKinematic = rigidBody.isKinematic;
                rigidBody.useGravity = false;
                rigidBody.isKinematic = false;
            }

            constraints.Initialize(new MixedRealityTransform(HostTransform));
            if (elasticsManager != null)
            {
                elasticsManager.EnableElasticsUpdate = false;
            }
        }

        private void HandleManipulationEnded(Vector3 pointerGrabPoint, Vector3 pointerVelocity, Vector3 pointerAnglularVelocity)
        {
            isManipulationStarted = false;
            // TODO: If we are on HoloLens 1, push and pop modal input handler so that we can use old
            // gaze/gesture/voice manipulation. For HoloLens 2, we don't want to do this.
            if (OnManipulationEnded != null)
            {
                OnManipulationEnded.Invoke(new ManipulationEventData
                {
                    ManipulationSource = gameObject,
                    IsNearInteraction = isNearManipulation,
                    PointerCentroid = pointerGrabPoint,
                    PointerVelocity = pointerVelocity,
                    PointerAngularVelocity = pointerAnglularVelocity
                });
            }

            ReleaseRigidBody(pointerVelocity, pointerAnglularVelocity);
            if (elasticsManager)
            {
                elasticsManager.EnableElasticsUpdate = true;
            }
        }

        #endregion Private Event Handlers

        #region Unused Event Handlers
        /// <inheritdoc />
        public void OnPointerClicked(MixedRealityPointerEventData eventData) { }
        public void OnBeforeFocusChange(FocusEventData eventData) { }

        #endregion Unused Event Handlers

        #region Private methods

        private void ApplyTargetTransform(MixedRealityTransform targetTransform)
        {
            if (rigidBody == null)
            {
                TransformFlags transformUpdated = 0;
                if (elasticsManager != null)
                {
                    transformUpdated = elasticsManager.ApplyTargetTransform(targetTransform);
                }

                if (!transformUpdated.HasFlag(TransformFlags.Move))
                {
                    HostTransform.position = isSmoothing ? Smoothing.SmoothTo(HostTransform.position, targetTransform.Position, moveLerpTime, Time.deltaTime) : targetTransform.Position;
                }
                if (!transformUpdated.HasFlag(TransformFlags.Rotate))
                {
                    HostTransform.rotation = isSmoothing ? Smoothing.SmoothTo(HostTransform.rotation, targetTransform.Rotation, rotateLerpTime, Time.deltaTime) : targetTransform.Rotation;
                }
                if (!transformUpdated.HasFlag(TransformFlags.Scale))
                {
                    HostTransform.localScale = isSmoothing ? Smoothing.SmoothTo(HostTransform.localScale, targetTransform.Scale, scaleLerpTime, Time.deltaTime) : targetTransform.Scale;
                }
            }
            else
            {
                // There is a RigidBody. Potential different paths for near vs far manipulation
                if (isNearManipulation && !useForcesForNearManipulation)
                {
                    // This is a near manipulation and we're not using forces
                    // Apply direct updates but account for smoothing

                    if (isSmoothing)
                    {
                        rigidBody.MovePosition(Smoothing.SmoothTo(rigidBody.position, targetTransform.Position, moveLerpTime, Time.deltaTime));
                        rigidBody.MoveRotation(Smoothing.SmoothTo(rigidBody.rotation, targetTransform.Rotation, rotateLerpTime, Time.deltaTime));
                    }
                    else
                    {
                        rigidBody.MovePosition(targetTransform.Position);
                        rigidBody.MoveRotation(targetTransform.Rotation);
                    }
                }
                else
                {
                    // We are using forces

                    rigidBody.velocity = ((1f - Mathf.Pow(moveLerpTime, Time.deltaTime)) / Time.deltaTime) * (targetTransform.Position - HostTransform.position);

                    var relativeRotation = targetTransform.Rotation * Quaternion.Inverse(HostTransform.rotation);
                    relativeRotation.ToAngleAxis(out float angle, out Vector3 axis);
                    
                    if (axis.IsValidVector())
                    {
                        if (angle > 180f)
                        {
                            angle -= 360f;
                        }
                        rigidBody.angularVelocity = ((1f - Mathf.Pow(rotateLerpTime, Time.deltaTime)) / Time.deltaTime) * (axis.normalized * angle * Mathf.Deg2Rad);
                    }
                }

                HostTransform.localScale = isSmoothing ? Smoothing.SmoothTo(HostTransform.localScale, targetTransform.Scale, scaleLerpTime, Time.deltaTime) : targetTransform.Scale;
            }
        }

        private Vector3[] GetHandPositionArray()
        {
            var handPositionMap = new Vector3[pointerIdToPointerMap.Count];
            int index = 0;
            foreach (var item in pointerIdToPointerMap)
            {
                handPositionMap[index++] = item.Value.pointer.Position;
            }
            return handPositionMap;
        }

        public void OnFocusChanged(FocusEventData eventData)
        {
            bool isFar = !(eventData.Pointer is IMixedRealityNearPointer);
            if (isFar && !AllowFarManipulation)
            {
                return;
            }

            if (eventData.OldFocusedObject == null ||
                    !eventData.OldFocusedObject.transform.IsChildOf(transform))
            {
                if (OnHoverEntered != null)
                {
                    OnHoverEntered.Invoke(new ManipulationEventData
                    {
                        ManipulationSource = gameObject,
                        Pointer = eventData.Pointer,
                        IsNearInteraction = !isFar
                    });
                }
            }
            else if (eventData.NewFocusedObject == null ||
                     !eventData.NewFocusedObject.transform.IsChildOf(transform))
            {
                if (OnHoverExited != null)
                {
                    OnHoverExited.Invoke(new ManipulationEventData
                    {
                        ManipulationSource = gameObject,
                        Pointer = eventData.Pointer,
                        IsNearInteraction = !isFar
                    });
                }
            }
        }

        private void ReleaseRigidBody(Vector3 velocity, Vector3 angularVelocity)
        {
            if (rigidBody != null)
            {
                rigidBody.useGravity = wasGravity;
                rigidBody.isKinematic = wasKinematic;

                if (releaseBehavior.HasFlag(ReleaseBehaviorType.KeepVelocity))
                {
                    rigidBody.velocity = velocity;
                }

                if (releaseBehavior.HasFlag(ReleaseBehaviorType.KeepAngularVelocity))
                {
                    rigidBody.angularVelocity = angularVelocity;
                }
            }
        }

        private PointerData GetFirstPointer()
        {
            // We may be able to do this without allocating memory.
            // Moving to a method for later investigation.
            return pointerIdToPointerMap.Values.First();
        }

        private bool TryGetGripRotation(IMixedRealityPointer pointer, out Quaternion rotation)
        {
            for (int i = 0; i < pointer.Controller.Interactions.Length; i++)
            {
                if (pointer.Controller.Interactions[i].InputType == DeviceInputType.SpatialGrip)
                {
                    rotation = pointer.Controller.Interactions[i].RotationData;
                    return true;
                }
            }
            rotation = Quaternion.identity;
            return false;
        }

        #endregion
    }
}
