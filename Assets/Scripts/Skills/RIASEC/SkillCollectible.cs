using UnityEngine;

namespace SunodGame.Demo
{
    [RequireComponent(typeof(Collider2D))]
    public class SkillCollectible : MonoBehaviour
    {
        [SerializeField] private int skillIndex;
        [SerializeField] private DemoGameplayManager manager;

        public void Configure(int index, DemoGameplayManager owner)
        {
            skillIndex = index;
            manager = owner;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (manager == null) return;
            if (!collision.CompareTag("Player")) return;

            manager.CollectSkill(skillIndex, gameObject);
        }
    }
}
