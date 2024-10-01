using System;
using UnityEditor;

namespace Reallusion.Import
{
    public class UpdateManager
    {
        public static event EventHandler UpdateChecksComplete;
        //    wrinkle manager to runtime inside project then access from build materials by reflection
        private static void WaitForUpdateCheckCompletion()
        {
            bool gotPackages = determinationStatus.HasFlag(ActivityStatus.GotPackages);
            bool gotHttp = determinationStatus.HasFlag(ActivityStatus.GotHttp);

            if (gotPackages && gotHttp)
                UpdateChecksComplete.Invoke(null, null);
        }

        [Flags]
        public enum ActivityStatus
        {
            None = 0,
            DeterminingPackages = 1,
            GotPackages = 2,
            DeterminingHttp = 4,
            GotHttp = 8
        }

        private static ActivityStatus determinationStatus = ActivityStatus.None;

        public static ActivityStatus DeterminationStatus { get { return determinationStatus; } }

        public static void SetDeterminationStatusFlag(ActivityStatus flag, bool value)
        {
            if (value)
            {
                if (!determinationStatus.HasFlag(flag))
                {
                    determinationStatus |= flag; // toggle changed to ON => bitwise OR to add flag                    
                }
            }
            else
            {
                if (determinationStatus.HasFlag(flag))
                {
                    determinationStatus ^= flag; // toggle changed to OFF => bitwise XOR to remove flag
                }
            }
        }

    }
}
