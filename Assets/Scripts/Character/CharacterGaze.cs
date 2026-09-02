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
        [Tooltip("눈이 추가로 도는 각도 비율. 머리가 덜 도는 만큼 눈이 보충한다.")]
        [Range(0f, 1f)][SerializeField] private float eyeContribution = 0.35f;
        [SerializeField] private float eyeMaxYaw = 14f;
        [SerializeField] private float eyeMaxPitch = 9f;

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

        private void ValidateWeights()
        {
            if (boneChain == null) return;

            // 가중치 길이가 안 맞으면 균등 분배로 맞춘다.
            if (boneWeights == null || boneWeights.Length != boneChain.Length)
            {
                boneWeights = new float[boneChain.Length];
                for (int i = 0; i < boneChain.Length; i++)
                    boneWeights[i] = 1f / Mathf.Max(1, boneChain.Length);
                return;
            }

            float sum = 0f;
            for (int i = 0; i < boneWeights.Length; i++) sum += Mathf.Max(0f, boneWeights[i]);
            if (sum <= Mathf.Epsilon)
            {
                for (int i = 0; i < boneWeights.Length; i++) boneWeights[i] = 1f / boneWeights.Length;
                return;
            }

            for (int i = 0; i < boneWeights.Length; i++)
                boneWeights[i] = Mathf.Max(0f, boneWeights[i]) / sum;
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
            ApplyEyeRotation(rawYaw, rawPitch);
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

            for (int i = 0; i < boneChain.Length; i++)
            {
                Transform bone = boneChain[i];
                if (bone == null) continue;

                float w = boneWeights[i];
                // 월드 델타 회전이라 본의 로컬 축이 어떻든 상관없다.
                Quaternion delta = Quaternion.AngleAxis(yaw * w, up) *
                                   Quaternion.AngleAxis(pitch * w, right);
                bone.rotation = delta * bone.rotation;
            }
        }

        private void ApplyEyeRotation(float rawYaw, float rawPitch)
        {
            if (eyeLeft == null && eyeRight == null) return;
            if (eyeContribution <= 0f) return;

            // 머리가 다 돌지 못한 나머지를 눈이 보충한다.
            float residualYaw = Mathf.Clamp(rawYaw - currentYaw, -eyeMaxYaw, eyeMaxYaw);
            float residualPitch = Mathf.Clamp(rawPitch - currentPitch, -eyeMaxPitch, eyeMaxPitch);

            float yaw = (currentYaw * eyeContribution + residualYaw) * currentWeight;
            float pitch = (currentPitch * eyeContribution + residualPitch) * currentWeight;

            yaw = Mathf.Clamp(yaw, -eyeMaxYaw, eyeMaxYaw);
            pitch = Mathf.Clamp(pitch, -eyeMaxPitch, eyeMaxPitch);

            Vector3 up = characterRoot.up;
            Vector3 right = characterRoot.right;
            Quaternion delta = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right);

            if (eyeLeft != null) eyeLeft.rotation = delta * eyeLeft.rotation;
            if (eyeRight != null) eyeRight.rotation = delta * eyeRight.rotation;
        }

        private void OnValidate()
        {
            ValidateWeights();
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
