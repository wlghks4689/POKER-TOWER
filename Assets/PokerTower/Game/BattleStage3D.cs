using System.Collections;
using UnityEngine;

namespace PokerTower.Game
{
    /// <summary>
    /// 3D 전장 (19장 [확정] 2.5D: 3D 전장 + 2D 카드 UI, 18.1 [확정] 1인칭 시점).
    /// 정식 아트가 나오기 전까지 기본 도형으로 공간과 연출 타이밍만 잡아 둡니다.
    /// 연출은 규칙 판정과 분리되어 있고, 결과값만 받아서 보여줍니다.
    /// </summary>
    public sealed class BattleStage3D : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Transform EnemyRoot { get; private set; }

        private Renderer[] _enemyRenderers;
        private Color _enemyBaseColor;
        private Vector3 _enemyHome;
        private Vector3 _camHome;
        private float _bobPhase;
        private bool _busy;

        private static readonly Vector3 EnemyPos = new Vector3(0f, 0f, 7f);

        public void Build()
        {
            // 카메라: 씬에 있으면 재사용, 없으면 생성
            Cam = Camera.main;
            if (Cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                Cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            // 조금 뒤·위에서 내려다봄: 에너미 모델이 화면(1080 기준) 아래 약 644~846px에 놓여
            // 사용 카드 존 위의 족보 이름 줄과 겹치지 않고, 에너미 정보 패널 바로 아래에 옴 (기획서 18.3 표시 정리)
            Cam.transform.position = new Vector3(0f, 3.0f, -5f);
            Cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            Cam.fieldOfView = 55f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.05f, 0.05f, 0.09f);
            _camHome = Cam.transform.position;

            if (FindLight() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.28f, 0.27f, 0.35f);

            var root = new GameObject("Battlefield").transform;
            root.SetParent(transform, false);

            // 바닥과 탑 내부 기둥
            var floor = Prim(PrimitiveType.Plane, root, "Floor", new Vector3(0, 0, 6), Vector3.one * 2.5f,
                             new Color(0.17f, 0.16f, 0.2f));
            floor.transform.rotation = Quaternion.identity;
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = 3f + (i / 2) * 4f;
                Prim(PrimitiveType.Cube, root, "Pillar", new Vector3(side * 5.5f, 3f, z), new Vector3(0.9f, 6f, 0.9f),
                     new Color(0.24f, 0.22f, 0.3f));
            }
            Prim(PrimitiveType.Cube, root, "BackWall", new Vector3(0, 4f, 15f), new Vector3(14f, 8f, 0.5f),
                 new Color(0.12f, 0.11f, 0.16f));
        }

        private static Light FindLight()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            var go = GameObject.Find("Directional Light");
            return go != null ? go.GetComponent<Light>() : null;
        }

        /// <summary>층마다 에너미 모형을 새로 만듭니다. 보스는 크고 붉게.</summary>
        public void SpawnEnemy(int floor, bool boss)
        {
            if (EnemyRoot != null) Destroy(EnemyRoot.gameObject);
            EnemyRoot = new GameObject("Enemy").transform;
            EnemyRoot.SetParent(transform, false);
            EnemyRoot.position = EnemyPos;
            float scale = boss ? 1.7f : 1f;
            EnemyRoot.localScale = Vector3.one * scale;

            float hue = (floor * 0.137f) % 1f;
            _enemyBaseColor = boss ? new Color(0.75f, 0.12f, 0.15f) : Color.HSVToRGB(hue, 0.55f, 0.75f);

            var body = Prim(PrimitiveType.Capsule, EnemyRoot, "Body", new Vector3(0, 1.1f, 0), new Vector3(1.2f, 1.1f, 1.2f), _enemyBaseColor);
            var eyeColor = new Color(1f, 0.95f, 0.6f);
            Prim(PrimitiveType.Sphere, EnemyRoot, "EyeL", new Vector3(-0.25f, 1.55f, -0.52f), Vector3.one * 0.18f, eyeColor);
            Prim(PrimitiveType.Sphere, EnemyRoot, "EyeR", new Vector3(0.25f, 1.55f, -0.52f), Vector3.one * 0.18f, eyeColor);
            if (boss)
            {
                Prim(PrimitiveType.Cube, EnemyRoot, "HornL", new Vector3(-0.4f, 2.35f, 0), new Vector3(0.15f, 0.5f, 0.15f), Color.black);
                Prim(PrimitiveType.Cube, EnemyRoot, "HornR", new Vector3(0.4f, 2.35f, 0), new Vector3(0.15f, 0.5f, 0.15f), Color.black);
            }
            _enemyRenderers = new[] { body.GetComponent<Renderer>() };
            _enemyHome = EnemyRoot.position;
        }

        private void Update()
        {
            if (EnemyRoot == null || _busy) return;
            _bobPhase += Time.deltaTime * 2f;
            EnemyRoot.position = _enemyHome + new Vector3(0, Mathf.Sin(_bobPhase) * 0.06f, 0);
        }

        /// <summary>에너미 화면 좌표 (UI 피해 숫자 위치용).</summary>
        public Vector3 EnemyScreenPoint(float heightOffset = 1.6f)
        {
            if (EnemyRoot == null || Cam == null) return new Vector3(Screen.width / 2f, Screen.height * 0.6f, 0);
            return Cam.WorldToScreenPoint(EnemyRoot.position + Vector3.up * heightOffset * EnemyRoot.localScale.y);
        }

        public IEnumerator PlayEnemyHit(Color flash, float strength)
        {
            if (EnemyRoot == null) yield break;
            _busy = true;
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - t / dur;
                SetEnemyColor(Color.Lerp(_enemyBaseColor, flash, k));
                EnemyRoot.position = _enemyHome + new Vector3(Mathf.Sin(t * 60f) * 0.08f * strength * k, 0, 0.35f * strength * k);
                yield return null;
            }
            SetEnemyColor(_enemyBaseColor);
            EnemyRoot.position = _enemyHome;
            _busy = false;
        }

        public IEnumerator PlayEnemyAttack()
        {
            if (EnemyRoot == null) yield break;
            _busy = true;
            float t = 0f;
            const float dur = 0.28f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                EnemyRoot.position = _enemyHome + new Vector3(0, 0, -3.2f * k);
                yield return null;
            }
            EnemyRoot.position = _enemyHome;
            _busy = false;
        }

        public IEnumerator PlayEnemyBuff(Color c)
        {
            if (EnemyRoot == null) yield break;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                SetEnemyColor(Color.Lerp(_enemyBaseColor, c, Mathf.Sin(t / 0.5f * Mathf.PI)));
                yield return null;
            }
            SetEnemyColor(_enemyBaseColor);
        }

        public IEnumerator ShakeCamera(float strength, float duration)
        {
            if (Cam == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - t / duration;
                Cam.transform.position = _camHome + (Vector3)(Random.insideUnitCircle * strength * k);
                yield return null;
            }
            Cam.transform.position = _camHome;
        }

        public IEnumerator PlayEnemyDeath()
        {
            if (EnemyRoot == null) yield break;
            _busy = true;
            float t = 0f;
            Vector3 s0 = EnemyRoot.localScale;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = 1f - t / 0.6f;
                EnemyRoot.localScale = new Vector3(s0.x * (1f + (1f - k) * 0.3f), s0.y * k, s0.z * (1f + (1f - k) * 0.3f));
                yield return null;
            }
            EnemyRoot.gameObject.SetActive(false);
            _busy = false;
        }

        private void SetEnemyColor(Color c)
        {
            if (_enemyRenderers == null) return;
            foreach (var r in _enemyRenderers) if (r != null) r.material.color = c;
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color; // Built-in·URP 모두 기본 머티리얼 복제본에 색만 지정
            return go;
        }
    }
}
