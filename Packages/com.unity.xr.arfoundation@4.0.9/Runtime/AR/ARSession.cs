using System;
using System.Collections;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace UnityEngine.XR.ARFoundation
{
    /// <summary>
    /// <para>
    /// Controls the lifecycle and configuration options for an AR session. There
    /// is only one active session. If you have multiple <see cref="ARSession"/> components,
    /// they all talk to the same session and will conflict with each other.
    /// </para><para>
    /// Enabling or disabling the <see cref="ARSession"/> will start or stop the session,
    /// respectively.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ARUpdateOrder.k_Session)]
    [HelpURL(HelpUrls.ApiWithNamespace + nameof(ARSession) + ".html")]
    public sealed class ARSession :
#if UNITY_2020_2_OR_NEWER
        SubsystemLifecycleManager<XRSessionSubsystem, XRSessionSubsystemDescriptor, XRSessionSubsystem.Provider>
#else
        SubsystemLifecycleManager<XRSessionSubsystem, XRSessionSubsystemDescriptor>
#endif
    {
        [SerializeField]
        [Tooltip("If enabled, the session will attempt to update a supported device if its AR software is out of date.")]
        bool m_AttemptUpdate = true;

        /// <summary>
        /// If the device supports XR but does not have the necessary software, some platforms
        /// allow prompting the user to install or update the software. If <see cref="attemptUpdate"/>
        /// is <c>true</c>, a software update will be attempted. If the appropriate software is not installed
        /// or out of date, and <see cref="attemptUpdate"/> is <c>false</c>, then AR will not be available.
        /// </summary>
        public bool attemptUpdate
        {
            get => m_AttemptUpdate;
            set => m_AttemptUpdate = value;
        }

        [SerializeField]
        [Tooltip("If enabled, the Unity frame will be synchronized with the AR session. Otherwise, the AR session will be updated independently of the Unity frame.")]
        bool m_MatchFrameRate = true;

        /// <summary>
        /// If <c>True</c>, the session will block execution until a new AR frame is available.
        /// This property is obsolete. Use <see cref="matchFrameRateRequested"/> or <see cref="matchFrameRateEnabled"/> instead.
        /// </summary>
        [Obsolete("Use matchFrameRateRequested or matchFrameRateEnabled instead. (2020-01-28)")]
        public bool matchFrameRate
        {
            get => m_MatchFrameRate;
            set => matchFrameRateRequested = value;
        }

        /// <summary>
        /// Whether the underlying subsystem will attempt to synchronize the AR frame rate with Unity's.
        /// </summary>
        /// <seealso cref="matchFrameRateRequested"/>
        public bool matchFrameRateEnabled => descriptor?.supportsMatchFrameRate == true ? subsystem.matchFrameRateEnabled : false;

        /// <summary>
        /// If <c>True</c>, the session will block execution until a new AR frame is available and set
        /// [Application.targetFrameRate](https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html)
        /// to match the native update frequency of the AR session.
        /// Otherwise, the AR session is updated indpendently of the Unity frame.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If enabled with a simple scene, the <c>ARSession.Update</c> may appear to take a long time.
        /// This is simply waiting for the next AR frame, similar to the way Unity will <c>WaitForTargetFPS</c> at the
        /// end of a frame. If the rest of the Unity frame takes non-trivial time, then the next <c>ARSession.Update</c>
        /// will take a proportionally less amount of time.
        /// </para><para>
        /// This option does three things:
        /// - Enables a setting on the <c>XRSessionSubsystem</c> which causes the update to block until the next AR frame is ready.
        /// - Sets <c>Application.targetFrameRate</c> to the session's preferred update rate.
        /// - Sets [QualitySettings.vSyncCount](https://docs.unity3d.com/ScriptReference/QualitySettings-vSyncCount.html) to zero
        /// </para>
        /// </remarks>
        public bool matchFrameRateRequested
        {
            get => descriptor?.supportsMatchFrameRate == true ? subsystem.matchFrameRateRequested : m_MatchFrameRate;
            set => SetMatchFrameRateRequested(value);
        }

        [SerializeField]
        [Tooltip("The requested tracking mode.")]
        TrackingMode m_TrackingMode = TrackingMode.PositionAndRotation;

        /// <summary>
        /// Get or set the <c>TrackingMode</c> for the session.
        /// </summary>
        public TrackingMode requestedTrackingMode
        {
            get => subsystem?.requestedTrackingMode.ToTrackingMode() ?? m_TrackingMode;
            set
            {
                m_TrackingMode = value;
                if (enabled && subsystem != null)
                {
                    subsystem.requestedTrackingMode = value.ToFeature();
                }
            }
        }

        /// <summary>
        /// Get the current <c>TrackingMode</c> in use by the session.
        /// </summary>
        public TrackingMode currentTrackingMode => subsystem?.currentTrackingMode.ToTrackingMode() ?? (TrackingMode)0;

        /// <summary>
        /// Get the number of AR frames produced per second, or null if the frame rate cannot be determined.
        /// </summary>
        public int? frameRate => (descriptor?.supportsMatchFrameRate ?? false) ? new Nullable<int>(subsystem.frameRate) : null;

        /// <summary>
        /// This event is invoked whenever the <see cref="state"/> changes.
        /// </summary>
        public static event Action<ARSessionStateChangedEventArgs> stateChanged;

        /// <summary>
        /// The state of the entire system. Use this to determine the status of AR availability and installation.
        /// </summary>
        public static ARSessionState state
        {
            get => s_State;
            private set
            {
                if (s_State == value)
                    return;

                s_State = value;

                UpdateNotTrackingReason();

                if (stateChanged != null)
                    stateChanged(new ARSessionStateChangedEventArgs(state));
            }
        }

        /// <summary>
        /// The reason AR tracking was lost.
        /// </summary>
        public static NotTrackingReason notTrackingReason => s_NotTrackingReason;

        /// <summary>
        /// Resets the AR Session.
        /// </summary>
        /// <remarks>
        /// Resetting the session destroys all trackables and resets device tracking (e.g., the position of the session
        /// is reset to the origin).
        /// </remarks>
        public void Reset()
        {
            if (subsystem != null)
                subsystem.Reset();

            if (state > ARSessionState.Ready)
                state = ARSessionState.SessionInitializing;
        }

        void SetMatchFrameRateRequested(bool value)
        {
            m_MatchFrameRate = value;
            if (descriptor?.supportsMatchFrameRate == true)
            {
                subsystem.matchFrameRateRequested = value;
            }
        }

        /// <summary>
        /// Emits a warning in the console if more than one active <see cref="ARSession"/>
        /// component is active. There is only a single, global AR Session; this
        /// component controls that session. If two or more <see cref="ARSession"/>s are
        /// simultaneously active, then they both issue commands to the same session.
        /// Although this can cause unintended behavior, it is not expressly forbidden.
        ///
        /// This method is expensive and should not be called frequently.
        /// </summary>
        void WarnIfMultipleARSessions()
        {
            var sessions = FindObjectsOfType<ARSession>();
            if (sessions.Length > 1)
            {
                // Compile a list of session names
                string sessionNames = "";
                foreach (var session in sessions)
                {
                    sessionNames += string.Format("\t{0}\n", session.name);
                }

                Debug.LogWarningFormat(
                    "Multiple active AR Sessions found. " +
                    "These will conflict with each other, so " +
                    "you should only have one active ARSession at a time. " +
                    "Found these active sessions:\n{0}", sessionNames);
            }
        }

        static XRSessionSubsystem GetSubsystem()
        {
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                {
                    return loader.GetLoadedSubsystem<XRSessionSubsystem>();
                }
            }

            return null;
        }

        /// <summary>
        /// Start checking the availability of XR on the current device.
        /// </summary>
        /// <remarks>
        /// The availability check may be asynchronous, so this is implemented as a coroutine.
        /// It is safe to call this multiple times; if called a second time while an availability
        /// check is being made, it returns a new coroutine which waits on the first.
        /// </remarks>
        /// <returns>An <c>IEnumerator</c> used for a coroutine.</returns>
        public static IEnumerator CheckAvailability()
        {
            // Wait if availability is currently being checked.
            while (state == ARSessionState.CheckingAvailability)
            {
                yield return null;
            }

            // Availability has already been determined if we make it here and the state is not None.
            if (state != ARSessionState.None)
                yield break;

            // Normally, the subsystem is created in OnEnable, but users may
            // want to check availability before enabling the session.
            var subsystem = GetSubsystem();
            if (subsystem == null)
            {
                // No subsystem means there is no support on this platform.
                state = ARSessionState.Unsupported;
            }
            else if (state == ARSessionState.None)
            {
                state = ARSessionState.CheckingAvailability;
                var availabilityPromise = subsystem.GetAvailabilityAsync();
                yield return availabilityPromise;
                s_Availability = availabilityPromise.result;

                if (s_Availability.IsSupported() && s_Availability.IsInstalled())
                {
                    state = ARSessionState.Ready;
                }
                else if (s_Availability.IsSupported() && !s_Availability.IsInstalled())
                {
                    bool supportsInstall =
#if UNITY_2020_2_OR_NEWER
                        subsystem.subsystemDescriptor.supportsInstall;
#else
                        subsystem.SubsystemDescriptor.supportsInstall;
#endif
                    state = supportsInstall ? ARSessionState.NeedsInstall : ARSessionState.Unsupported;
                }
                else
                {
                    state = ARSessionState.Unsupported;
                }
            }
        }

        /// <summary>
        /// Begin installing AR software on the current device (if supported).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Installation may be asynchronous, so this is implemented as a coroutine.
        /// It is safe to call this multiple times, but you must first call <see cref="CheckAvailability"/>.
        /// </para><para>
        /// You must call <see cref="CheckAvailability"/> before trying to Install
        /// and the <see cref="state"/> must not be <see cref="ARSessionState.Unsupported"/>
        /// or this method will throw.
        /// </para>
        /// </remarks>
        /// <returns>An <c>IEnumerator</c> used for a coroutine.</returns>
        public static IEnumerator Install()
        {
            while ((state == ARSessionState.Installing) || (state == ARSessionState.CheckingAvailability))
            {
                yield return null;
            }

            switch (state)
            {
                case ARSessionState.Installing:
                case ARSessionState.NeedsInstall:
                    break;
                case ARSessionState.None:
                    throw new InvalidOperationException("Cannot install until availability has been determined. Have you called CheckAvailability()?");
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                case ARSessionState.SessionTracking:
                    yield break;
                case ARSessionState.Unsupported:
                    throw new InvalidOperationException("Cannot install because XR is not supported on this platform.");
            }

            // We can't get this far without having had a valid subsystem at one point.
            var subsystem = GetSubsystem();
            if (subsystem == null)
                throw new InvalidOperationException("The subsystem was destroyed while attempting to install AR software.");

            state = ARSessionState.Installing;
            var installPromise = subsystem.InstallAsync();
            yield return installPromise;
            var installStatus = installPromise.result;

            switch (installStatus)
            {
                case SessionInstallationStatus.Success:
                    state = ARSessionState.Ready;
                    s_Availability = (s_Availability | SessionAvailability.Installed);
                    break;
                case SessionInstallationStatus.ErrorUserDeclined:
                    state = ARSessionState.NeedsInstall;
                    break;
                default:
                    state = ARSessionState.Unsupported;
                    break;
            }
        }

        /// <summary>
        /// Creates and initializes the session subsystem. Begins checking for availability.
        /// </summary>
        protected override void OnEnable()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            WarnIfMultipleARSessions();
#endif
            EnsureSubsystemInstanceSet();

            if (subsystem != null)
            {
                StartCoroutine(Initialize());
            }
#if DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarningFormat(
                    "No ARSession available for the current platform. " +
                    "Please ensure you have installed the relevant XR Plugin package " +
                    "for this platform via the Package Manager."
                );
            }
