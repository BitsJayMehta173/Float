using System.Collections.Generic;

namespace FloatingReminder
{
    /// <summary>
    /// Holds application-wide state, including the active user session and SyncManager instance.
    /// </summary>
    public static class AppState
    {
        public static SyncManager CurrentSyncManager { get; private set; }
        public static string CurrentUsername { get; private set; }

        /// <summary>
        /// Initializes the SyncManager after a successful login.
        /// </summary>
        /// <param name="username">The user who successfully logged in.</param>
        /// <param name="onRemoteChangeCallback">The action the SyncManager calls on remote change.</param>
        public static void InitializeSession(string username, System.Action onRemoteChangeCallback)
        {
            // First, stop any existing sync session
            CleanupSession();

            CurrentUsername = username;

            // Initialize and start the SyncManager
            CurrentSyncManager = new SyncManager(CurrentUsername, onRemoteChangeCallback);
            CurrentSyncManager.StartSync();
        }

        /// <summary>
        /// Cleans up the session by stopping the SyncManager. Called on logout or application exit.
        /// </summary>
        public static void CleanupSession()
        {
            if (CurrentSyncManager != null)
            {
                CurrentSyncManager.StopSync();
                CurrentSyncManager = null;
            }
            CurrentUsername = null;
        }
    }
}