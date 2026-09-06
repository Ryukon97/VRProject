using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 팔 체인 밖으로 갈라져 나간 트위스트 본을 팔에 다시 붙인다.
    ///
    /// 이 모델의 계층은 이렇게 되어 있다:
    ///
    ///   Chest → Left shoulder ─┬─ Left arm → Left elbow → Left wrist → 손가락
    ///                          └─ zArmTwist_L → zHandTwist_L        ← 별개 가지
    ///
    /// MMD에서 腕捩(팔 비틀림)은 원래 腕의 자식이어야 하는데, 이 변환본에서는
    /// 어깨에서 평행하게 갈라져 나갔다. 그래서 팔이 움직여도 어깨만 따라가고,
    /// 여기 물린 살(손목 주변 웨이트의 절반 가까이)이 바인드 포즈에 남는다.
    ///
    /// 휴머노이드에는 트위스트 본 슬롯이 없어서 매핑으로는 해결되지 않는다.
    /// 계층을 바꾸는 대신, 매 프레임 원래 붙어 있어야 할 본을 따라가게 만든다.
    ///
    /// 바인드 시점의 상대 위치는 SkinnedMeshRenderer의 bindposes에서 구한다.
    /// 현재 포즈와 무관하게 정확하므로, 애니메이션이 돌고 있어도 안전하다.
    /// </summary>
    [DefaultExecutionOrder(100)]   // Animator가 포즈를 쓴 뒤
    [DisallowMultipleComponent]
    [AddComponentMenu("VRProject/Twist Bone Follower")]
    public class TwistBoneFollower : MonoBehaviour
    {
        [Serializable]
        public class Pair
        {
            [Tooltip("팔 체인 밖에 있는 트위스트 본.")]
            public Transform 트위스트본;

            [Tooltip("따라갈 본. MMD 관례상 腕捩은 위팔을, 手捩은 팔뚝을 따른다.")]
            public Transform 기준본;

            [Tooltip("1이면 기준본을 그대로 따라간다. 낮추면 원래 자리와 섞인다.")]
            [Range(0f, 1f)] public float 세기 = 1f;
        }

        [Tooltip("bindposes를 읽을 렌더러. 비워두면 자식에서 찾는다.")]
        [SerializeField] private SkinnedMeshRenderer 렌더러;

        [Tooltip("트위스트 본과 기준본의 짝. 컨텍스트 메뉴의 '자동 할당'을 쓰면 채워진다.")]
        [SerializeField] private List<Pair> 짝 = new List<Pair>();

        // 바인드 시점의 기준본→트위스트본 상대 행렬
        private Matrix4x4[] offsets;
        private bool ready;

        private void OnEnable() => Rebuild();

        private void OnValidate()
        {
            if (Application.isPlaying) Rebuild();
        }

        /// <summary>
        /// bindposes에서 상대 행렬을 뽑는다.
        ///
        /// bindposes[i]는 바인드 시점 본의 월드 행렬의 역행렬(렌더러 기준)이므로,
        /// 두 본 사이의 상대 행렬은 bindposes[기준] * bindposes[트위스트]⁻¹ 이 된다.
        /// 렌더러의 월드 행렬이 양쪽에서 상쇄돼 현재 포즈에 영향받지 않는다.
        /// </summary>
        private void Rebuild()
        {
            ready = false;

            if (렌더러 == null) 렌더러 = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (렌더러 == null || 렌더러.sharedMesh == null || 짝 == null || 짝.Count == 0) return;

            Matrix4x4[] bind = 렌더러.sharedMesh.bindposes;
            Transform[] bones = 렌더러.bones;
            if (bind == null || bones == null || bind.Length != bones.Length) return;

            var index = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && !index.ContainsKey(bones[i])) index[bones[i]] = i;

            offsets = new Matrix4x4[짝.Count];

            for (int i = 0; i < 짝.Count; i++)
            {
                Pair p = 짝[i];
                offsets[i] = Matrix4x4.identity;

                if (p?.트위스트본 == null || p.기준본 == null) continue;
                if (!index.TryGetValue(p.트위스트본, out int ti)) continue;
                if (!index.TryGetValue(p.기준본, out int si)) continue;

                offsets[i] = bind[si] * bind[ti].inverse;
            }

            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready || 짝 == null) return;

            for (int i = 0; i < 짝.Count; i++)
            {
                Pair p = 짝[i];
                if (p?.트위스트본 == null || p.기준본 == null || p.세기 <= 0f) continue;

                Matrix4x4 target = p.기준본.localToWorldMatrix * offsets[i];

                Vector3 pos = target.GetColumn(3);
                Quaternion rot = target.rotation;

                if (p.세기 >= 0.999f)
                {
                    p.트위스트본.SetPositionAndRotation(pos, rot);
                }
                else
                {
                    p.트위스트본.SetPositionAndRotation(
                        Vector3.Lerp(p.트위스트본.position, pos, p.세기),
                        Quaternion.Slerp(p.트위스트본.rotation, rot, p.세기));
                }
            }
        }

        /// <summary>
        /// 이름 규칙으로 짝을 채운다.
        /// MMD 관례: 腕捩(ArmTwist)은 위팔을, 手捩(HandTwist)은 팔뚝을 따른다.
        /// </summary>
        [ContextMenu("자동 할당")]
        public void AutoAssign()
        {
            var all = GetComponentsInChildren<Transform>(true);
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in all)
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            짝 = new List<Pair>();

            // (트위스트 본, 따라갈 본) — 좌우 모두
            var wanted = new (string twist, string source)[]
            {
                ("zArmTwist_L",  "Left arm"),
                ("zHandTwist_L", "Left elbow"),
                ("zArmTwist_R",  "Right arm"),
                ("zHandTwist_R", "Right elbow"),
            };

            foreach ((string twist, string source) in wanted)
            {
                if (!byName.TryGetValue(twist, out Transform t)) continue;
                if (!byName.TryGetValue(source, out Transform s)) continue;
                짝.Add(new Pair { 트위스트본 = t, 기준본 = s, 세기 = 1f });
            }

            Rebuild();

            if (짝.Count == 0)
                Debug.LogWarning("[TwistBoneFollower] 트위스트 본을 찾지 못했다. 직접 할당할 것.", this);
            else
                Debug.Log($"[TwistBoneFollower] {짝.Count}쌍 할당됨. " +
                          string.Join(", ", 짝.ConvertAll(p => $"{p.트위스트본.name}←{p.기준본.name}")), this);
        }
    }
}
