using System;
using Network;
using UnityEngine;
using Utils;

namespace Game
{
    [Serializable]
    public class Player
    {
        public int clientID;
        public string name;
        public int hp;
        public Vec3 position;
        public bool hasBody;
        public GameObject body;
        
        
        public void ReduceHealth(int damage)
        {
            hp -= damage;
            
            if(hp <= 0)
            {
                NetworkManager.Instance.Disconnect();
            }
        }
    }
}