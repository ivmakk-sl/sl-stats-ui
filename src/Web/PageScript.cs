using System;
using System.Collections.Generic;
using GameCore.HotUpdate.ReduxUI;
using UnityEngine;

namespace StatsUI
{
    // Runs page.js (an embedded resource) in the root page, which reaches the CoreUI1 iframe itself. The
    // full script goes only when the root page does not have it (a new root page, or one that the game
    // built again after a browser crash); each other send holds only the data or the check.
    internal static class PageScript
    {
        private static string script;
        private static readonly HashSet<string> loggedMissing = new HashSet<string>();
        private static readonly HashSet<string> loggedOther = new HashSet<string>();

        // The Vite bundle, embedded by StatsUI.csproj under this name.
        private static string Script()
        {
            if (script != null) return script;
            using (var stream = typeof(PageScript).Assembly.GetManifestResourceStream("StatsUI.page.js"))
            using (var reader = new System.IO.StreamReader(stream))
                script = reader.ReadToEnd();
            return script;
        }

        private static Vuplex.WebView.IWebView WebView() =>
            ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        // A full send whose result has not come back yet blocks a second one, for at most this long: a
        // browser crash drops the result.
        private const float FullSendWaitSeconds = 5f;
        private static float fullSendAt = float.NegativeInfinity;
        // The number of the last full send: only its result opens the gate, not a late one of an older send.
        private static int fullSendId;

        // Sends the data alone when the root page has the page script, and the script with the data when
        // it does not. setData applies once when the CoreUI1 frame is there; when it is not, PageTick
        // sends again one real second later. reason is the Verbose text of the push.
        public static void SetData(string json, string reason)
        {
            var webView = WebView();
            if (webView == null)
            {
                PageTick.ForgetLastPush();
                return;
            }
            webView.ExecuteJavaScript(PageJson.SetDataCommand(json, BuffsReader.GameNow()), (Il2CppSystem.Action<string>)(r =>
            {
                if (r == PageJson.NoScript) SendWithScript(json, reason);
                else OnSetDataResult(r, reason);
            }));
        }

        // A browser crash or a CoreUI1 frame built again drops the mod nodes while the data stays the same, so
        // no push would come. A root page with no script gets the script with the last data at once; another
        // answer that is not ok makes PageTick push again.
        public static void Check()
        {
            var webView = WebView();
            if (webView == null) return;
            webView.ExecuteJavaScript(PageJson.CheckCommand(BuffsReader.GameNow()), (Il2CppSystem.Action<string>)(r =>
            {
                if (r == PageJson.NoScript)
                {
                    string json = PageTick.LastJson;
                    if (json != null) SendWithScript(json, "check found no script");
                    return;
                }
                var (ok, runs, passes, passMicros) = PushSchedule.IsOk(r);
                if (ok)
                {
                    LogMissing(r);
                    PageTick.OnCheckCounts(runs, passes, passMicros);
                    return;
                }
                PageTick.ForgetLastPush();
                Plugin.Debug($"Stats UI page check: {r}");
            }));
        }

        private static void SendWithScript(string json, string reason)
        {
            var webView = WebView();
            float now = Time.realtimeSinceStartup;
            if (webView == null || now - fullSendAt < FullSendWaitSeconds)
            {
                PageTick.ForgetLastPush();
                return;
            }
            fullSendAt = now;
            int id = ++fullSendId;
            Plugin.Debug("Stats UI page script: sent");
            webView.ExecuteJavaScript(PageJson.SetDataWithScriptCommand(Script(), json, BuffsReader.GameNow()), (Il2CppSystem.Action<string>)(r =>
            {
                if (id == fullSendId) fullSendAt = float.NegativeInfinity;
                OnSetDataResult(r, reason);
            }));
        }

        private static void OnSetDataResult(string r, string reason)
        {
            // No result means the call did not run (for example a browser crash).
            if (r == null || r == "no CoreUI1 frame") PageTick.ForgetLastPush();
            LogPageCheck(r);
            Plugin.Debug($"Stats UI push: {reason} result={r}");
        }

        // page.js's result is "installed", "installed; missing: <parts>", "no CoreUI1 frame", or
        // "error: ...". Only a missing part or an error logs a warning, and each distinct message logs once, so a
        // game update is not silent but a normal call does not spam the log.
        private static void LogPageCheck(string r)
        {
            // No CoreUI1 frame is normal while the main menu or a save load is on screen.
            if (r == null || r == "installed" || r == "no CoreUI1 frame") return;
            if (LogMissing(r)) return;
            if (loggedOther.Add(r)) Plugin.Log.LogWarning($"Stats UI page check: {r}");
        }

        // One warning for each game part that the page misses, the first time it misses it. True when the answer
        // names a missing part.
        private static bool LogMissing(string r)
        {
            var parts = PushSchedule.MissingParts(r);
            foreach (string part in parts)
                if (loggedMissing.Add(part)) Plugin.Log.LogWarning($"Stats UI page check: missing {part}");
            return parts.Count > 0;
        }
    }
}
