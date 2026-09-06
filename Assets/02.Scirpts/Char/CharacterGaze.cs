using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 캐릭터가 플레이어(또는 지정 타깃)를 바라보게 한다.
    ///
    /// Animator IK(SetLookAtPosition)를 쓰지 않고 본을 직접 회전시킨다.
    /// Humanoid 릭이 아니어도 동작하며, 나중에 Humanoid로 바꿔도 그대로 쓸 수 있다.
    ///
    /// 회전은 캐릭터 루트 기준 축(up/right)에 대한 월드 델타로 적용하므로
    /// 본의 로컬 축 방향(MMD 모델은 제각각이다)에 영향받지 않는다.
    ///
    /// 실행 순서: Animator가 포즈를 쓴 뒤에 덮어써야 하므로 LateUpdate에서 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterGaze : MonoBehaviour
    {
        [Header("타깃")]
        [Tooltip("바라볼 대상. 비워두면 Camera.main(= XR Origin의 Main Camera)을 자동으로 찾는다.")]
        [SerializeField] private Transform target;

        [Tooltip("캐릭터의 정면 기준이 되는 루트. 비워두면 이 컴포넌트가 붙은 Transform.")]
        [SerializeField] private Transform characterRoot;

        [Header("본 체인 (위 → 아래 순서 무관, 가중치와 짝을 맞출 것)")]
        [Tooltip("회전을 분산시킬 본. 보통 상반신 → 목 → 머리 순으로 넣는다.")]
        [SerializeField] private Transform[] boneChain;

        [Tooltip("각 본이 담당할 회전 비율. boneChain과 같은 길이여야 하며 내부에서 합이 1이 되도록 정규화된다.")]
        [SerializeField] private float[] boneWeights = { 0.2f, 0.3f, 0.5f };

        [Header("눈 (선택)")]
        [SerializeField] private Transform eyeLeft;
        [SerializeField] private Transform eyeRight;
        [Tooltip("눈이 타깃을 얼마나 정확히 조준할지.\n\n" +
                 "1이면 머리가 못 채운 각도를 눈이 전부 보충해 정확히 응시한다.\n" +
                 "낮추면 시선이 느슨해진다. 눈을 딱 고정하려면 1로 둔다.")]
        [Range(0f, 1f)][SerializeField] private float eyeContribution = 1f;

        [Tooltip("눈알이 좌우로 돌 수 있는 한계(도). 사람은 대개 15° 안쪽이다.\n" +
                 "너무 키우면 흰자가 드러나 부자연스럽다.")]
        [SerializeField] private float eyeMaxYaw = 16f;

        [Tooltip("눈알이 위아래로 돌 수 있는 한계(도).")]
        [SerializeField] private float eyeMaxPitch = 11f;

        [Header("가동 범위 (도)")]
        [SerializeField] private float maxYaw = 65f;
        [SerializeField] private float maxPitch = 32f;

        [Tooltip("이 각도를 넘어가면 타깃을 놓아준다. 목이 꺾이는 것을 막는다.")]
        [SerializeField] private float releaseYaw = 105f;

        [Header("반응")]
        [Tooltip("시선이 붙고 떨어지는 속도. 낮을수록 느긋하다.")]
        [SerializeField] private float weightSpeed = 2.5f;

        [Tooltip("고개가 따라 도는 속도. 낮을수록 나른하다.")]
        [SerializeField] private float trackSpeed = 8f;

        [Tooltip("전체 세기. 대화 중 0.8, 작업 중 0.3 정도로 런타임에 조절하면 좋다.")]
        [Range(0f, 1f)][SerializeField] private float gazeWeight = 1f;

        [Header("생동감")]
        [Tooltip("시선이 미세하게 흔들리는 폭(도). 0이면 완전히 고정되어 인형처럼 보인다.")]
        [SerializeField] private float driftAmplitude = 1.2f;
        [SerializeField] private float driftSpeed = 0.7f;

        // 런타임 상태
        private float currentWeight;      // 0~1, 부드럽게 보간됨
        private float currentYaw;
        private float currentPitch;
        private float driftSeedX;
        private float driftSeedY;
        private bool initialized;
        private float[] normalized;   // 계산용 정규화 사본. 인스펙터 값은 건드리지 않는다.

        /// <summary>대화 시스템 등에서 세기를 조절할 때 쓴다.</summary>
        public float GazeWeight
        {
            get => gazeWeight;
            set => gazeWeight = Mathf.Clamp01(value);
        }

        /// <summary>바라볼 대상을 런타임에 교체한다.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        /// <summary>현재 타깃을 실제로 바라보고 있는지. 대사 트리거 조건으로 쓸 수 있다.</summary>
        public bool IsEngaged => currentWeight > 0.5f;

        private void Awake()
        {
            if (characterRoot == null) characterRoot = transform;

            driftSeedX = Random.value * 100f;
            driftSeedY = Random.value * 100f;

            ValidateWeights();
            initialized = boneChain != null && boneChain.Length > 0;

            if (!initialized)
            {
                Debug.LogWarning($"[CharacterGaze] {name}: boneChain이 비어 있어 동작하지 않는다. " +
                                 "상반신/목/머리 본을 할당할 것.", this);
            }
        }

        /// <summary>
        /// 길이만 맞추고 값은 건드리지 않는다.
        ///
        /// 예전에는 여기서 합이 1이 되도록 정규화해 필드에 되썼는데,
        /// 그러면 인스펙터에서 한 칸을 입력하는 순간 나머지 칸이 전부 바뀐다.
        /// 정규화는 실제로 회전을 적용할 때만 한다.
        /// </summary>
        private void ValidateWeights()
        {
            if (boneChain == null) return;
            if (boneWeights != null && boneWeights.Length == boneChain.Length) return;

            var resized = new float[boneChain.Length];
            for (int i = 0; i < resized.Length; i++)
            {
                // 기존 값은 살리고, 새로 늘어난 칸만 균등값으로 채운다.
                resized[i] = (boneWeights != null && i < boneWeights.Length)
                    ? boneWeights[i]
                    : 1f / Mathf.Max(1, boneChain.Length);
            }
            boneWeights = resized;
        }

        /// <summary>
        /// 적용 직전에만 합이 1이 되도록 정규화한다.
        /// 인스펙터 값은 그대로 두고 계산용 사본만 만든다.
        /// </summary>
        private float[] NormalizedWeights()
        {
            int n = boneChain != null ? boneChain.Length : 0;
            if (normalized == null || normalized.Length != n) normalized = new float[n];

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = (boneWeights != null && i < boneWeights.Length)
                    ? Mathf.Max(0f, boneWeights[i]) : 0f;
                normalized[i] = w;
                sum += w;
            }

            if (sum <= Mathf.Epsilon)
            {
                for (int i = 0; i < n; i++) normalized[i] = n > 0 ? 1f / n : 0f;
                return normalized;
            }

            for (int i = 0; i < n; i++) normalized[i] /= sum;
            return normalized;
        }

        private void LateUpdate()
        {
            if (!initialized) return;

            if (target == null)
            {
                // XR Origin의 Main Camera를 늦게 찾을 수도 있으므로 매 프레임 가볍게 재시도한다.
                if (Camera.main != null) target = Camera.main.transform;
                if (target == null)
                {
                    FadeOut();
                    return;
                }
            }

            // 머리(체인의 마지막)를 기준점으로 방향을 계산한다.
            Transform head = boneChain[boneChain.Length - 1];
            if (head == null) return;

            Vector3 toTarget = target.position - head.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                FadeOut();
                return;
            }

            // 캐릭터 루트 기준 로컬 방향 → yaw/pitch 분해
            Vector3 local = characterRoot.InverseTransformDirection(toTarget.normalized);
            float rawYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float rawPitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;

            // 뒤쪽으로 넘어가면 시선을 놓는다.
            bool inRange = Mathf.Abs(rawYaw) <= releaseYaw;
            float desiredWeight = inRange ? gazeWeight : 0f;
            currentWeight = Mathf.MoveTowards(currentWeight, desiredWeight, weightSpeed * Time.deltaTime);

            if (currentWeight <= 0.001f) return;

            float targetYaw = Mathf.Clamp(rawYaw, -maxYaw, maxYaw);
            float targetPitch = Mathf.Clamp(rawPitch, -maxPitch, maxPitch);

            // 미세한 흔들림. 사람의 눈은 완전히 멈추지 않는다.
            if (driftAmplitude > 0f)
            {
                float t = Time.time * driftSpeed;
                targetYaw += (Mathf.PerlinNoise(driftSeedX, t) - 0.5f) * 2f * driftAmplitude;
                targetPitch += (Mathf.PerlinNoise(driftSeedY, t) - 0.5f) * 2f * driftAmplitude * 0.6f;
            }

            float lerp = 1f - Mathf.Exp(-trackSpeed * Time.deltaTime); // 프레임레이트 독립
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, lerp);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, lerp);

            ApplyChainRotation();

            // 체인을 돌린 뒤에 호출해야 눈의 위치가 갱신된 상태로 계산된다.
            ApplyEyeRotation();
        }

        private void FadeOut()
        {
            currentWeight = Mathf.MoveTowards(currentWeight, 0f, weightSpeed * Time.deltaTime);
        }

        private void ApplyChainRotation()
        {
            Vector3 up = characterRoot.up;
            Vector3 right = characterRoot.right;

            float yaw = currentYaw * currentWeight;
            float pitch = currentPitch * currentWeight;

            float[] w = NormalizedWeights();

            for (int i = 0; i < boneChain.Length; i++)
            {
                Transform bone = boneChain[i];
                if (bone == null) continue;

                // 월드 델타 회전이라 본의 로컬 축이 어떻든 상관없다.
                Quaternion delta = Quaternion.AngleAxis(yaw * w[i], up) *
                                   Quaternion.AngleAxis(pitch * w[i], right);
                bone.rotation = delta * bone.rotation;
            }
        }

        /// <summary>
        /// 눈을 타깃에 정확히 조준한다.
        ///
        /// 눈마다 따로 계산하는 것이 핵심이다. 두 눈은 머리 중심에서 좌우로 떨어져
        /// 있어서 같은 대상을 봐도 필요한 각도가 다르다. 머리 기준 각도를 비율로
        /// 나눠 쓰면 가까이서 볼수록 어긋난다.
        ///
        /// 체인이 이미 돌려놓은 만큼을 빼고 남은 각도만 눈에 준다.
        /// 반드시 ApplyChainRotation 뒤에 호출해야 한다 — 그래야 눈의 위치가
        /// 머리가 돌아간 뒤의 값이 된다.
        /// </summary>
        private void ApplyEyeRotation()
        {
            if (eyeLeft == null && eyeRight == null) return;
            if (eyeContribution <= 0f) return;

            Vector3 up = characterRoot.up;
            Vector3 right = characterRoot.right;

            // 체인이 실제로 돌린 양. 정규화된 가중치의 합이 1이므로 이 값이 머리의 총 회전이다.
            float appliedYaw = currentYaw * currentWeight;
            float appliedPitch = currentPitch * currentWeight;

            AimEye(eyeLeft, up, right, appliedYaw, appliedPitch);
            AimEye(eyeRight, up, right, appliedYaw, appliedPitch);
        }

        private void AimEye(Transform eye, Vector3 up, Vector3 right,
                            float appliedYaw, float appliedPitch)
        {
            if (eye == null) return;

            Vector3 toTarget = target.position - eye.position;
            if (toTarget.sqrMagnitude < 1e-6f) return;

            // 이 눈이 타깃을 보려면 캐릭터 기준으로 몇 도가 필요한지.
            Vector3 local = characterRoot.InverseTransformDirection(toTarget.normalized);
            float wantYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;

            // 머리가 못 채운 나머지만 눈이 담당한다.
            float yaw = Mathf.Clamp((wantYaw - appliedYaw) * eyeContribution, -eyeMaxYaw, eyeMaxYaw);
            float pitch = Mathf.Clamp((wantPitch - appliedPitch) * eyeContribution, -eyeMaxPitch, eyeMaxPitch);

            yaw *= currentWeight;
            pitch *= currentWeight;

            Quaternion delta = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right);
            eye.rotation = delta * eye.rotation;
        }

        /// <summary>
        /// 척추 체인과 눈 본을 이름으로 찾아 채운다.
        ///
        /// 이 FBX에는 스켈레톤이 두 벌 들어 있어(원본 MMD + 영문 renaming)
        /// 같은 이름의 본이 둘씩 있는 경우가 있다. Armature 아래 것을 우선한다.
        /// </summary>
        [ContextMenu("본 자동 할당")]
        public void AutoAssignBones()
        {
            Transform spine = FindBone("Spine", "上半身");
            Transform chest = FindBone("Chest", "上半身2", "UpperChest");
            Transform neck = FindBone("Neck", "首");
            Transform head = FindBone("Head", "頭");

            var chain = new System.Collections.Generic.List<Transform>();
            var weights = new System.Collections.Generic.List<float>();

            if (spine != null) { chain.Add(spine); weights.Add(0.15f); }
            if (chest != null) { chain.Add(chest); weights.Add(0.20f); }
            if (neck != null) { chain.Add(neck); weights.Add(0.30f); }
            if (head != null) { chain.Add(head); weights.Add(0.35f); }

            if (head == null)
            {
                Debug.LogWarning("[CharacterGaze] Head 본을 찾지 못했다. 직접 할당할 것.", this);
                return;
            }

            boneChain = chain.ToArray();
            boneWeights = weights.ToArray();
            ValidateWeights();

            eyeLeft = FindBone("Eye_L", "左目", "EyeLeft");
            eyeRight = FindBone("Eye_R", "右目", "EyeRight");

            if (characterRoot == null) characterRoot = transform;

            var names = new System.Collections.Generic.List<string>();
            foreach (Transform t in boneChain) names.Add(t.name);

            Debug.Log($"[CharacterGaze] 체인: {string.Join(" → ", names)}   " +
                      $"눈: {(eyeLeft != null ? eyeLeft.name : "없음")} / " +
                      $"{(eyeRight != null ? eyeRight.name : "없음")}", this);
        }

        /// <summary>이름으로 본을 찾는다. 같은 이름이 여럿이면 Armature 아래를 우선한다.</summary>
        private Transform FindBone(params string[] candidates)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);

            foreach (string name in candidates)
            {
                Transform fallback = null;

                foreach (Transform t in all)
                {
                    if (t.name != name) continue;

                    // 중복 이름 대비. Armature 계보에 있는 것을 먼저 쓴다.
                    for (Transform p = t; p != null; p = p.parent)
                    {
                        if (p.name == "Armature") return t;
                    }
                    fallback ??= t;
                }

                if (fallback != null) return fallback;
            }
            return null;
        }

        /// <summary>허리 위주로 도는 것을 막는 권장 배분. 머리로 갈수록 크게.</summary>
        [ContextMenu("가중치 권장값으로")]
        public void ResetWeightsToDefault()
        {
            if (boneChain == null || boneChain.Length == 0) return;

            // 체인 길이에 맞춰 아래(허리)에서 위(머리)로 갈수록 커지게 배분한다.
            float[] preset = boneChain.Length switch
            {
                4 => new[] { 0.15f, 0.20f, 0.30f, 0.35f },
                3 => new[] { 0.20f, 0.30f, 0.50f },
                2 => new[] { 0.35f, 0.65f },
                _ => null,
            };

            if (preset != null) boneWeights = preset;
            else
            {
                boneWeights = new float[boneChain.Length];
                for (int i = 0; i < boneChain.Length; i++)
                    boneWeights[i] = (i + 1f) / boneChain.Length;
            }

            Debug.Log($"[CharacterGaze] 가중치를 권장값으로 되돌렸다: " +
                      string.Join(" / ", boneWeights), this);
        }

        private void OnValidate()
        {
            ValidateWeights();

            // 음수는 회전을 반대로 돌려 상체가 꼬인다. 입력 단계에서 막는다.
            if (boneWeights != null)
            {
                for (int i = 0; i < boneWeights.Length; i++)
                {
                    if (boneWeights[i] < 0f)
                    {
                        Debug.LogWarning(
                            $"[CharacterGaze] Bone Weights[{i}]에 음수({boneWeights[i]:F2})가 들어왔다. " +
                            "0으로 되돌린다. 값은 0 이상이어야 하고, 합이 1일 필요는 없다 " +
                            "(적용 직전에 비율로 환산된다).", this);
                        boneWeights[i] = 0f;
                    }
                }
            }

            // 눈 본이 체인에 들어가는 실수가 잦다. 그러면 고개는 안 돌고
            // 눈알만 미세하게 움직여서 원인을 찾기 어렵다.
            if (boneChain != null)
            {
                foreach (Transform t in boneChain)
                {
                    if (t == null) continue;
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("eye") || n.Contains("目"))
                    {
                        Debug.LogWarning(
                            $"[CharacterGaze] Bone Chain에 눈 본({t.name})이 들어 있다. " +
                            "여기는 Spine/Chest/Neck/Head가 들어갈 자리이고, 눈은 아래 " +
                            "'눈 (선택)' 항목에 넣는다. 컨텍스트 메뉴의 '본 자동 할당'을 쓰면 된다.", this);
                        break;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (boneChain == null || boneChain.Length == 0) return;
            Transform head = boneChain[boneChain.Length - 1];
            if (head == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(head.position, 0.04f);

            if (target != null)
            {
                Gizmos.color = IsEngaged ? Color.green : new Color(1f, 0.6f, 0.1f);
                Gizmos.DrawLine(head.position, target.position);
            }
        }
    }
}
