using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MonsterData",
    menuName = "Game/Monster Data"
)]
public class MonsterData : ScriptableObject
{
    [Serializable]
    public class DropEntry
    {
        public ConsumableId consumableId;

        [Range(0f, 1f)]
        public float dropChance;
    }

    // 기존 몬스터 데이터 필드들...

    [Header("Drops")]
    [SerializeField]
    private List<DropEntry> drops = new();

    public IReadOnlyList<DropEntry> Drops =>
        drops;
}