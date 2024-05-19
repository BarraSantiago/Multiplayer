using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;

namespace Game
{
    public class PlayerController : MonoBehaviour
    {
        public static Action<Vec3> onMove;

        [SerializeField] private float speed = 5f;
        [SerializeField] private GameObject bulletPrefab;

        public GameObject configMenu;

        private CharacterController characterController;
        private Vector2 _direction;

        private void Awake()
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }

        private void Update()
        {
            MovePlayer();
        }

        private void MovePlayer()
        {
            if (_direction == Vector2.zero) return;
            Vector3 movement = new Vector3(_direction.x, _direction.y, 0);
            characterController.Move(movement * (speed * Time.deltaTime));
            onMove?.Invoke(Vec3.FromVector3(transform.position));
        }

        public void OnMove(InputValue context)
        {
            _direction = context.Get<Vector2>();
        }

        public void OnFire(InputValue context)
        {
        }

        public void OnPause(InputValue context)
        {
            configMenu.SetActive(!configMenu.activeSelf);
        }
    }
}