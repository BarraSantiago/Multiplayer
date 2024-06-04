using System.Collections;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        [FormerlySerializedAs("text")] [SerializeField]
        private TMP_Text countdown;

        [SerializeField] private InputActionAsset inputMap;
        [SerializeField] private GameObject configMenu;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Button resume;
        [SerializeField] private Button quit;

        private double timer;
        [SerializeField] private double gameTimer = 12;
        private GameObject _player;

        private void Awake()
        {
            NetworkManager.OnPlayerSpawned += InitializePlayer;
            NetworkManager.OnRejected += OnRejected;
            quit.onClick.AddListener(NetworkManager.CheckDisconnect);
            NetworkManager.OnGameStart += OnGameStarted;
            NetworkManager.OnCountdownStart += OnCountdownStarted;
            NetworkManager.OnWinner += OnWinner;
        }

        private void OnWinner(string obj)
        {
            countdown.gameObject.SetActive(false);
        }

        private void OnCountdownStarted()
        {
            countdown.gameObject.SetActive(true);
            timer = NetworkManager.Instance.countdownSeconds;
            StartCoroutine(Countdown());
        }

        private IEnumerator Countdown()
        {
            while (timer > 0)
            {
                countdown.text = "Countdown to start: " + timer;

                yield return new WaitForSeconds(1f);

                timer--;
            }
        }


        private void OnRejected(string message)
        {
            NetworkManager.OnPlayerSpawned += InitializePlayer;
        }

        private void InitializePlayer(GameObject obj)
        {
            _player = obj;
            PlayerController controller = obj.AddComponent<PlayerController>();

            controller.bulletPrefab = bulletPrefab;
            controller.configMenu = configMenu;

            NetworkManager.OnPlayerSpawned -= InitializePlayer;
        }

        private void OnGameStarted()
        {
            PlayerInput input = _player.AddComponent<PlayerInput>();
            input.actions = inputMap;
            input.actions.FindActionMap("Player").Enable();
            input.notificationBehavior = PlayerNotifications.SendMessages;

            input.actions.Enable();


            StartCoroutine(GameCountdown());
        }

        private IEnumerator GameCountdown()
        {
            while (gameTimer > 0)
            {
                countdown.text = "Remaining time: " + gameTimer;

                yield return new WaitForSeconds(1f);

                gameTimer--;
            }

            if (NetworkManager.Instance.IsServer)
            {
                NetworkManager.Instance.EndGame();
            }
        }
    }
}