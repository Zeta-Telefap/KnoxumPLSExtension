using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Student Shuffle: a prop that lands on a student's head rides with the crowd.
    /// </summary>
    public sealed partial class PropLifter
    {


        // Student Shuffle.
        private const float CrowdCatchRadius = 3.5f;
        private const float CrowdCatchAbove = 0.5f;
        private const float CrowdHeadOffsetDefault = 3.5f;
        private const float CrowdLiftSpeed = 20f;
        private const float CrowdCenterSpeed = 4f;
        private float _crowdHeadOffset = CrowdHeadOffsetDefault;
        private bool _crowdRunning;
        private StudentCrowd_Follower[] _followers = new StudentCrowd_Follower[0];
        private StudentEvent _studentEvent;

        /// <summary>
        /// The students of the crowd are children of the student event. The event is remembered until the last
        /// of them is gone: it is no longer "running" while the crowd walks out, and a ride goes on until then.
        /// </summary>
        private void RefreshFollowers(StudentEvent crowd)
        {
            if (crowd != null)
            {
                _studentEvent = crowd;
            }

            StudentCrowd_Follower[] followers = _studentEvent != null
                ? _studentEvent.GetComponentsInChildren<StudentCrowd_Follower>()
                : new StudentCrowd_Follower[0];
            if (followers.Length == 0 && crowd == null)
            {
                _studentEvent = null;
            }

            _followers = followers;
            LogEventObjects("Student Shuffle: students", followers.Length, ref _loggedStudents);
            for (int i = 0; i < _followers.Length; i++)
            {
                StudentCrowd_Follower follower = _followers[i];
                Renderer sprite = follower != null ? follower.GetComponentInChildren<Renderer>() : null;
                if (sprite != null && sprite.enabled)
                {
                    float top = sprite.bounds.max.y - follower.transform.position.y;
                    if (top > 1f && top < 6f)
                    {
                        _crowdHeadOffset = top;
                    }

                    break;
                }
            }
        }

        private StudentCrowd_Follower FindFollowerUnder(EnvironmentController environment, Vector3 c, Vector3 half)
        {
            // A prop as tall as the room cannot sit on a head under the ceiling; nor one that is at a spawner room.
            if (_followers.Length == 0 || 2f * half.y + 0.2f >= FlightCeilingHeight
                || BoxTouchesEventSpawner(environment, c, half))
            {
                return null;
            }

            float reach = CrowdCatchRadius + Mathf.Max(half.x, half.z) * 0.5f;
            float bestSqr = reach * reach;
            StudentCrowd_Follower best = null;
            for (int i = 0; i < _followers.Length; i++)
            {
                StudentCrowd_Follower follower = _followers[i];
                if (follower == null || !follower.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 position = follower.transform.position;
                float dx = position.x - c.x;
                float dz = position.z - c.z;
                float sqr = dx * dx + dz * dz;
                Cell followerCell;
                if (sqr >= bestSqr || c.y - half.y > position.y + _crowdHeadOffset + CrowdCatchAbove
                    || (TryGetCell(environment, position, out followerCell) && IsEventSpawnerCell(followerCell)))
                {
                    continue;
                }

                best = follower;
                bestSqr = sqr;
            }

            return best;
        }
    }
}
