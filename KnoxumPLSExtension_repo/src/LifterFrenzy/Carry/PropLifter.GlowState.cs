using System;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// The glowing outline that marks the prop under the crosshair.
    /// </summary>
    public sealed partial class PropLifter
    {


        internal sealed class GlowState
        {
            // BB+ shaders use _TextureColor (MaterialModifier.SetColor writes this property).
            private static readonly int TextureColorId = Shader.PropertyToID("_TextureColor");
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

            private readonly Transform _root;
            private readonly Renderer[] _renderers;
            private readonly SpriteRenderer[] _spriteRenderers;
            private readonly Color[] _originalSpriteColors;
            private readonly MaterialPropertyBlock[][] _originalBlocks;
            private readonly int[][] _tintPropertyIds;
            private readonly bool[][] _hasEmission;
            private readonly Color[][] _baseColors;
            private readonly GameObject _lightObject;
            private readonly Light _light;
            private const float ReapplyInterval = 0.15f;
            private bool _restored;
            private Color _appliedColor;
            private bool _hasAppliedColor;
            private float _appliedTime;

            public GlowState(Transform root, Renderer[] renderers)
            {
                _root = root;
                _renderers = renderers ?? new Renderer[0];
                _spriteRenderers = new SpriteRenderer[_renderers.Length];
                _originalSpriteColors = new Color[_renderers.Length];
                _originalBlocks = new MaterialPropertyBlock[_renderers.Length][];
                _tintPropertyIds = new int[_renderers.Length][];
                _hasEmission = new bool[_renderers.Length][];
                _baseColors = new Color[_renderers.Length][];

                Bounds bounds;
                bool hasBounds = CombinedBounds(_renderers, out bounds);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                    if (spriteRenderer != null)
                    {
                        _spriteRenderers[i] = spriteRenderer;
                        _originalSpriteColors[i] = spriteRenderer.color;
                        // SpriteRenderer owns per-sprite texture/color state; never snapshot or
                        // reapply a material property block for it.
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    int materialCount = materials != null ? materials.Length : 0;
                    _originalBlocks[i] = new MaterialPropertyBlock[materialCount];
                    _tintPropertyIds[i] = new int[materialCount];
                    _hasEmission[i] = new bool[materialCount];
                    _baseColors[i] = new Color[materialCount];

                    for (int j = 0; j < materialCount; j++)
                    {
                        MaterialPropertyBlock original = new MaterialPropertyBlock();
                        // BB+ MaterialModifier stores per-material blocks, so capture/restore those too.
                        renderer.GetPropertyBlock(original, j);
                        _originalBlocks[i][j] = original;

                        Material material = materials[j];
                        if (material == null)
                        {
                            continue;
                        }

                        if (material.HasProperty(TextureColorId))
                        {
                            _tintPropertyIds[i][j] = TextureColorId;
                        }
                        else if (material.HasProperty(BaseColorId))
                        {
                            _tintPropertyIds[i][j] = BaseColorId;
                        }
                        else if (material.HasProperty(ColorId))
                        {
                            _tintPropertyIds[i][j] = ColorId;
                        }

                        if (_tintPropertyIds[i][j] != 0)
                        {
                            _baseColors[i][j] = material.GetColor(_tintPropertyIds[i][j]);
                        }

                        _hasEmission[i][j] = material.HasProperty(EmissionColorId);
                    }
                }

                if (_root != null && hasBounds)
                {
                    _lightObject = new GameObject(GlowObjectName);
                    _lightObject.transform.SetParent(_root, false);
                    _lightObject.transform.localPosition = _root.InverseTransformPoint(bounds.center);
                    _light = _lightObject.AddComponent<Light>();
                    _light.type = LightType.Point;
                    _light.range = Mathf.Clamp(bounds.extents.magnitude * 2.5f, 3f, 8f);
                    _light.intensity = 0.25f;
                    _light.shadows = LightShadows.None;
                }
            }

            public void SetColor(Color color)
            {
                if (_restored)
                {
                    return;
                }

                // The highlight is now refreshed every frame; only touch the property blocks when the
                // color changes or periodically (other systems may rewrite the blocks), not 60x a second.
                float now = Time.unscaledTime;
                if (_hasAppliedColor && color == _appliedColor && now - _appliedTime < ReapplyInterval)
                {
                    return;
                }

                _hasAppliedColor = true;
                _appliedColor = color;
                _appliedTime = now;
                if (_light != null)
                {
                    _light.color = color;
                }

                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    // SpriteRenderer's visible tint is its color property (including BB+ sprite shaders),
                    // not necessarily a material color property. Avoid rewriting sprite material blocks.
                    if (_spriteRenderers[i] != null)
                    {
                        _spriteRenderers[i].color = Color.Lerp(
                            _originalSpriteColors[i], color, 0.22f);
                        continue;
                    }

                    if (_originalBlocks[i] == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < _originalBlocks[i].Length; j++)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, j);
                        int tintPropertyId = _tintPropertyIds[i][j];
                        if (tintPropertyId != 0)
                        {
                            // A restrained tint keeps the original image/texture visible instead of bleaching it white.
                            block.SetColor(tintPropertyId, Color.Lerp(_baseColors[i][j], color, 0.22f));
                        }

                        if (_hasEmission[i][j])
                        {
                            block.SetColor(EmissionColorId, color * 0.12f);
                        }

                        renderer.SetPropertyBlock(block, j);
                    }
                }
            }

            public void Restore()
            {
                if (_restored)
                {
                    return;
                }

                _restored = true;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null)
                    {
                        continue;
                    }

                    if (_spriteRenderers[i] != null)
                    {
                        _spriteRenderers[i].color = _originalSpriteColors[i];
                        continue;
                    }

                    if (_originalBlocks[i] == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < _originalBlocks[i].Length; j++)
                    {
                        _renderers[i].SetPropertyBlock(_originalBlocks[i][j], j);
                    }
                }

                if (_lightObject != null)
                {
                    UnityEngine.Object.Destroy(_lightObject);
                }
            }
        }
    }
}
