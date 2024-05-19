using System;
using Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utils;

namespace Game
{
    [Serializable]
    public struct Player
    {
        public int clientID;
        public string name;
        public int hp;
        public Vec3 position;
        public bool hasBody;
        public GameObject body;
    }
    
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputMap;
        [SerializeField] private GameObject configMenu;
        [SerializeField] private Button resume;
        [SerializeField] private Button quit;

        private void Awake()
        {
            Handshake.onPlayerSpawned += InitializePlayer;
        }

        private void InitializePlayer(GameObject obj)
        {
            PlayerInput input = obj.AddComponent<PlayerInput>();
            obj.AddComponent<PlayerController>();
            input.actions = inputMap;
            input.actions.FindActionMap("Player").Enable();
            input.notificationBehavior = PlayerNotifications.SendMessages;

            input.actions.Enable();
            
            Handshake.onPlayerSpawned -= InitializePlayer;
        }
    }
}