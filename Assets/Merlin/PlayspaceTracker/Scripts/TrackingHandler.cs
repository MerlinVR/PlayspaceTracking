
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Merlin
{
    /// <summary>
    /// Tracks the camera tracking position offset from the head to neutralize VR offset, tracks the player base, and tracks the playspace root
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("")]
    public class TrackingHandler : UdonSharpBehaviour
    {
#pragma warning disable CS0649
        [SerializeField]
        Camera editorRefCamera;

        [SerializeField]
        Camera vrMeasureCamera;

        [SerializeField]
        Transform _cameraRoot;

        /// <summary>
        /// The root position of the camera transform so that it is offset to neutralize the VR transform
        /// </summary>
        //[PublicAPI]
        public Transform CameraRoot { get => _cameraRoot; }

        [SerializeField]
        Transform _playspaceRoot;

        /// <summary>
        /// This will be the same as the camera root when in VR, but will be the base of the player while in desktop
        /// </summary>
        //[PublicAPI]
        public Transform PlayspaceRoot { get => _playspaceRoot; }

        [SerializeField]
        Transform _headRoot;

        /// <summary>
        /// A transform that follows the head position, rotation, and most importantly, scale which the player Api does not expose
        /// </summary>
        //[PublicAPI]
        public Transform HeadRoot { get => _headRoot; }

        [SerializeField]
        Transform _playerRoot;

        /// <summary>
        /// This will always be at the root of the player capsule
        /// </summary>
        //[PublicAPI]
        public Transform PlayerRoot { get => _playerRoot; }

        [SerializeField] Transform spaceTransferTransform;
        
        VRCPlayerApi playerApi;
#pragma warning restore CS0649

        Vector3 lastHeadPosition;
        float lastVRCamHeight;

        private void Start()
        {
            Transform trackerRoot = transform.parent;

            // The tracker needs to be at 1 scale
            trackerRoot.parent = null;
            trackerRoot.position = Vector3.zero;
            trackerRoot.rotation = Quaternion.identity;
            trackerRoot.localScale = Vector3.one;

#if UNITY_EDITOR
            if (editorRefCamera == null)
                Debug.LogWarning("Playspace tracker editor ref camera is null, will not track in editor.", this);
#else
            lastHeadPosition = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
#endif
            lastVRCamHeight = vrMeasureCamera.transform.localPosition.y;
        }

        private void Update()
        {
            UpdateTracking();
        }

        /// <summary>
        /// Moves a transform from a source coordinate space to a target coordinate space. This means its current local transform relative to the source transform's local space will be transferred to the target transform's local space.
        /// </summary>
        /// <param name="transferTransform"></param>
        /// <param name="sourceTransform"></param>
        /// <param name="targetTransform"></param>
        [PublicAPI]
        public void TransferTransform(Transform transferTransform, Transform sourceTransform, Transform targetTransform)
        {
            Transform originalParent = transferTransform.parent;

            spaceTransferTransform.parent = sourceTransform;
            spaceTransferTransform.localPosition = Vector3.zero;
            spaceTransferTransform.localRotation = Quaternion.identity;
            spaceTransferTransform.localScale = Vector3.one;

            transferTransform.parent = spaceTransferTransform;
            spaceTransferTransform.parent = targetTransform;
            spaceTransferTransform.localPosition = Vector3.zero;
            spaceTransferTransform.localRotation = Quaternion.identity;
            spaceTransferTransform.localScale = Vector3.one;

            transferTransform.parent = originalParent;
        }

        /// <summary>
        /// Forces an update to tracking, this would generally be used by teleports to make sure other visual stuff is up to date, but we may need better handling if some visual stuff has already updated before the teleport.
        /// </summary>
        [PublicAPI]
        public void UpdateTracking()
        {
#if UNITY_EDITOR
            Transform refCamTransform = editorRefCamera.transform;

            if (refCamTransform)
            {
                transform.position = refCamTransform.position;
                transform.rotation = refCamTransform.rotation;
                transform.localScale = refCamTransform.lossyScale;
            }
#else
            playerApi = Networking.LocalPlayer;
            VRCPlayerApi.TrackingData trackingData = playerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            transform.position = trackingData.position;
            transform.rotation = trackingData.rotation;

            UpdateCameraScale();
#endif

            UpdateTransforms();
        }

        private void UpdateCameraScale()
        {
            if (playerApi.IsUserInVR())
            {
                vrMeasureCamera.enabled = true;

                float headDelta;

                Quaternion playerRot = playerApi.GetRotation();
                Vector3 playerPos = playerApi.GetPosition();

                bool isUpright = Vector3.Dot(Vector3.up, playerRot * Vector3.up) > 0.98f;

                Vector3 currentVrPosition = vrMeasureCamera.transform.localPosition;
                Vector3 currentHeadPosition;

                float checkDelta = Mathf.LerpUnclamped(0.002f, 0.02f, Mathf.Abs(playerPos.y / 2000f));

                if (isUpright)
                {
                    currentHeadPosition = (playerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position - playerPos);
                    headDelta = currentHeadPosition.y - lastHeadPosition.y;

                    float absDelta = Mathf.Abs(headDelta);

                    if (absDelta > 0.5f)
                    {
                        lastHeadPosition = currentHeadPosition;
                    }
                    else if (Mathf.Abs(headDelta) > checkDelta)
                    {
                        float vrTrackerDelta = currentVrPosition.y - lastVRCamHeight;
                        float relativeScale = Mathf.Clamp(headDelta / vrTrackerDelta, 0.001f, 100f);

                        transform.localScale = new Vector3(relativeScale, relativeScale, relativeScale);
                        lastHeadPosition = currentHeadPosition;
                        lastVRCamHeight = currentVrPosition.y;
                    }
                }
                else // Fallback for if people are in stations
                {
                    //currentHeadPosition = new Plane(playerRot * Vector3.up, playerPos).GetDistanceToPoint(playerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position);

                    currentHeadPosition = Quaternion.Inverse(playerRot) * (playerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position - playerPos);

                    headDelta = (currentHeadPosition - lastHeadPosition).magnitude;

                    float absDelta = Mathf.Abs(headDelta);

                    if (absDelta > 0.5f)
                    {
                        lastHeadPosition = currentHeadPosition;
                    }
                    else if (Mathf.Abs(headDelta) > checkDelta)
                    {
                        float vrTrackerDelta = currentVrPosition.y - lastVRCamHeight;
                        float relativeScale = Mathf.Clamp(headDelta / vrTrackerDelta, 0.001f, 100f);

                        transform.localScale = new Vector3(relativeScale, relativeScale, relativeScale);
                        lastHeadPosition = currentHeadPosition;
                        lastVRCamHeight = currentVrPosition.y;
                    }
                }

                //debugOut.text = $"Scale: {transform.localScale.x:F5}\nCheck delta: {checkDelta:F5}";
            }
            else if (vrMeasureCamera.enabled) // Optimization for desktop players since the camera scale can always be 1.0 for desktop in our use case
                vrMeasureCamera.enabled = false;
        }

        private void UpdateTransforms()
        {
            Vector3 scale = transform.localScale;

            _cameraRoot.localScale = scale;
            _cameraRoot.rotation = Quaternion.identity;
            _cameraRoot.rotation = transform.rotation * Quaternion.Inverse(vrMeasureCamera.transform.rotation);
            _cameraRoot.position = transform.position - _cameraRoot.rotation * Vector3.Scale(vrMeasureCamera.transform.localPosition, scale);

#if !UNITY_EDITOR
            VRCPlayerApi.TrackingData trackingData = playerApi.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            _headRoot.SetPositionAndRotation(trackingData.position, trackingData.rotation);
            _headRoot.localScale = scale;

            if (playerApi.IsUserInVR())
            {
                _playspaceRoot.SetPositionAndRotation(_cameraRoot.position, _cameraRoot.rotation);
                _playerRoot.SetPositionAndRotation(playerApi.GetPosition(), _playspaceRoot.rotation);
            }
            else
            {
                _playspaceRoot.SetPositionAndRotation(playerApi.GetPosition(), playerApi.GetRotation());
                _playerRoot.SetPositionAndRotation(playerApi.GetPosition(), playerApi.GetRotation());
            }
            
            _playerRoot.localScale = scale;
#else
            if (editorRefCamera)
            {
                _headRoot.SetPositionAndRotation(editorRefCamera.transform.position, editorRefCamera.transform.rotation);
                _headRoot.localScale = scale;

                _playspaceRoot.SetPositionAndRotation(_cameraRoot.position, _cameraRoot.rotation);
            }
#endif

            _playspaceRoot.localScale = scale;
        }
    }
}
