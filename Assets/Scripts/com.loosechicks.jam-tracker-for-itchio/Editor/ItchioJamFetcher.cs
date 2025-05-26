using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace JamTrackerItchio.Editor
{
    public class ItchioJamFetcher
    {
        private const string JamsURL = "https://itch.io/jams";
        private const int RequestTimeout = 15;

        public static IEnumerator FetchJams(System.Action<List<GameJam>> callback)
        {
            using UnityWebRequest www = UnityWebRequest.Get(JamsURL);
            www.timeout = RequestTimeout;

            www.SetRequestHeader(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
            );

            var operation = www.SendWebRequest();
            float startTime = Time.realtimeSinceStartup;

            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - startTime > RequestTimeout)
                {
                    callback(new List<GameJam>());
                    yield break;
                }

                yield return null;
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                callback(new List<GameJam>());
            }
            else
            {
                try
                {
                    string htmlContent = www.downloadHandler.text;

                    if (string.IsNullOrEmpty(htmlContent))
                    {
                        callback(new List<GameJam>());
                        yield break;
                    }

                    List<GameJam> jams = ItchioJamParser.ParseFromHtml(htmlContent);

                    callback(jams);
                }
                catch (Exception)
                {
                    callback(new List<GameJam>());
                }
            }
        }
    }
}
