using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Reallusion.Import
{
    // invoke with FrameTimer.CreateTimer(frameCount, uniqueId, Callback);
    // minimally needs: Callback(object obj, FrameTimerArgs args) { FrameTimer.OnFrameTimerComplete -= Callback; }

    public class FrameTimerArgs : EventArgs
    {
        public int ident { get; set; }

        public FrameTimerArgs(int id)
        {
            ident = id;
        }
    }

    public class FrameTimer
    {
        int frameCount = 0;
        int timerId = 0;
        public static event EventHandler<FrameTimerArgs> OnFrameTimerComplete;
        public static List<FrameTimer> frameTimers;

        public FrameTimer()
        {
            if (frameTimers == null) frameTimers = new List<FrameTimer>();
        }

        public static FrameTimer CreateTimer(int i, int id, EventHandler<FrameTimerArgs> func)
        {
            if (frameTimers != null)
            {
                if (frameTimers.Exists(n => n.timerId == id))
                {
                    return frameTimers.Find(n => n.timerId == id);
                }
            }

            FrameTimer timer = new FrameTimer();
            timer.StartTimer(i, id);
            frameTimers.Add(timer);
            OnFrameTimerComplete += func;

            return timer;
        }

        public void StartTimer(int i, int id)
        {
            frameCount = i;
            timerId = id;
            EditorApplication.update += WaitForFrames;
        }

        private void WaitForFrames()
        {
            while (frameCount > 0)
            {
                frameCount--;
                return;
            }
            // clean up
            FramesCompleted();
            EditorApplication.update -= WaitForFrames;
            frameTimers.Remove(this);
        }

        private void FramesCompleted()
        {
            OnFrameTimerComplete.Invoke(null, new FrameTimerArgs(timerId));
        }

        public static int initShaderUpdater = 1111;
        public static int onAfterAssemblyReload = 1212;

    }
}
