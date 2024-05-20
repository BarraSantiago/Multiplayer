﻿using Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputMap;
        [SerializeField] private GameObject configMenu;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Button resume;
        [SerializeField] private Button quit;

        private void Awake()
        {
            NetworkManager.Instance.OnPlayerSpawned += InitializePlayer;
            quit.onClick.AddListener(NetworkManager.Instance.CheckDisconnect);
        }

        private void InitializePlayer(GameObject obj)
        {
            PlayerController controller = obj.AddComponent<PlayerController>();
            
            controller.bulletPrefab = bulletPrefab;
            controller.configMenu = configMenu;
            
            PlayerInput input = obj.AddComponent<PlayerInput>();
            input.actions = inputMap;
            input.actions.FindActionMap("Player").Enable();
            input.notificationBehavior = PlayerNotifications.SendMessages;

            input.actions.Enable();
            
            NetworkManager.Instance.OnPlayerSpawned -= InitializePlayer;
        }
    }
}