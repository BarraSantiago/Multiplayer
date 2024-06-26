﻿using System;
using Network;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class Player : MonoBehaviour
    {
        public int clientID = -1;
        public string name;
        public int hp = 3;
        
        public void ReduceHealth(int damage)
        {
            hp -= damage;

            if (hp > 0) return;

            if (!NetworkManager.Instance.IsServer && NetworkManager.Instance.thisPlayer.clientID == clientID)
            {
                NetworkManager.Instance.Reject("You died.");
            }
            Destroy(gameObject);
        }
    }
}