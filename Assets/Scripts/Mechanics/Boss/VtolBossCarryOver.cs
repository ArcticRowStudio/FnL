using System;
using UnityEngine;

namespace Platformer.Mechanics.Boss
{
    /// <summary>
    /// What the VTOL boss had left when it fled the helipad, kept for the sailing-ship map where
    /// it crash-lands for its last phase. The ship is a separate map, so this outlives the scene
    /// (PlayerPrefs) until that fight clears it.
    /// </summary>
    public static class VtolBossCarryOver
    {
        const string Key = "VtolBoss.EscapeRecord";

        [Serializable]
        public class Record
        {
            public int hp;
            public int maxHp;
            public bool leftDuctWrecked;
            public bool rightDuctWrecked;
            public bool leftPodBroken;
            public bool rightPodBroken;
        }

        public static void Save(Record record)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out Record record)
        {
            record = null;
            if (!PlayerPrefs.HasKey(Key)) return false;
            record = JsonUtility.FromJson<Record>(PlayerPrefs.GetString(Key));
            return record != null;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
        }
    }
}
