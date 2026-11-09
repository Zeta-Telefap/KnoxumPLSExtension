using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The squash of Entity.Squish on a prop (vertical scale 0.5; during the last 3 s sin((3 - t) * 16 pi) * 0.2 + 0.5,
    /// as Entity.Update does). It restores the original scale and removes itself when the time is up.
    /// </summary>
    internal sealed class PropFx : MonoBehaviour
    {
        private const float SquishFactor = 0.5f;
        private const float WobbleSeconds = 3f;

        private Vector3 _baseScale = Vector3.one;
        private bool _captured;
        private float _squishTime;

        public bool Squished
        {
            get { return _squishTime > 0f; }
        }

        public static PropFx GetOrAdd(Transform root)
        {
            PropFx fx = root.GetComponent<PropFx>();
            if (fx == null || !fx.enabled)
            {
                fx = root.gameObject.AddComponent<PropFx>();
            }

            return fx;
        }

        public void Squish(float seconds)
        {
            Capture();
            _squishTime = Mathf.Max(seconds, _squishTime);
            Apply();
        }

        private void Capture()
        {
            if (!_captured)
            {
                _baseScale = transform.localScale;
                _captured = true;
            }
        }

        private void Update()
        {
            if (_squishTime > 0f)
            {
                _squishTime -= Time.deltaTime;
            }

            if (_squishTime <= 0f)
            {
                if (_captured)
                {
                    transform.localScale = _baseScale;
                    _captured = false;
                }

                enabled = false;
                Destroy(this);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            float squash = _squishTime <= WobbleSeconds
                ? Mathf.Sin((WobbleSeconds - _squishTime) * 16f * Mathf.PI) * 0.2f + SquishFactor
                : SquishFactor;
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * squash, _baseScale.z);
        }
    }
}