#endif
        }

        IEnumerator Initialize()
        {
            // Make sure we've checked for availability
            if (state <= ARSessionState.CheckingAvailability)
                yield return CheckAvailability();

            // Make sure we didn't get disabled while checking for availability
            if (!enabled)
                yield break;

            // Complete install if necessary
            if (((state == ARSessionState.NeedsInstall) && attemptUpdate) ||
                (state == ARSessionState.Installing))
            {
                yield return Install();
            }

            // If we're still enabled and everything is ready, then start.
            if (state == ARSessionState.Ready && enabled)
            {
                StartSubsystem();
            }
            else
            {
                enabled = false;
            }
        }

        void StartSubsystem()
        {
            SetMatchFrameRateRequested(m_MatchFrameRate);
            subsystem.requestedTrackingMode = m_TrackingMode.ToFeature();
            subsystem.Start();
        }

        void Awake()
        {
            s_NotTrackingReason = NotTrackingReason.None;
        }

        void Update()
        {
            if (subsystem?.running == true)
            {
                m_TrackingMode = subsystem.requestedTrackingMode.ToTrackingMode();
                if (subsystem.matchFrameRateEnabled && m_MatchFrameRate)
                {
                    Application.targetFrameRate = subsystem.frameRate;
                    QualitySettings.vSyncCount = 0;
                }

                subsystem.Update(new XRSessionUpdateParams
                {
                    screenOrientation = Screen.orientation,
                    screenDimensions = new Vector2Int(Screen.width, Screen.height)
                });

                switch (subsystem.trackingState)
                {
                    case TrackingState.None:
                    case TrackingState.Limited:
                        state = ARSessionState.SessionInitializing;
                        break;
                    case TrackingState.Tracking:
                        state = ARSessionState.SessionTracking;
                        break;
                }
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (subsystem == null)
                return;

            if (paused)
                subsystem.OnApplicationPause();
            else
                subsystem.OnApplicationResume();
        }

        /// <summary>
        /// Invoked when this `MonoBehaviour` is disabled. Used to affect the <see cref="state"/>.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            // Only set back to ready if we were previously running
            if (state > ARSessionState.Ready)
                state = ARSessionState.Ready;
        }

        /// <summary>
        /// Invoked when this `MonoBehaviour` is destroyed. Used to affect the <see cref="state"/>.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Only set back to ready if we were previously running
            if (state > ARSessionState.Ready)
                state = ARSessionState.Ready;
        }

        static void UpdateNotTrackingReason()
        {
            switch (state)
            {
                case ARSessionState.None:
                case ARSessionState.SessionInitializing:
                    s_NotTrackingReason = GetSubsystem()?.notTrackingReason ?? NotTrackingReason.Unsupported;
                    break;
                case ARSessionState.Unsupported:
                    s_NotTrackingReason = NotTrackingReason.Unsupported;
                    break;
                case ARSessionState.CheckingAvailability:
                case ARSessionState.NeedsInstall:
                case ARSessionState.Installing:
                case ARSessionState.Ready:
                case ARSessionState.SessionTracking:
                    s_NotTrackingReason = NotTrackingReason.None;
                    break;
            }
        }

        // Internal for tests
        internal static ARSessionState s_State;

        static NotTrackingReason s_NotTrackingReason;

        static SessionAvailability s_Availability;
    }
}
