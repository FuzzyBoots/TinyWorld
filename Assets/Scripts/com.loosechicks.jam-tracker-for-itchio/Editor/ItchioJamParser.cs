using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamTrackerItchio.Editor
{
    public class ItchioJamParser
    {
        public static List<GameJam> ParseFromHtml(string html)
        {
            var jams = new List<GameJam>();

            try
            {
                if (string.IsNullOrEmpty(html))
                {
                    return jams;
                }

                string[] searchPatterns = new string[]
                {
                    "ReactDOM.render(R.Jam.FilteredJamCalendar({\"jams\":[",
                    "\"jams\":[",
                    "\"jams\":",
                    "data-calendar=",
                    "data-jams=",
                };

                int jamsArrayStart = -1;
                string matchedPattern = "";

                foreach (string pattern in searchPatterns)
                {
                    jamsArrayStart = html.IndexOf(pattern);

                    if (jamsArrayStart != -1)
                    {
                        matchedPattern = pattern;
                        break;
                    }
                }

                if (jamsArrayStart == -1)
                {
                    return jams;
                }

                int openBracketPos = html.IndexOf("[", jamsArrayStart);
                if (openBracketPos == -1)
                {
                    return jams;
                }

                int closeBracketPos = FindMatchingClosingBracket(html, openBracketPos);
                if (closeBracketPos == -1)
                {
                    return jams;
                }

                int jsonLength = closeBracketPos - openBracketPos + 1;
                string jamsJson = html.Substring(openBracketPos, jsonLength);

                try
                {
                    // Wrap the array in an object to match our deserialization structure
                    string wrappedJson = "{\"jams\":" + jamsJson + "}";
                    var jamObjects = JsonUtility.FromJson<JamsContainer>(wrappedJson).jams;

                    for (int i = 0; i < jamObjects.Length; i++)
                    {
                        try
                        {
                            var jamObj = jamObjects[i];
                            var jam = new GameJam
                            {
                                JamId = jamObj.ID,
                                Title = jamObj.Title,
                                Url = "https://itch.io" + jamObj.URL,
                                JoinedCount = jamObj.Joined,
                                IsHighlighted = jamObj.Highlight || false,
                                StartDate = ParseDateTime(jamObj.StartDate),
                                EndDate = ParseDateTime(jamObj.EndDate),
                            };

                            if (!string.IsNullOrEmpty(jamObj.VotingEndDate))
                            {
                                jam.VotingEndDate = ParseDateTime(jamObj.VotingEndDate);
                            }

                            jams.Add(jam);
                        }
                        catch (Exception)
                        {
                            // Skip malformed jam entries and continue processing others
                        }
                    }
                }
                catch (Exception)
                {
                    // Return any successfully parsed jams even if overall JSON structure is invalid
                }
            }
            catch (Exception)
            {
                // Return empty list if HTML parsing completely fails
            }

            return jams;
        }

        private static int FindMatchingClosingBracket(string text, int openBracketPos)
        {
            int depth = 0;
            for (int i = openBracketPos; i < text.Length; i++)
            {
                if (text[i] == '[')
                    depth++;
                else if (text[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private static DateTime ParseDateTime(string dateStr)
        {
            try
            {
                if (dateStr.EndsWith("Z"))
                {
                    // Z suffix indicates UTC time - convert to local
                    return DateTime.Parse(dateStr).ToLocalTime();
                }
                else if (dateStr.Contains("T"))
                {
                    // ISO format without Z but with T - assume UTC
                    dateStr = dateStr.Replace('T', ' ');
                    DateTime utcTime = DateTime.Parse(dateStr);
                    return DateTime.SpecifyKind(utcTime, DateTimeKind.Utc).ToLocalTime();
                }
                else
                {
                    // No timezone indicator - assume UTC for consistency
                    DateTime utcTime = DateTime.Parse(dateStr);
                    return DateTime.SpecifyKind(utcTime, DateTimeKind.Utc).ToLocalTime();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing date: {dateStr}, Error: {ex.Message}");
                return DateTime.Now; // Fallback to current time to avoid crashes
            }
        }

        [Serializable]
        private class JamsContainer
        {
            public JamObject[] jams;
        }

        [Serializable]
        private class JamObject
        {
            [SerializeField]
            private int id;

            [SerializeField]
            private string title;

            [SerializeField]
            private string url;

            [SerializeField]
            private string start_date;

            [SerializeField]
            private string end_date;

            [SerializeField]
            private string voting_end_date;

            [SerializeField]
            private int joined;

            [SerializeField]
            private bool highlight;

            public int ID => id;
            public string Title => title;
            public string URL => url;
            public string StartDate => start_date;
            public string EndDate => end_date;
            public string VotingEndDate => voting_end_date;
            public int Joined => joined;
            public bool Highlight => highlight;
        }
    }
}
