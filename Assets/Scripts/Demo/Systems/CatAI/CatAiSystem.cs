using System;
using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void SpawnCat()
        {
            Vector3 catSpawn = new Vector3(CatSpawnWorldX, _riverCenter.y + 0.8f, 0f);

            _cat = FindExistingCat();
            if (_cat == null)
            {
                _cat = new GameObject("Cat", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D));
            }

            _cat.transform.position = catSpawn;

            _catRenderer = _cat.GetComponent<SpriteRenderer>();
            if (_catRenderer == null)
                _catRenderer = _cat.AddComponent<SpriteRenderer>();

            Sprite resolved = ResolveCatSprite();
            if (_catRenderer.sprite == null && resolved != null)
                _catRenderer.sprite = resolved;

            _catRenderer.color = Color.white;
            _catRenderer.sortingOrder = 40;

            if (_catRenderer.sprite == null)
            {
                _catRenderer.sprite = WhiteSprite;
                _catRenderer.color = new Color(1f, 0.6f, 0.2f, 1f);
                _cat.transform.localScale = new Vector3(0.6f, 0.4f, 1f);
            }
            else
            {
                _cat.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            }

            CircleCollider2D catCollider = _cat.GetComponent<CircleCollider2D>();
            if (catCollider == null)
                catCollider = _cat.AddComponent<CircleCollider2D>();
            catCollider.radius = 0.35f;
            catCollider.isTrigger = false;

            Rigidbody2D body = _cat.GetComponent<Rigidbody2D>();
            if (body == null)
                body = _cat.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            float catMinX = _riverCenter.x + 0.8f;
            _catBounds = new Rect(
                catMinX,
                _worldBounds.yMin + 0.5f,
                (_worldBounds.xMax - 0.5f) - catMinX,
                _worldBounds.height - 1f
            );
        }

        private GameObject FindExistingCat()
        {
            SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.gameObject == null) continue;
                if (renderer.gameObject.name.IndexOf("cat", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (renderer.gameObject == gameObject) continue;
                return renderer.gameObject;
            }

            return null;
        }

        private Sprite ResolveCatSprite()
        {
#if UNITY_EDITOR
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/NPCs/Cat/Cat Idle00001.png");
            if (sprite != null) return sprite;
#endif
            return null;
        }

        private AudioClip CreateMimicClip()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float frequency = 620f;

            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.2f;
            }

            AudioClip clip = AudioClip.Create("MimicPing", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void UpdateCatBehavior()
        {
            if (_cat == null || _player == null) return;

            if (Time.time >= _nextPawPrintAt)
            {
                SpawnPawPrint();
                _nextPawPrintAt = Time.time + PawPrintInterval;
            }

            Vector3 catPos = _cat.transform.position;
            Vector3 playerPos = _player.position;

            if (Time.time < _catFrozenUntil)
            {
                _catState = CatState.Frozen;
            }
            else if (Time.time < _catFollowUntil)
            {
                _catState = CatState.Following;
            }
            else if (Vector2.Distance(catPos, playerPos) <= GetEffectiveFleeRadius())
            {
                _catState = CatState.Fleeing;
            }
            else
            {
                _catState = CatState.Idle;
            }

            Vector3 moveDir = Vector3.zero;
            switch (_catState)
            {
                case CatState.Following:
                    moveDir = (playerPos - catPos).normalized;
                    break;
                case CatState.Fleeing:
                    moveDir = (catPos - playerPos).normalized;
                    if (moveDir.sqrMagnitude < 0.001f)
                        moveDir = new Vector3(1f, 0f, 0f);
                    break;
            }

            if (moveDir.sqrMagnitude > 0.001f)
            {
                catPos += moveDir * (2.2f * Time.deltaTime);
                catPos.x = Mathf.Clamp(catPos.x, _catBounds.xMin, _catBounds.xMax);
                catPos.y = Mathf.Clamp(catPos.y, _catBounds.yMin, _catBounds.yMax);
                _cat.transform.position = catPos;
            }
        }

        private void TryTriggerWin()
        {
            if (_winShown || _cat == null || _player == null) return;

            float dist = Vector2.Distance(_player.position, _cat.transform.position);
            if (_collectedSkillCount < 3)
            {
                if (dist <= 1.2f && Time.time >= _nextRequirementToastAt)
                {
                    ShowToast("Collect at least 3 skills first.");
                    _nextRequirementToastAt = Time.time + 1.5f;
                }
                return;
            }

            if (_catState == CatState.Fleeing) return;
            if (dist > 1.2f) return;

            _winShown = true;
            _catFrozenUntil = Time.time + 999f;
            var playerController = _player.GetComponent<PlayerController>();
            if (playerController != null) playerController.enabled = false;

            if (_winPanel != null) _winPanel.SetActive(true);
            ShowToast("You found the cat!");
        }
    }
}