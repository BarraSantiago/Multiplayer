using UnityEngine;

namespace Game
{
    public class Bullet : MonoBehaviour
    {
        public float speed = 10f;
        public int damage = 1;
        public float destroyTimer = 4;
        public int clientID;

        private Vector3 target;

        public void SetTarget(Vector3 target)
        {
            this.target = target;
            this.target.z = 0;
            Destroy(gameObject, destroyTimer);
        }

        void Update()
        {
            transform.Translate(target * (speed * Time.deltaTime));
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                // Reduce the player's HP
                Player player = other.gameObject.GetComponent<Player>();

                if (player.clientID == clientID) return;

                player.ReduceHealth(damage);
            }

            Destroy(gameObject);
        }
    }
}