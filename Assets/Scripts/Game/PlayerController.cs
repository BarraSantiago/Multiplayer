using Network;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;

namespace Game
{
    public class PlayerController : MonoBehaviour
    {

        [SerializeField] private float speed = 5f;

        public GameObject bulletPrefab;
        public GameObject configMenu;

        private Vector2 _direction;
        
        private void Update()
        {
            MovePlayer();
        }

        private void MovePlayer()
        {
            if (_direction == Vector2.zero) return;
            Vector3 movement = new Vector3(_direction.x, _direction.y, 0);
            transform.position += movement * (speed * Time.deltaTime);
            
            NetworkManager.Instance.MovePlayer(Vec3.FromVector3(transform.position));
        }

        public void OnMove(InputValue context)
        {
            _direction = context.Get<Vector2>();
        }

        public void OnFire(InputValue context)
        {
            // Instantiate the bullet
            GameObject bulletBody = Instantiate(bulletPrefab);

            Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z);
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(screenPosition);
            Vector3 direction = (mousePosition - transform.position).normalized;

            direction.z = 0;
            
            bulletBody.transform.position = transform.position + direction;

            Bullet bullet = bulletBody.AddComponent<Bullet>();
            bullet.clientID = NetworkManager.Instance.thisPlayer.clientID;
            bullet.SetTarget(direction);
            NetworkManager.Instance.FireBullet( Vec3.FromVector3(bullet.transform.position), Vec3.FromVector3(direction));
        }

        public void OnPause(InputValue context)
        {
            configMenu.SetActive(!configMenu.activeSelf);
        }
    }
}